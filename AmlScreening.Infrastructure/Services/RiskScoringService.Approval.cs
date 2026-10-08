using System.Security.Cryptography;
using System.Text;
using AmlScreening.Application.Models;
using AmlScreening.Domain.Common;
using AmlScreening.Domain.Entities.Compliance;
using Microsoft.EntityFrameworkCore;

namespace AmlScreening.Infrastructure.Services;

public sealed partial class RiskScoringService
{
    private static bool IsPending(string status) => status is "PENDING" or "DRAFT";

    // One maker owns the entire pending category/range set, including removals.
    private void EnsureDraftMaker(IEnumerable<long?> makerIds)
    {
        var ids = makerIds.Distinct().ToList();
        if (ids.Count != 1 || ids[0] is null or <= 0)
            throw new RiskConfigurationException("The pending maker is not verified. Ask your administrator to review the legacy draft ownership.");
        if (ids[0] != _currentUser.UserId)
            throw new RiskConfigurationException("Only the maker can change this pending set. Another administrator must review it.");
    }

    private string ReviewToken(string kind, string? factorCode, IEnumerable<BaseEntity> rows)
    {
        var snapshot = $"{_currentUser.PartnerId}:{kind}:{factorCode}:" + string.Join("|",
            rows.OrderBy(x => x.Id).Select(x => $"{x.Id}:{Convert.ToBase64String(x.RowVersion)}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
    }

    private string? ApprovalBlock(IEnumerable<long?> makerIds, string requiredPermission)
    {
        var ids = makerIds.Distinct().ToList();
        if (ids.Count != 1 || ids[0] is null or <= 0)
            return "Maker information is missing or inconsistent. Legacy ownership must be verified before approval.";
        if (ids[0] == _currentUser.UserId)
            return "You made these changes. A different administrator must approve them.";
        if (!_currentUser.HasPermission(requiredPermission))
            return $"You need {requiredPermission} permission to approve these changes.";
        return null;
    }

    private void EnsureApproval(IEnumerable<long?> makerIds, string suppliedToken, string currentToken, string requiredPermission)
    {
        var message = ApprovalBlock(makerIds, requiredPermission);
        if (message != null) throw new RiskConfigurationException(message);
        if (string.IsNullOrEmpty(suppliedToken) || !string.Equals(suppliedToken, currentToken, StringComparison.Ordinal))
            throw new RiskConfigurationException("These changes were updated after you opened the review. Reload and review them again.");
    }

    public async Task<RiskApprovalReview> GetApprovalReviewAsync(string kind, string? factorCode, CancellationToken cancellationToken = default, int ruleId = 0)
    {
        var viewPermission = kind switch
        {
            "Rules" => AppPermissions.RiskScore.View,
            "Bands" or "Model" => AppPermissions.RiskScoreSetup.View,
            _ => throw new RiskConfigurationException("Unknown review type.")
        };
        var approvalPermission = kind == "Rules"
            ? AppPermissions.RiskScore.Approve
            : AppPermissions.RiskScoreSetup.Approve;
        factorCode = factorCode?.Trim().ToUpperInvariant();
        RiskApprovalReview? review = null;
        // The same client lock used by writers gives the checker a consistent snapshot.
        await ChangeConfigurationAsync(async () =>
        {
            if (kind == "Rules")
            {
                var factor = await _db.RiskFactors.SingleOrDefaultAsync(x => x.PartnerId == _currentUser.PartnerId &&
                    x.IsActive && x.FactorCode == factorCode, cancellationToken)
                    ?? throw new RiskConfigurationException("Risk category was not found.");
                var history = await RuleHistory().AsNoTracking().Where(x => x.RiskFactorId == factor.Id).ToListAsync(cancellationToken);
                var row = history.SingleOrDefault(x => x.Id == ruleId && IsPending(x.ApprovalStatus))
                    ?? throw new RiskConfigurationException("Select a pending score to review.");
                var blocked = ApprovalBlock([row.MakerUserId], approvalPermission);
                review = new RiskApprovalReview
                {
                    Kind = kind,
                    RuleId = row.Id,
                    FactorCode = factor.FactorCode,
                    Title = $"Review {factor.FactorName} score",
                    MakerName = row.CreatedBy,
                    CanApprove = blocked == null,
                    ApprovalMessage = blocked,
                    ReviewToken = ReviewToken(kind, factor.FactorCode, [row]),
                    ActiveRows = history.Where(x => x.Id == row.ReplacesRuleId)
                        .Select(RuleReviewRow).ToList(),
                    PendingRows = [RuleReviewRow(row)]
                };
            }
            else if (kind == "Bands")
            {
                var history = await BandHistory().AsNoTracking().ToListAsync(cancellationToken);
                var version = history.Max(x => (int?)x.BandVersion) ?? 0;
                var pending = history.Where(x => x.BandVersion == version).ToList();
                if (pending.Count == 0 || pending.Any(x => !IsPending(x.ApprovalStatus)))
                    throw new RiskConfigurationException("There are no pending ranges to review.");
                var blocked = ApprovalBlock(pending.Select(x => x.MakerUserId), approvalPermission);
                review = new RiskApprovalReview
                {
                    Kind = kind,
                    Title = "Review risk score ranges",
                    MakerName = pending[0].CreatedBy,
                    CanApprove = blocked == null,
                    ApprovalMessage = blocked,
                    ReviewToken = ReviewToken(kind, null, pending),
                    ActiveRows = history.Where(x => x.ApprovalStatus == "APPROVED" && x.IsActive && x.DeleteFg == "N")
                        .OrderBy(x => x.MinimumScore).Select(BandReviewRow).ToList(),
                    PendingRows = pending.OrderBy(x => x.MinimumScore).Select(BandReviewRow).ToList()
                };
            }
            else if (kind == "Model")
            {
                var history =
                    await ModelHistory()
                        .AsNoTracking()
                        .OrderByDescending(
                            x => x.ModelVersion)
                        .ToListAsync(
                            cancellationToken);

                var pending =
                    history.FirstOrDefault(
                        x =>
                            IsPending(
                                x.ApprovalStatus) &&
                            x.DeleteFg == "N")
                    ?? throw new RiskConfigurationException(
                        "There is no pending risk model to review.");

                var active =
                    history.FirstOrDefault(
                        x =>
                            x.ApprovalStatus ==
                                "APPROVED" &&
                            x.IsActive &&
                            x.DeleteFg == "N");

                var blocked =
                    ApprovalBlock(
                        [pending.MakerUserId], approvalPermission);

                review =
                    new RiskApprovalReview
                    {
                        Kind = "Model",

                        Title =
                            $"Review risk model v{pending.ModelVersion}",

                        MakerName =
                            pending.CreatedBy,

                        CanApprove =
                            blocked == null,

                        ApprovalMessage =
                            blocked,

                        ReviewToken =
                            ReviewToken(
                                "Model",
                                null,
                                [pending]),

                        ActiveRows =
                            active == null
                                ? []
                                : ModelReviewRows(
                                    active),

                        PendingRows =
                            ModelReviewRows(
                                pending)
                    };
            }
            else
            {
                throw new RiskConfigurationException(
                    "Unknown review type.");
            }

        }, cancellationToken, viewPermission);
        return review!;
    }

    private static RiskReviewRow RuleReviewRow(RiskFactorRule row) => new(
        $"{row.ValueCode ?? "Range"} — {row.ValueName}" +
        (row.MinimumValue.HasValue ? $" ({row.MinimumValue}–{row.MaximumValue?.ToString() ?? "No maximum"})" : ""),
        row.Score.ToString(), row.Remarks, row.DeleteFg != "N");

    private static RiskReviewRow BandReviewRow(RiskBand row) => new(
        row.RiskLevel, $"{row.MinimumScore}–{row.MaximumScore?.ToString() ?? "-"} / {row.Decision}", row.Remarks, row.DeleteFg != "N");

    public Task ActivateRulesAsync(
      string factorCode,
      int ruleId,
      string reviewToken,
      CancellationToken cancellationToken = default)
    {
        return ChangeConfigurationAsync(
            async () =>
            {
                // 1. Find the factor for current partner.
                var factor =
                    await GetRiskFactorAsync(
                        factorCode,
                        cancellationToken);


                // 2. Get complete history for this factor.
                var history =
                    await RuleHistory()
                        .Where(x =>
                            x.RiskFactorId == factor.Id)
                        .ToListAsync(
                            cancellationToken);


                // 3. Find requested pending score.
                var pending =
                    history.FirstOrDefault(x =>
                        x.Id == ruleId &&
                        IsPending(
                            x.ApprovalStatus));

                if (pending == null)
                {
                    throw new RiskConfigurationException(
                        "This score is not pending.");
                }


                // 4. Maker cannot approve own change.
                // Also verifies review token.
                EnsureApproval(
                    [pending.MakerUserId],
                    reviewToken,
                    ReviewToken(
                        "Rules",
                        factor.FactorCode,
                        [pending]),
                    AppPermissions.RiskScore.Approve);


                // 5. Get currently active approved scores.
                var activeRules =
                    history
                        .Where(x =>
                            x.ApprovalStatus == "APPROVED" &&
                            x.IsActive &&
                            x.DeleteFg == "N")
                        .ToList();


                // 6. Find old score if this is an update/delete.
                RiskFactorRule? oldRule =
                    null;

                if (pending.ReplacesRuleId.HasValue)
                {
                    oldRule =
                        activeRules.FirstOrDefault(x =>
                            x.Id ==
                            pending.ReplacesRuleId.Value);

                    if (oldRule == null)
                    {
                        throw new RiskConfigurationException(
                            "The original score is no longer active. " +
                            "Reload and review the change.");
                    }


                    // Remove old rule from temporary
                    // configuration before validation.
                    activeRules.Remove(
                        oldRule);
                }


                // 7. Add new rule unless this is deletion.
                if (pending.DeleteFg == "N")
                {
                    activeRules.Add(
                        pending);
                }


                // 8. Validate final configuration.
                //
                // DEFAULT is optional based on
                // your latest requirement.
                ValidateRuleSet(
                    factor,
                    activeRules);


                var now =
                    DateTime.UtcNow;


                // 9. Deactivate old approved score.
                if (oldRule != null)
                {
                    oldRule.IsActive =
                        false;

                    oldRule.EffectiveTo =
                        now;

                    Stamp(
                        oldRule);
                }


                // 10. Approve pending score.
                pending.ApprovalStatus =
                    "APPROVED";

                pending.ApprovedByUserId =
                    _currentUser.UserId;

                pending.ApprovedBy =
                    _currentUser.Username;

                pending.ApprovedDate =
                    now;

                pending.EffectiveFrom =
                    now;

                pending.EffectiveTo =
                    null;

                pending.IsActive =
                    pending.DeleteFg == "N";

                Stamp(
                    pending);


                // 11. Audit.
                AddAudit(
                    "Approve",
                    "RiskFactorRule",
                    pending.Id,
                    $"Approved score change; " +
                    $"original {pending.ReplacesRuleId}; " +
                    $"maker {pending.MakerUserId}; " +
                    $"checker {_currentUser.UserId}.");
            },
            cancellationToken,
            AppPermissions.RiskScore.Approve);
    }

    public Task ActivateBandsAsync(string reviewToken, CancellationToken cancellationToken = default) =>
        ChangeConfigurationAsync(async () =>
        {
            var history = await BandHistory().ToListAsync(cancellationToken);
            var version = history.Max(x => (int?)x.BandVersion) ?? 0;
            var pending = history.Where(x => x.BandVersion == version).ToList();
            if (pending.Count == 0 || pending.Any(x => !IsPending(x.ApprovalStatus)))
                throw new RiskConfigurationException("There are no pending ranges to approve.");
            EnsureApproval(pending.Select(x => x.MakerUserId), reviewToken, ReviewToken("Bands", null, pending), AppPermissions.RiskScoreSetup.Approve);
            try { ValidateBands(pending.Where(x => x.DeleteFg == "N")); }
            catch (InvalidOperationException ex) { throw new RiskConfigurationException(ex.Message); }
            foreach (var row in history.Where(x => x.BandVersion != version && x.IsActive))
            {
                row.IsActive = false;
                Stamp(row);
            }
            foreach (var row in pending)
            {
                row.ApprovalStatus = "APPROVED";
                row.ApprovedByUserId = _currentUser.UserId;
                row.ApprovedBy = _currentUser.Username;
                row.ApprovedDate = DateTime.UtcNow;
                row.IsActive = row.DeleteFg == "N";
                Stamp(row);
            }
            AddAudit("Approve", "RiskBand", 0,
                $"Approved ranges v{version}; maker {pending[0].MakerUserId}, checker {_currentUser.UserId}.");
        }, cancellationToken, AppPermissions.RiskScoreSetup.Approve);

    public Task ActivateModelAsync(
    string reviewToken,
    CancellationToken cancellationToken = default) =>
    ChangeConfigurationAsync(
        async () =>
        {
            var history =
                await ModelHistory()
                    .ToListAsync(
                        cancellationToken);

            var pendingModels =
                history
                    .Where(x =>
                        IsPending(
                            x.ApprovalStatus) &&
                        x.DeleteFg == "N")
                    .ToList();

            if (pendingModels.Count != 1)
            {
                throw new RiskConfigurationException(
                    "Exactly one pending risk model is required for approval.");
            }

            var pending =
                pendingModels[0];

            EnsureApproval(
                [pending.MakerUserId],
                reviewToken,
                ReviewToken(
                    "Model",
                    null,
                    [pending]), AppPermissions.RiskScoreSetup.Approve);

            ValidateModel(
                pending.CustomerWeightPercent,
                pending.TransactionWeightPercent,
                pending.ScreeningWeightPercent);

            var now =
                DateTime.UtcNow;

            // Retire existing approved model.
            foreach (var active in
                history.Where(x =>
                    x.Id != pending.Id &&
                    x.ApprovalStatus ==
                        "APPROVED" &&
                    x.IsActive &&
                    x.DeleteFg == "N"))
            {
                active.IsActive = false;

                active.EffectiveTo =
                    now;

                Stamp(active);
            }

            // Publish pending model.
            pending.ApprovalStatus =
                "APPROVED";

            pending.ApprovedByUserId =
                _currentUser.UserId;

            pending.ApprovedBy =
                _currentUser.Username;

            pending.ApprovedDate =
                now;

            pending.EffectiveFrom =
                now;

            pending.EffectiveTo =
                null;

            pending.IsActive =
                true;

            Stamp(pending);

            AddAudit(
                "Approve",
                "RiskModel",
                pending.Id,
                $"Approved risk model v{pending.ModelVersion}; " +
                $"maker {pending.MakerUserId}, " +
                $"checker {_currentUser.UserId}.");
        },
        cancellationToken,
        AppPermissions.RiskScoreSetup.Approve);

    private static IReadOnlyList<RiskReviewRow>
    ModelReviewRows(RiskModel model) =>
[
    new(
        "Customer risk weight",
        $"{model.CustomerWeightPercent:0.##}%",
        $"Model v{model.ModelVersion}",
        false),

    new(
        "Transaction risk weight",
        $"{model.TransactionWeightPercent:0.##}%",
        $"Model v{model.ModelVersion}",
        false),

    new(
        "Screening risk weight",
        $"{model.ScreeningWeightPercent:0.##}%",
        $"Model v{model.ModelVersion}",
        false)
];

    private async Task<RiskFactor>
        GetRiskFactorAsync(
            string factorCode,
            CancellationToken cancellationToken)
    {
        var factor =
            await _db.RiskFactors
                .SingleOrDefaultAsync(
                    x =>
                        x.PartnerId ==
                            _currentUser.PartnerId
                        &&
                        x.FactorCode ==
                            factorCode
                        &&
                        x.IsActive,
                    cancellationToken);

        if (factor == null)
        {
            throw new RiskConfigurationException(
                "Risk category was not found.");
        }

        return factor;
    }
}