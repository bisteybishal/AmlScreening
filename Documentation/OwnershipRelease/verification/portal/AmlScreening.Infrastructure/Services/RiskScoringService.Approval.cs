using System.Security.Cryptography;
using System.Text;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
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
        var snapshot = $"{_currentUser.ClientId}:{kind}:{factorCode}:" + string.Join("|",
            rows.OrderBy(x => x.Id).Select(x => $"{x.Id}:{Convert.ToBase64String(x.RowVersion)}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
    }

    private string? ApprovalBlock(IEnumerable<long?> makerIds)
    {
        var ids = makerIds.Distinct().ToList();
        if (ids.Count != 1 || ids[0] is null or <= 0)
            return "Maker information is missing or inconsistent. Legacy ownership must be verified before approval.";
        if (ids[0] == _currentUser.UserId)
            return "You made these changes. A different administrator must approve them.";
        if (!_currentUser.HasPermission(AppPermissions.RiskScoringApprove))
            return "You need RiskScoring.Approve permission to approve these changes.";
        return null;
    }

    private void EnsureApproval(IEnumerable<long?> makerIds, string suppliedToken, string currentToken)
    {
        var message = ApprovalBlock(makerIds);
        if (message != null) throw new RiskConfigurationException(message);
        if (string.IsNullOrEmpty(suppliedToken) || !string.Equals(suppliedToken, currentToken, StringComparison.Ordinal))
            throw new RiskConfigurationException("These changes were updated after you opened the review. Reload and review them again.");
    }

    public async Task<RiskApprovalReview> GetApprovalReviewAsync(string kind, string? factorCode, CancellationToken cancellationToken = default, int ruleId = 0)
    {
        RiskApprovalReview? review = null;
        // The same client lock used by writers gives the checker a consistent snapshot.
        await ChangeConfigurationAsync(async () =>
        {
            if (kind == "Rules")
            {
                var factor = await _db.RiskFactors.SingleOrDefaultAsync(x => x.ClientId == _currentUser.ClientId &&
                    x.IsActive && x.FactorCode == factorCode, cancellationToken)
                    ?? throw new RiskConfigurationException("Risk category was not found.");
                var history = await RuleHistory().AsNoTracking().Where(x => x.RiskFactorId == factor.Id).ToListAsync(cancellationToken);
                var row = history.SingleOrDefault(x => x.Id == ruleId && IsPending(x.ApprovalStatus))
                    ?? throw new RiskConfigurationException("Select a pending score to review.");
                var blocked = ApprovalBlock([row.MakerUserId]);
                review = new RiskApprovalReview
                {
                    Kind = kind, RuleId = row.Id, FactorCode = factor.FactorCode,
                    Title = $"Review {factor.FactorName} score",
                    MakerName = row.CreatedBy, CanApprove = blocked == null, ApprovalMessage = blocked,
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
                var blocked = ApprovalBlock(pending.Select(x => x.MakerUserId));
                review = new RiskApprovalReview
                {
                    Kind = kind, Title = "Review risk score ranges", MakerName = pending[0].CreatedBy,
                    CanApprove = blocked == null, ApprovalMessage = blocked, ReviewToken = ReviewToken(kind, null, pending),
                    ActiveRows = history.Where(x => x.ApprovalStatus == "APPROVED" && x.IsActive && x.DeleteFg == "N")
                        .OrderBy(x => x.MinimumScore).Select(BandReviewRow).ToList(),
                    PendingRows = pending.OrderBy(x => x.MinimumScore).Select(BandReviewRow).ToList()
                };
            }
            else throw new RiskConfigurationException("Unknown review type.");
        }, cancellationToken, AppPermissions.RiskScoringView);
        return review!;
    }

    private static RiskReviewRow RuleReviewRow(RiskFactorRule row) => new(
        $"{row.ValueCode ?? "Range"} — {row.ValueName}" +
        (row.MinimumValue.HasValue ? $" ({row.MinimumValue}–{row.MaximumValue?.ToString() ?? "No maximum"})" : ""),
        row.Score.ToString(), row.Remarks, row.DeleteFg != "N");

    private static RiskReviewRow BandReviewRow(RiskBand row) => new(
        row.RiskLevel, $"{row.MinimumScore}–{row.MaximumScore ?? 100} / {row.Decision}", row.Remarks, row.DeleteFg != "N");

    public Task ActivateRulesAsync(string factorCode, int ruleId, string reviewToken, CancellationToken cancellationToken = default) =>
        ChangeConfigurationAsync(async () =>
        {
            var factor = await _db.RiskFactors.SingleOrDefaultAsync(x => x.ClientId == _currentUser.ClientId &&
                x.FactorCode == factorCode && x.IsActive, cancellationToken)
                ?? throw new RiskConfigurationException("Risk category was not found.");
            var history = await RuleHistory().Where(x => x.RiskFactorId == factor.Id).ToListAsync(cancellationToken);
            var pending = history.SingleOrDefault(x => x.Id == ruleId && IsPending(x.ApprovalStatus))
                ?? throw new RiskConfigurationException("This score is not pending.");
            EnsureApproval([pending.MakerUserId], reviewToken, ReviewToken("Rules", factorCode, [pending]));

            var active = history.Where(x => x.ApprovalStatus == "APPROVED" && x.IsActive && x.DeleteFg == "N").ToList();
            RiskFactorRule? original = null;
            if (pending.ReplacesRuleId.HasValue)
            {
                original = active.SingleOrDefault(x => x.Id == pending.ReplacesRuleId)
                    ?? throw new RiskConfigurationException("The original score is no longer active. Reload and review the change.");
                active.Remove(original);
            }
            if (pending.DeleteFg == "N") active.Add(pending);
            // Validate the resulting configuration, but approve only this change.
            ValidateRuleSet(factor, active);
            var now = DateTime.UtcNow;
            if (original != null)
            {
                original.IsActive = false;
                original.EffectiveTo = now;
                Stamp(original);
            }
            pending.ApprovalStatus = "APPROVED";
            pending.ApprovedByUserId = _currentUser.UserId;
            pending.ApprovedBy = _currentUser.Username;
            pending.ApprovedDate = now;
            pending.EffectiveFrom = now;
            pending.EffectiveTo = null;
            pending.IsActive = pending.DeleteFg == "N";
            Stamp(pending);
            AddAudit("Approve", "RiskFactorRule", pending.Id,
                $"Approved score change; original {pending.ReplacesRuleId}; maker {pending.MakerUserId}, checker {_currentUser.UserId}.");
        }, cancellationToken, AppPermissions.RiskScoringApprove);

    public Task ActivateBandsAsync(string reviewToken, CancellationToken cancellationToken = default) =>
        ChangeConfigurationAsync(async () =>
        {
            var history = await BandHistory().ToListAsync(cancellationToken);
            var version = history.Max(x => (int?)x.BandVersion) ?? 0;
            var pending = history.Where(x => x.BandVersion == version).ToList();
            if (pending.Count == 0 || pending.Any(x => !IsPending(x.ApprovalStatus)))
                throw new RiskConfigurationException("There are no pending ranges to approve.");
            EnsureApproval(pending.Select(x => x.MakerUserId), reviewToken, ReviewToken("Bands", null, pending));
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
        }, cancellationToken, AppPermissions.RiskScoringApprove);
}
