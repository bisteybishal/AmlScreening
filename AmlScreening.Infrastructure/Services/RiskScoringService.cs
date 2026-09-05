using System.Text;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Models;
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

    public async Task<RiskScoringResult> CalculateAsync(
        RiskScoringInput input,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.ClientId <= 0)
            throw new InvalidOperationException("A client must be selected before risk can be calculated.");
        if (!string.Equals(input.Currency, "NPR", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The current risk model supports NPR transactions only.");
        if (input.Amount <= 0 || decimal.Round(input.Amount, 2) != input.Amount)
            throw new ArgumentOutOfRangeException(nameof(input.Amount), "Amount must be positive with at most two decimal places.");
        if (input.RegisteredReceiverCount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(input.RegisteredReceiverCount),
                "Registered receiver count cannot be negative.");

        var now = DateTime.UtcNow;
        var model = await _db.RiskModels.AsNoTracking()
            .Where(item => item.ClientId == _currentUser.ClientId &&
                           item.IsActive &&
                           item.ApprovalStatus == "APPROVED" &&
                           item.EffectiveFrom <= now &&
                           (!item.EffectiveTo.HasValue || item.EffectiveTo >= now))
            .OrderByDescending(item => item.ModelVersion)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "No approved risk model is active for this client.");

        var modelWeight = model.CustomerWeightPercent +
                          model.TransactionWeightPercent +
                          model.ScreeningWeightPercent;
        if (model.CustomerWeightPercent < 0 || model.TransactionWeightPercent < 0 ||
            model.ScreeningWeightPercent < 0 || modelWeight != 100m)
            throw new InvalidOperationException(
                "Risk model weights must total 100 percent.");

        var factors = await _db.RiskFactors.AsNoTracking()
            .Where(item => item.ClientId == _currentUser.ClientId && item.IsActive)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);
        if (factors.Count == 0)
            throw new InvalidOperationException(
                "No active risk factors are configured for this client.");

        var factorIds = factors.Select(item => item.Id).ToArray();
        var allRules = await _db.RiskFactorRules.AsNoTracking()
            .Where(item => item.ClientId == _currentUser.ClientId &&
                           factorIds.Contains(item.RiskFactorId) &&
                           item.IsActive &&
                           item.ApprovalStatus == "APPROVED" &&
                           item.EffectiveFrom <= now &&
                           (!item.EffectiveTo.HasValue || item.EffectiveTo >= now))
            .ToListAsync(cancellationToken);

        var currentRules = allRules
            .GroupBy(item => item.RiskFactorId)
            .SelectMany(group =>
            {
                var latestVersion = group.Max(item => item.RuleVersion);
                return group.Where(item => item.RuleVersion == latestVersion);
            })
            .ToList();

        var factorResults = new List<RiskFactorResult>();
        foreach (var factor in factors)
        {
            var resolvedValue = ResolveValue(factor.FactorCode, input);
            var factorRules = currentRules
                .Where(item => item.RiskFactorId == factor.Id)
                .ToList();
            var matchedRule = MatchRule(factor, resolvedValue, factorRules)
                ?? throw new InvalidOperationException(
                    $"No approved rule matches factor '{factor.FactorName}' and value '{resolvedValue.DisplayValue}'.");

            if (matchedRule.Score is < 0 or > 100 || factor.WeightPercent <= 0 ||
                factor.WeightPercent > 100)
                throw new InvalidOperationException("A configured score or factor weight is invalid.");
            var rawScore = matchedRule.Score;
            var weightedScore = Math.Round(
                rawScore * factor.WeightPercent / 100m,
                2,
                MidpointRounding.AwayFromZero);

            factorResults.Add(new RiskFactorResult
            {
                FactorCode = factor.FactorCode,
                FactorName = factor.FactorName,
                ScoreCategory = factor.ScoreCategory.ToUpperInvariant(),
                InputValue = resolvedValue.DisplayValue,
                RawScore = rawScore,
                WeightPercent = factor.WeightPercent,
                WeightedScore = weightedScore,
                RuleVersion = matchedRule.RuleVersion,
                Explanation = string.IsNullOrWhiteSpace(matchedRule.Remarks)
                    ? $"{factor.FactorName}: {resolvedValue.DisplayValue} scored {rawScore}."
                    : matchedRule.Remarks
            });
        }

        var customerRisk = CalculateCategoryScore(factorResults, "CUSTOMER");
        var transactionRisk = Math.Max(
            CalculateCategoryScore(factorResults, "TRANSACTION"),
            Math.Clamp(input.AdditionalTransactionRiskScore, 0, 100));
        var screeningRisk = Math.Clamp(input.ScreeningRiskScore, 0, 100);
        var overallRisk = Math.Clamp((int)Math.Round(
            (customerRisk * model.CustomerWeightPercent +
             transactionRisk * model.TransactionWeightPercent +
             screeningRisk * model.ScreeningWeightPercent) / 100m,
            MidpointRounding.AwayFromZero), 0, 100);

        var bands = await _db.RiskBands.AsNoTracking()
            .Where(item => item.ClientId == _currentUser.ClientId &&
                           item.IsActive &&
                           item.ApprovalStatus == "APPROVED")
            .ToListAsync(cancellationToken);
        if (bands.Count == 0)
            throw new InvalidOperationException(
                "No approved risk bands are configured for this client.");

        var currentBandVersion = bands.Max(item => item.BandVersion);
        ValidateBands(bands.Where(item => item.BandVersion == currentBandVersion));
        var matchingBands = bands
            .Where(item => item.BandVersion == currentBandVersion &&
                           overallRisk >= item.MinimumScore &&
                           (!item.MaximumScore.HasValue || overallRisk <= item.MaximumScore.Value))
            .ToList();
        if (matchingBands.Count != 1)
            throw new InvalidOperationException(
                $"Risk-band configuration must contain exactly one band for score {overallRisk}.");

        var band = matchingBands[0];
        return new RiskScoringResult
        {
            CustomerRiskScore = customerRisk,
            TransactionRiskScore = transactionRisk,
            ScreeningRiskScore = screeningRisk,
            OverallRiskScore = overallRisk,
            RiskLevel = band.RiskLevel.ToUpperInvariant(),
            RecommendedDecision = band.Decision.ToUpperInvariant(),
            ModelVersion = $"{model.ModelCode}:v{model.ModelVersion}",
            Factors = factorResults
        };
    }

    public async Task<RiskScoringSetupResult> GetSetupAsync(
        string? factorCode,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        pageSize = new[] { 5, 10, 20, 50 }.Contains(pageSize) ? pageSize : 10;
        pageNumber = Math.Max(1, pageNumber);

        var factors = await _db.RiskFactors.AsNoTracking()
            .Where(item => item.ClientId == _currentUser.ClientId && item.IsActive)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);
        var selected = factors.FirstOrDefault(item =>
                           item.FactorCode == factorCode) ??
                       factors.FirstOrDefault();

        var selectedFactorId = selected?.Id ?? 0;
        var ruleVersion = await RuleHistory().Where(x => x.RiskFactorId == selectedFactorId)
            .MaxAsync(x => (int?)x.RuleVersion, cancellationToken) ?? 0;
        var hasRuleDraft = await RuleHistory().AnyAsync(x => x.RiskFactorId == selectedFactorId &&
            x.RuleVersion == ruleVersion && x.ApprovalStatus == "DRAFT", cancellationToken);
        var ruleQuery = _db.RiskFactorRules.AsNoTracking()
            .Where(item => item.ClientId == _currentUser.ClientId && item.IsActive && item.RuleVersion == ruleVersion);
        if (selected is not null)
            ruleQuery = ruleQuery.Where(item => item.RiskFactorId == selected.Id);
        else
            ruleQuery = ruleQuery.Where(_ => false);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            ruleQuery = ruleQuery.Where(item =>
                (item.ValueCode ?? string.Empty).Contains(term) ||
                (item.ValueName ?? string.Empty).Contains(term) ||
                (item.Remarks ?? string.Empty).Contains(term));
        }

        var totalCount = await ruleQuery.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        pageNumber = Math.Min(pageNumber, totalPages);
        var rules = await ruleQuery
            .OrderBy(item => item.MinimumValue)
            .ThenBy(item => item.ValueName)
            .ThenBy(item => item.ValueCode)
            .ThenBy(item => item.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var bandVersion = await BandHistory().MaxAsync(x => (int?)x.BandVersion, cancellationToken) ?? 0;
        var hasBandDraft = await BandHistory().AnyAsync(x => x.BandVersion == bandVersion && x.ApprovalStatus == "DRAFT", cancellationToken);
        var bands = await _db.RiskBands.AsNoTracking()
            .Where(item => item.ClientId == _currentUser.ClientId && item.IsActive && item.BandVersion == bandVersion)
            .OrderByDescending(item => item.BandVersion)
            .ThenBy(item => item.MinimumScore)
            .ToListAsync(cancellationToken);
        if (bands.Count > 0)
        {
            var latestVersion = bands.Max(item => item.BandVersion);
            bands = bands.Where(item => item.BandVersion == latestVersion).ToList();
        }

        var activeModel = await _db.RiskModels.AsNoTracking()
            .Where(item => item.ClientId == _currentUser.ClientId && item.IsActive)
            .OrderByDescending(item => item.ModelVersion)
            .FirstOrDefaultAsync(cancellationToken);

        return new RiskScoringSetupResult
        {
            HasRuleDraft = hasRuleDraft,
            HasBandDraft = hasBandDraft,
            ActiveModel = activeModel,
            Factors = factors,
            SelectedFactor = selected,
            Rules = rules,
            Bands = bands,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public Task<RiskFactorRule?> GetRuleAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.RiskFactorRules.AsNoTracking().FirstOrDefaultAsync(
            item => item.Id == id && item.ClientId == _currentUser.ClientId,
            cancellationToken);

    public Task<RiskBand?> GetBandAsync(int id, CancellationToken cancellationToken = default) =>
        _db.RiskBands.AsNoTracking().FirstOrDefaultAsync(
            item => item.Id == id && item.ClientId == _currentUser.ClientId, cancellationToken);

    private static ResolvedFactorValue ResolveValue(
        string factorCode,
        RiskScoringInput input) => factorCode.ToUpperInvariant() switch
        {
            "CUSTOMER_PEP" => ResolvedFactorValue.Text(input.IsPep ? "YES" : "NO"),
            "OCCUPATION" => ResolvedFactorValue.Text(input.Occupation),
            "SOURCE_OF_FUNDS" => ResolvedFactorValue.Text(input.SourceOfFunds),
            "AMOUNT" => ResolvedFactorValue.Number(input.Amount),
            "RECEIVING_COUNTRY" => ResolvedFactorValue.Text(input.ReceivingCountry),
            "PURPOSE" => ResolvedFactorValue.Text(input.PurposeCode),
            "RECEIVER_COUNT" => ResolvedFactorValue.Number(input.RegisteredReceiverCount),
            "RELATIONSHIP" => ResolvedFactorValue.Text(input.RelationshipCode),
            _ => throw new InvalidOperationException(
                $"Risk factor code '{factorCode}' is not supported by the scoring engine.")
        };

    private static RiskFactorRule? MatchRule(
        RiskFactor factor,
        ResolvedFactorValue value,
        IReadOnlyList<RiskFactorRule> rules)
    {
        if (factor.ValueType.Equals("NUMBER", StringComparison.OrdinalIgnoreCase) ||
            factor.ValueType.Equals("NUMERIC", StringComparison.OrdinalIgnoreCase))
        {
            var numericMatches = rules.Where(rule =>
                    !string.Equals(rule.ValueCode, "DEFAULT", StringComparison.OrdinalIgnoreCase) &&
                    value.NumberValue.HasValue &&
                    (!rule.MinimumValue.HasValue || value.NumberValue >= rule.MinimumValue) &&
                    (!rule.MaximumValue.HasValue || value.NumberValue <= rule.MaximumValue))
                .ToList();
            if (numericMatches.Count > 1)
                throw new InvalidOperationException(
                    $"Numeric ranges overlap for risk factor '{factor.FactorName}'.");
            return numericMatches.SingleOrDefault() ?? DefaultRule(rules);
        }

        var inputCode = NormalizeCode(value.DisplayValue);
        var exactMatches = rules.Where(rule =>
                !string.Equals(rule.ValueCode, "DEFAULT", StringComparison.OrdinalIgnoreCase) &&
                NormalizeCode(rule.ValueCode) == inputCode)
            .ToList();
        if (exactMatches.Count > 1)
            throw new InvalidOperationException(
                $"Duplicate rules exist for risk factor '{factor.FactorName}' and value '{value.DisplayValue}'.");
        return exactMatches.SingleOrDefault() ?? DefaultRule(rules);
    }

    private static RiskFactorRule? DefaultRule(IEnumerable<RiskFactorRule> rules)
    {
        var defaults = rules.Where(rule =>
                string.Equals(rule.ValueCode, "DEFAULT", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (defaults.Count > 1)
            throw new InvalidOperationException("A risk factor has more than one DEFAULT rule.");
        return defaults.SingleOrDefault();
    }

    private static int CalculateCategoryScore(
        IEnumerable<RiskFactorResult> results,
        string category)
    {
        var categoryResults = results
            .Where(item => item.ScoreCategory.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (categoryResults.Count == 0)
            throw new InvalidOperationException($"No {category} factors are configured.");

        var totalWeight = categoryResults.Sum(item => item.WeightPercent);
        if (totalWeight != 100m)
            throw new InvalidOperationException(
                $"{category} risk-factor weights must total 100 percent.");

        return Math.Clamp((int)Math.Round(
            categoryResults.Sum(item => item.WeightedScore) * 100m / totalWeight,
            MidpointRounding.AwayFromZero), 0, 100);
    }

    private static void ValidateBands(IEnumerable<RiskBand> bands)
    {
        var nextScore = 0;
        foreach (var band in bands.OrderBy(item => item.MinimumScore))
        {
            var maximum = band.MaximumScore ?? 100;
            if (band.MinimumScore != nextScore || maximum < band.MinimumScore ||
                maximum > 100 || band.Decision.ToUpperInvariant() is not ("ALLOW" or "REVIEW" or "BLOCK"))
                throw new InvalidOperationException(
                    "Risk bands must cover every score from 0 to 100 exactly once, with valid decisions.");
            nextScore = maximum + 1;
        }
        if (nextScore != 101)
            throw new InvalidOperationException("Risk bands must cover every score from 0 to 100.");
    }

    private static string NormalizeCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "UNKNOWN";
        var builder = new StringBuilder(value.Length);
        var lastWasSeparator = false;
        foreach (var character in value.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append('_');
                lastWasSeparator = true;
            }
        }
        return builder.ToString().Trim('_');
    }

    private void AddAudit(string action, string entityType, int entityId, string details) =>
        _db.AuditLogs.Add(new AuditLog
        {
            ClientId = _currentUser.ClientId,
            UserId = _currentUser.UserId,
            Username = _currentUser.Username,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            Details = details,
            CreatedDate = DateTime.UtcNow
        });

    private sealed record ResolvedFactorValue(string DisplayValue, decimal? NumberValue)
    {
        public static ResolvedFactorValue Text(string? value) =>
            new(string.IsNullOrWhiteSpace(value) ? "UNKNOWN" : value.Trim(), null);

        public static ResolvedFactorValue Number(decimal value) =>
            new(value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), value);
    }
}
