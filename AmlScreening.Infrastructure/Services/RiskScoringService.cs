using System.Text;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using AmlScreening.Domain.Entities.Compliance;
using AmlScreening.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AmlScreening.Infrastructure.Services;

public sealed partial class RiskScoringService : IRiskScoringService
{
    private readonly AmlScreeningDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public RiskScoringService(
        AmlScreeningDbContext db,
        ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }


    // =====================================================
    // RUNTIME RISK CALCULATION
    // =====================================================

    public async Task<RiskScoringResult> CalculateAsync(
        RiskScoringInput input,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.PartnerId <= 0)
        {
            throw new InvalidOperationException(
                "A partner must be selected before risk can be calculated.");
        }

        if (!string.Equals(
                input.Currency,
                "NPR",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The current risk model supports NPR transactions only.");
        }

        if (input.Amount <= 0 ||
            decimal.Round(input.Amount, 2) != input.Amount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.Amount),
                "Amount must be positive with at most two decimal places.");
        }

        if (input.RegisteredReceiverCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.RegisteredReceiverCount),
                "Registered receiver count cannot be negative.");
        }


        var now = DateTime.UtcNow;


        // =================================================
        // LOAD EXACTLY ONE ACTIVE APPROVED RISK MODEL
        // =================================================

        var activeModels =
            await _db.RiskModels
                .AsNoTracking()
                .Where(item =>
                    item.PartnerId == _currentUser.PartnerId &&
                    item.IsActive &&
                    item.DeleteFg == "N" &&
                    item.ApprovalStatus == "APPROVED" &&
                    item.EffectiveFrom <= now &&
                    (
                        !item.EffectiveTo.HasValue ||
                        item.EffectiveTo >= now
                    ))
                .OrderByDescending(
                    item => item.ModelVersion)
                .Take(2)
                .ToListAsync(
                    cancellationToken);


        if (activeModels.Count == 0)
        {
            throw new InvalidOperationException(
                "No approved risk model is active for this partner.");
        }


        if (activeModels.Count > 1)
        {
            throw new InvalidOperationException(
                "More than one approved risk model is active. Risk calculation has been stopped.");
        }


        // IMPORTANT:
        // This was missing in your current code.
        var model = activeModels[0];


        var modelWeight =
            model.CustomerWeightPercent +
            model.TransactionWeightPercent +
            model.ScreeningWeightPercent;


        if (model.CustomerWeightPercent < 0 ||
            model.CustomerWeightPercent > 100 ||
            model.TransactionWeightPercent < 0 ||
            model.TransactionWeightPercent > 100 ||
            model.ScreeningWeightPercent < 0 ||
            model.ScreeningWeightPercent > 100 ||
            modelWeight != 100m)
        {
            throw new InvalidOperationException(
                "Risk model weights must total exactly 100 percent.");
        }


        // =================================================
        // RISK FACTORS
        // =================================================

        var factors =
            await _db.RiskFactors
                .AsNoTracking()
                .Where(item =>
                    item.PartnerId == _currentUser.PartnerId &&
                    item.IsActive)
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(
                    cancellationToken);


        if (factors.Count == 0)
        {
            throw new InvalidOperationException(
                "No active risk factors are configured for this partner.");
        }


        var factorIds =
            factors
                .Select(item => item.Id)
                .ToArray();


        // =================================================
        // APPROVED / EFFECTIVE RULES ONLY
        // =================================================

        var currentRules =
            await _db.RiskFactorRules
                .AsNoTracking()
                .Where(item =>
                    item.PartnerId == _currentUser.PartnerId &&
                    factorIds.Contains(item.RiskFactorId) &&
                    item.IsActive &&
                    item.DeleteFg == "N" &&
                    item.ApprovalStatus == "APPROVED" &&
                    item.EffectiveFrom <= now &&
                    (
                        !item.EffectiveTo.HasValue ||
                        item.EffectiveTo >= now
                    ))
                .ToListAsync(
                    cancellationToken);


        // =================================================
        // CALCULATE FACTORS
        // =================================================

        var factorResults =
            new List<RiskFactorResult>();


        foreach (var factor in factors)
        {
            var resolvedValue =
                ResolveValue(
                    factor.FactorCode,
                    input);


            var factorRules =
                currentRules
                    .Where(item =>
                        item.RiskFactorId ==
                        factor.Id)
                    .ToList();


            var matchedRule =
                MatchRule(
                    factor,
                    resolvedValue,
                    factorRules)
                ?? throw new InvalidOperationException(
                    $"No approved rule matches factor '{factor.FactorName}' and value '{resolvedValue.DisplayValue}'.");


            if (matchedRule.Score is < 0 or > 100 ||
                factor.WeightPercent <= 0 ||
                factor.WeightPercent > 100)
            {
                throw new InvalidOperationException(
                    "A configured score or factor weight is invalid.");
            }


            var rawScore =
                matchedRule.Score;


            var weightedScore =
                Math.Round(
                    rawScore *
                    factor.WeightPercent /
                    100m,
                    2,
                    MidpointRounding.AwayFromZero);


            factorResults.Add(
                new RiskFactorResult
                {
                    FactorCode =
                        factor.FactorCode,

                    FactorName =
                        factor.FactorName,

                    ScoreCategory =
                        factor.ScoreCategory
                            .ToUpperInvariant(),

                    InputValue =
                        resolvedValue.DisplayValue,

                    RawScore =
                        rawScore,

                    WeightPercent =
                        factor.WeightPercent,

                    WeightedScore =
                        weightedScore,

                    RuleVersion =
                        matchedRule.RuleVersion,

                    Explanation =
                        string.IsNullOrWhiteSpace(
                            matchedRule.Remarks)
                            ? $"{factor.FactorName}: {resolvedValue.DisplayValue} scored {rawScore}."
                            : matchedRule.Remarks
                });
        }


        // =================================================
        // CATEGORY SCORES
        // =================================================

        var customerRisk =
            CalculateCategoryScore(
                factorResults,
                "CUSTOMER");


        var transactionRisk =
            Math.Max(
                CalculateCategoryScore(
                    factorResults,
                    "TRANSACTION"),

                Math.Clamp(
                    input.AdditionalTransactionRiskScore,
                    0,
                    100));


        var screeningRisk =
            Math.Clamp(
                input.ScreeningRiskScore,
                0,
                100);


        var overallRisk =
            Math.Clamp(
                (int)Math.Round(
                    (
                        customerRisk *
                        model.CustomerWeightPercent +

                        transactionRisk *
                        model.TransactionWeightPercent +

                        screeningRisk *
                        model.ScreeningWeightPercent
                    ) / 100m,

                    MidpointRounding.AwayFromZero),

                0,
                100);


        // =================================================
        // APPROVED RISK BANDS
        // =================================================

        var bands =
            await _db.RiskBands
                .AsNoTracking()
                .Where(item =>
                    item.PartnerId ==
                        _currentUser.PartnerId &&
                    item.IsActive &&
                    item.DeleteFg == "N" &&
                    item.ApprovalStatus ==
                        "APPROVED")
                .ToListAsync(
                    cancellationToken);


        if (bands.Count == 0)
        {
            throw new InvalidOperationException(
                "No approved risk bands are configured for this partner.");
        }


        var currentBandVersion =
            bands.Max(
                item => item.BandVersion);


        var currentBands =
            bands
                .Where(item =>
                    item.BandVersion ==
                    currentBandVersion)
                .ToList();


        ValidateBands(
            currentBands);


        var matchingBands =
            currentBands
                .Where(item =>
                    overallRisk >=
                        item.MinimumScore &&
                    (
                        !item.MaximumScore.HasValue ||
                        overallRisk <=
                            item.MaximumScore.Value
                    ))
                .ToList();


        if (matchingBands.Count != 1)
        {
            throw new InvalidOperationException(
                $"Risk-band configuration must contain exactly one band for score {overallRisk}.");
        }


        var band =
            matchingBands[0];


        return new RiskScoringResult
        {
            CustomerRiskScore =
                customerRisk,

            TransactionRiskScore =
                transactionRisk,

            ScreeningRiskScore =
                screeningRisk,

            OverallRiskScore =
                overallRisk,

            RiskLevel =
                band.RiskLevel
                    .ToUpperInvariant(),

            RecommendedDecision =
                band.Decision
                    .ToUpperInvariant(),

            ModelVersion =
                $"{model.ModelCode}:v{model.ModelVersion}",

            Factors =
                factorResults
        };
    }


    // =====================================================
    // CONFIGURATION / SETUP PAGE
    // =====================================================

    public async Task<RiskScoringSetupResult> GetSetupAsync(
        string? factorCode,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigurationAccess(
            AppPermissions.Risk.View);


        pageSize = 10;

        pageNumber =
            Math.Max(
                1,
                pageNumber);


        // =================================================
        // RISK FACTORS
        // =================================================

        var factors =
            await _db.RiskFactors
                .AsNoTracking()
                .Where(item =>
                    item.PartnerId ==
                        _currentUser.PartnerId &&
                    item.IsActive)
                .OrderBy(
                    item => item.DisplayOrder)
                .ToListAsync(
                    cancellationToken);


        var selected =
            factors.FirstOrDefault(
                item =>
                    item.FactorCode ==
                    factorCode)
            ??
            factors.FirstOrDefault();


        var selectedFactorId =
            selected?.Id ?? 0;


        // =================================================
        // RULE DRAFT STATUS
        // =================================================

        var hasRuleDraft =
            selectedFactorId > 0 &&
            await RuleHistory()
                .AnyAsync(
                    x =>
                        x.RiskFactorId ==
                            selectedFactorId &&
                        x.DeleteFg == "N" &&
                        (
                            x.ApprovalStatus ==
                                "DRAFT" ||
                            x.ApprovalStatus ==
                                "PENDING"
                        ),
                    cancellationToken);


        var ruleQuery =
            RuleHistory()
                .AsNoTracking()
                .Where(item =>
                    item.DeleteFg == "N" &&
                    (
                        (
                            item.ApprovalStatus ==
                                "APPROVED" &&
                            item.IsActive
                        )
                        ||
                        item.ApprovalStatus ==
                            "PENDING"
                        ||
                        item.ApprovalStatus ==
                            "DRAFT"
                    ));


        if (selected is not null)
        {
            ruleQuery =
                ruleQuery.Where(
                    item =>
                        item.RiskFactorId ==
                        selected.Id);
        }
        else
        {
            ruleQuery =
                ruleQuery.Where(_ => false);
        }


        if (!string.IsNullOrWhiteSpace(search))
        {
            var term =
                search.Trim();


            ruleQuery =
                ruleQuery.Where(
                    item =>
                        (item.ValueCode ??
                         string.Empty)
                            .Contains(term)
                        ||
                        (item.ValueName ??
                         string.Empty)
                            .Contains(term)
                        ||
                        (item.Remarks ??
                         string.Empty)
                            .Contains(term));
        }


        var totalCount =
            await ruleQuery.CountAsync(
                cancellationToken);


        var totalPages =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    totalCount /
                    (double)pageSize));


        pageNumber =
            Math.Min(
                pageNumber,
                totalPages);


        var rules =
            await ruleQuery
                .OrderBy(
                    item => item.MinimumValue)
                .ThenBy(
                    item => item.ValueName)
                .ThenBy(
                    item => item.ValueCode)
                .ThenBy(
                    item => item.Id)
                .Skip(
                    (pageNumber - 1) *
                    pageSize)
                .Take(
                    pageSize)
                .ToListAsync(
                    cancellationToken);


        // =================================================
        // RULE MAKER
        // =================================================

        var ruleMakers =
            selectedFactorId > 0
                ? await RuleHistory()
                    .Where(x =>
                        x.RiskFactorId ==
                            selectedFactorId &&
                        x.DeleteFg == "N" &&
                        (
                            x.ApprovalStatus ==
                                "DRAFT" ||
                            x.ApprovalStatus ==
                                "PENDING"
                        ))
                    .Select(
                        x => x.MakerUserId)
                    .Distinct()
                    .ToListAsync(
                        cancellationToken)
                : new List<long?>();


        // =================================================
        // RISK BANDS
        // =================================================

        var bandVersion =
            await BandHistory()
                .MaxAsync(
                    x =>
                        (int?)x.BandVersion,
                    cancellationToken)
            ?? 0;


        var hasBandDraft =
            bandVersion > 0 &&
            await BandHistory()
                .AnyAsync(
                    x =>
                        x.BandVersion ==
                            bandVersion &&
                        x.DeleteFg == "N" &&
                        (
                            x.ApprovalStatus ==
                                "DRAFT" ||
                            x.ApprovalStatus ==
                                "PENDING"
                        ),
                    cancellationToken);


        var bands =
            await BandHistory()
                .AsNoTracking()
                .Where(item =>
                    item.DeleteFg == "N" &&
                    item.BandVersion ==
                        bandVersion &&
                    (
                        (
                            item.ApprovalStatus ==
                                "APPROVED" &&
                            item.IsActive
                        )
                        ||
                        item.ApprovalStatus ==
                            "PENDING"
                        ||
                        item.ApprovalStatus ==
                            "DRAFT"
                    ))
                .OrderBy(
                    item => item.MinimumScore)
                .ToListAsync(
                    cancellationToken);


        var bandMakers =
            bandVersion > 0
                ? await BandHistory()
                    .Where(x =>
                        x.BandVersion ==
                            bandVersion &&
                        x.DeleteFg == "N" &&
                        (
                            x.ApprovalStatus ==
                                "DRAFT" ||
                            x.ApprovalStatus ==
                                "PENDING"
                        ))
                    .Select(
                        x => x.MakerUserId)
                    .Distinct()
                    .ToListAsync(
                        cancellationToken)
                : new List<long?>();


        // =================================================
        // RISK MODEL
        // =================================================

        var now =
            DateTime.UtcNow;


        var activeModel =
            await ModelHistory()
                .AsNoTracking()
                .Where(item =>
                    item.DeleteFg == "N" &&
                    item.IsActive &&
                    item.ApprovalStatus ==
                        "APPROVED" &&
                    item.EffectiveFrom <= now &&
                    (
                        !item.EffectiveTo.HasValue ||
                        item.EffectiveTo >= now
                    ))
                .OrderByDescending(
                    item =>
                        item.ModelVersion)
                .FirstOrDefaultAsync(
                    cancellationToken);


        var pendingModels =
            await ModelHistory()
                .AsNoTracking()
                .Where(item =>
                    item.DeleteFg == "N" &&
                    (
                        item.ApprovalStatus ==
                            "PENDING"
                        ||
                        item.ApprovalStatus ==
                            "DRAFT"
                    ))
                .OrderByDescending(
                    item =>
                        item.ModelVersion)
                .Take(2)
                .ToListAsync(
                    cancellationToken);


        if (pendingModels.Count > 1)
        {
            throw new RiskConfigurationException(
                "More than one pending risk model exists. Resolve the model configuration before continuing.");
        }


        var pendingModel =
            pendingModels
                .FirstOrDefault();


        var hasModelDraft =
            pendingModel != null;


        // =================================================
        // PERMISSIONS
        // =================================================

        var canManage =
            _currentUser.HasPermission(
                AppPermissions.Risk.Manage);


        var canEditRules =
            canManage &&
            (
                !hasRuleDraft
                ||
                (
                    ruleMakers.Count == 1 &&
                    ruleMakers[0] ==
                        _currentUser.UserId
                )
            );


        var canEditBands =
            canManage &&
            (
                !hasBandDraft
                ||
                (
                    bandMakers.Count == 1 &&
                    bandMakers[0] ==
                        _currentUser.UserId
                )
            );


        var canEditModel =
            canManage &&
            (
                !hasModelDraft
                ||
                pendingModel?.MakerUserId ==
                    _currentUser.UserId
            );


        // =================================================
        // RETURN
        // =================================================

        return new RiskScoringSetupResult
        {
            CanEditRules =
                canEditRules,

            CanEditBands =
                canEditBands,

            CanEditModel =
                canEditModel,

            HasRuleDraft =
                hasRuleDraft,

            HasBandDraft =
                hasBandDraft,

            HasModelDraft =
                hasModelDraft,

            ActiveModel =
                activeModel,

            PendingModel =
                pendingModel,

            Factors =
                factors,

            SelectedFactor =
                selected,

            Rules =
                rules,

            Bands =
                bands,

            PageNumber =
                pageNumber,

            PageSize =
                pageSize,

            TotalCount =
                totalCount
        };
    }


    // =====================================================
    // GET RULE
    // =====================================================

    public Task<RiskFactorRule?> GetRuleAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigurationAccess(
            AppPermissions.Risk.View);


        return _db.RiskFactorRules
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.PartnerId ==
                        _currentUser.PartnerId,
                cancellationToken);
    }


    // =====================================================
    // GET BAND
    // =====================================================

    public Task<RiskBand?> GetBandAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigurationAccess(
            AppPermissions.Risk.View);


        return _db.RiskBands
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.PartnerId ==
                        _currentUser.PartnerId,
                cancellationToken);
    }


    // =====================================================
    // FACTOR VALUE RESOLUTION
    // =====================================================

    private static ResolvedFactorValue ResolveValue(
        string factorCode,
        RiskScoringInput input) =>
        factorCode
            .ToUpperInvariant()
            switch
        {
            "CUSTOMER_PEP" =>
                ResolvedFactorValue.Text(
                    input.IsPep
                        ? "YES"
                        : "NO"),

            "OCCUPATION" =>
                ResolvedFactorValue.Text(
                    input.Occupation),

            "SOURCE_OF_FUNDS" =>
                ResolvedFactorValue.Text(
                    input.SourceOfFunds),

            "AMOUNT" =>
                ResolvedFactorValue.Number(
                    input.Amount),

            "RECEIVING_COUNTRY" =>
                ResolvedFactorValue.Text(
                    input.ReceivingCountry),

            "PURPOSE" =>
                ResolvedFactorValue.Text(
                    input.PurposeCode),

            "RECEIVER_COUNT" =>
                ResolvedFactorValue.Number(
                    input.RegisteredReceiverCount),

            "RELATIONSHIP" =>
                ResolvedFactorValue.Text(
                    input.RelationshipCode),

            _ =>
                throw new InvalidOperationException(
                    $"Risk factor code '{factorCode}' is not supported by the scoring engine.")
        };


    // =====================================================
    // MATCH RULE
    // =====================================================

    private static RiskFactorRule? MatchRule(
        RiskFactor factor,
        ResolvedFactorValue value,
        IReadOnlyList<RiskFactorRule> rules)
    {
        if (factor.ValueType.Equals(
                "NUMBER",
                StringComparison.OrdinalIgnoreCase)
            ||
            factor.ValueType.Equals(
                "NUMERIC",
                StringComparison.OrdinalIgnoreCase))
        {
            var numericMatches =
                rules
                    .Where(rule =>
                        !string.Equals(
                            rule.ValueCode,
                            "DEFAULT",
                            StringComparison.OrdinalIgnoreCase)
                        &&
                        value.NumberValue.HasValue
                        &&
                        (
                            !rule.MinimumValue.HasValue ||
                            value.NumberValue >=
                                rule.MinimumValue
                        )
                        &&
                        (
                            !rule.MaximumValue.HasValue ||
                            value.NumberValue <=
                                rule.MaximumValue
                        ))
                    .ToList();


            if (numericMatches.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Numeric ranges overlap for risk factor '{factor.FactorName}'.");
            }


            return
                numericMatches.SingleOrDefault()
                ??
                DefaultRule(rules);
        }


        var inputCode =
            NormalizeCode(
                value.DisplayValue);


        var exactMatches =
            rules
                .Where(rule =>
                    !string.Equals(
                        rule.ValueCode,
                        "DEFAULT",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    NormalizeCode(
                        rule.ValueCode)
                    ==
                    inputCode)
                .ToList();


        if (exactMatches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Duplicate rules exist for risk factor '{factor.FactorName}' and value '{value.DisplayValue}'.");
        }


        return
            exactMatches.SingleOrDefault()
            ??
            DefaultRule(rules);
    }


    // =====================================================
    // DEFAULT RULE
    // =====================================================

    private static RiskFactorRule? DefaultRule(
        IEnumerable<RiskFactorRule> rules)
    {
        var defaults =
            rules
                .Where(rule =>
                    string.Equals(
                        rule.ValueCode,
                        "DEFAULT",
                        StringComparison.OrdinalIgnoreCase))
                .ToList();


        if (defaults.Count > 1)
        {
            throw new InvalidOperationException(
                "A risk factor has more than one DEFAULT rule.");
        }


        return defaults.SingleOrDefault();
    }


    // =====================================================
    // CATEGORY SCORE
    // =====================================================

    private static int CalculateCategoryScore(
        IEnumerable<RiskFactorResult> results,
        string category)
    {
        var categoryResults =
            results
                .Where(item =>
                    item.ScoreCategory.Equals(
                        category,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();


        if (categoryResults.Count == 0)
        {
            throw new InvalidOperationException(
                $"No {category} factors are configured.");
        }


        var totalWeight =
            categoryResults.Sum(
                item =>
                    item.WeightPercent);


        if (totalWeight != 100m)
        {
            throw new InvalidOperationException(
                $"{category} risk-factor weights must total 100 percent.");
        }


        return Math.Clamp(
            (int)Math.Round(
                categoryResults.Sum(
                    item =>
                        item.WeightedScore)
                *
                100m
                /
                totalWeight,

                MidpointRounding.AwayFromZero),

            0,
            100);
    }


    // =====================================================
    // VALIDATE BANDS
    // =====================================================

    private static void ValidateBands(
        IEnumerable<RiskBand> bands)
    {
        var nextScore = 0;


        foreach (
            var band in
            bands.OrderBy(
                item =>
                    item.MinimumScore))
        {
            var maximum =
                band.MaximumScore
                ??
                100;


            if (band.MinimumScore != nextScore ||
                maximum < band.MinimumScore ||
                maximum > 100 ||
                band.Decision
                    .ToUpperInvariant()
                    is not
                    ("ALLOW" or
                     "REVIEW" or
                     "BLOCK"))
            {
                throw new InvalidOperationException(
                    "Risk bands must cover every score from 0 to 100 exactly once, with valid decisions.");
            }


            nextScore =
                maximum + 1;
        }


        if (nextScore != 101)
        {
            throw new InvalidOperationException(
                "Risk bands must cover every score from 0 to 100.");
        }
    }


    // =====================================================
    // NORMALIZE CODE
    // =====================================================

    private static string NormalizeCode(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "UNKNOWN";
        }


        var builder =
            new StringBuilder(
                value.Length);


        var lastWasSeparator =
            false;


        foreach (
            var character in
            value
                .Trim()
                .ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);

                lastWasSeparator =
                    false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append('_');

                lastWasSeparator =
                    true;
            }
        }


        return builder
            .ToString()
            .Trim('_');
    }


    // =====================================================
    // AUDIT
    // =====================================================

    private void AddAudit(
        string action,
        string entityType,
        int entityId,
        string details) =>
        _db.AuditLogs.Add(
            new AuditLog
            {
                PartnerId =
                    _currentUser.PartnerId,

                UserId =
                    _currentUser.UserId,

                Username =
                    _currentUser.Username,

                Action =
                    action,

                EntityType =
                    entityType,

                EntityId =
                    entityId.ToString(),

                Details =
                    details,

                CreatedDate =
                    DateTime.UtcNow
            });


    private sealed record ResolvedFactorValue(
        string DisplayValue,
        decimal? NumberValue)
    {
        public static ResolvedFactorValue Text(
            string? value) =>
            new(
                string.IsNullOrWhiteSpace(value)
                    ? "UNKNOWN"
                    : value.Trim(),
                null);


        public static ResolvedFactorValue Number(
            decimal value) =>
            new(
                value.ToString(
                    "0.##",
                    System.Globalization
                        .CultureInfo
                        .InvariantCulture),
                value);
    }
}