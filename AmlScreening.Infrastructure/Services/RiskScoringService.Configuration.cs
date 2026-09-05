using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Common;
using AmlScreening.Domain.Entities.Compliance;
using Microsoft.EntityFrameworkCore;

namespace AmlScreening.Infrastructure.Services;

public sealed partial class RiskScoringService
{
    public Task SaveBandRangesAsync(IReadOnlyList<RiskBandInput> ranges, CancellationToken cancellationToken = default) =>
        ChangeConfigurationAsync(async () =>
        {
            var version = await BandHistory().MaxAsync(x => (int?)x.BandVersion, cancellationToken) ?? 0;
            var current = await BandHistory()
                .Where(x => x.BandVersion == version && x.DeleteFg == "N" && x.IsActive)
                .ToListAsync(cancellationToken);
            if (current.Count == 0 || ranges.Count != current.Count ||
                ranges.Select(x => x.Id).Distinct().Count() != current.Count ||
                current.Any(x => !ranges.Any(r => r.Id == x.Id)))
                throw new RiskConfigurationException("The range list changed. Reload it and try again.");

            var checkedRanges = new List<RiskBand>();
            foreach (var row in current)
            {
                var input = ranges.Single(x => x.Id == row.Id);
                CheckToken(row, input.RowVersion);
                if (input.MinimumScore is < 0 or > 100 || input.MaximumScore is < 0 or > 100 ||
                    input.MaximumScore < input.MinimumScore || input.Remarks?.Length > 1000)
                    throw new RiskConfigurationException("Enter ranges within 0–100 and remarks of at most 1,000 characters.");
                checkedRanges.Add(new RiskBand
                {
                    RiskLevel = row.RiskLevel, Decision = row.Decision,
                    MinimumScore = input.MinimumScore, MaximumScore = input.MaximumScore,
                    Remarks = input.Remarks?.Trim()
                });
            }
            if (current.Select(x => x.RiskLevel).Distinct(StringComparer.OrdinalIgnoreCase).Count() != current.Count)
                throw new RiskConfigurationException("Risk level names are duplicated. Ask your administrator to correct the configuration.");
            try { ValidateBands(checkedRanges); }
            catch (InvalidOperationException ex) { throw new RiskConfigurationException(ex.Message); }

            var draft = await BandDraftAsync(0, [], cancellationToken);
            foreach (var row in draft.Rows.Where(x => x.DeleteFg == "N"))
            {
                var input = checkedRanges.Single(x => x.RiskLevel == row.RiskLevel);
                row.MinimumScore = input.MinimumScore;
                row.MaximumScore = input.MaximumScore;
                row.Remarks = input.Remarks;
                Stamp(row);
            }
            AddAudit("Update", "RiskBand", 0, $"Saved all ranges to draft v{draft.Version}.");
        }, cancellationToken);

    // One SQL transaction saves both the change and its audit record.
    // The transaction-owned lock prevents two editors creating competing drafts.
    private async Task ChangeConfigurationAsync(Func<Task> change, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.ClientId <= 0 ||
            !_currentUser.HasPermission(AppPermissions.RiskScoringManage))
            throw new RiskConfigurationException("You do not have permission to manage risk configuration.");

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var resource = $"AML-RiskConfiguration-{_currentUser.ClientId}";
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource={resource},
                @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51000, 'Risk configuration is busy. Please retry.', 1;
            """, ct);
        try
        {
            await change();
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new RiskConfigurationException("This entry changed. Reload the list before editing again.");
        }
    }

    private IQueryable<RiskFactorRule> RuleHistory() => _db.RiskFactorRules
        .IgnoreQueryFilters(["SoftDeleteFilter"])
        .Where(x => x.ClientId == _currentUser.ClientId);

    private IQueryable<RiskBand> BandHistory() => _db.RiskBands
        .IgnoreQueryFilters(["SoftDeleteFilter"])
        .Where(x => x.ClientId == _currentUser.ClientId);

    private static void CheckToken(BaseEntity entity, byte[] token)
    {
        if (token.Length != 8 || !entity.RowVersion.SequenceEqual(token))
            throw new RiskConfigurationException("This entry changed. Reload the list before editing again.");
    }

    private void Stamp(BaseEntity entity)
    {
        entity.UpdatedBy = _currentUser.Username;
        entity.UpdatedDate = DateTime.UtcNow;
    }

    private void SoftDelete(BaseEntity entity)
    {
        entity.DeleteFg = "Y";
        entity.IsActive = false;
        entity.DeleteBy = _currentUser.Username;
        entity.DeleteDate = DateTime.UtcNow;
        Stamp(entity);
    }

    // Clone the whole factor, so deleting one rule never revives an older rule.
    private async Task<(List<RiskFactorRule> Rows, RiskFactorRule? Target, int Version)> RuleDraftAsync(
        int factorId, int id, byte[] token, CancellationToken ct)
    {
        var version = await RuleHistory().Where(x => x.RiskFactorId == factorId)
            .MaxAsync(x => (int?)x.RuleVersion, ct) ?? 0;
        var rows = await RuleHistory().Where(x => x.RiskFactorId == factorId && x.RuleVersion == version)
            .ToListAsync(ct);
        var target = rows.SingleOrDefault(x => x.Id == id && x.DeleteFg == "N");
        if (id != 0)
        {
            if (target is null) throw new RiskConfigurationException("This entry is no longer current. Reload the list.");
            CheckToken(target, token);
        }
        if (rows.Count > 0 && rows.All(x => x.ApprovalStatus == "DRAFT"))
            return (rows, target, version);

        var copies = new List<RiskFactorRule>();
        RiskFactorRule? copyTarget = null;
        foreach (var row in rows.Where(x => x.DeleteFg == "N" && x.IsActive))
        {
            var copy = new RiskFactorRule
            {
                ClientId = _currentUser.ClientId, RiskFactorId = factorId,
                ValueCode = row.ValueCode, ValueName = row.ValueName,
                MinimumValue = row.MinimumValue, MaximumValue = row.MaximumValue,
                Score = row.Score, Remarks = row.Remarks, RuleVersion = version + 1,
                CreatedBy = _currentUser.Username, ApprovalStatus = "DRAFT"
            };
            _db.RiskFactorRules.Add(copy);
            copies.Add(copy);
            if (row == target) copyTarget = copy;
        }
        return (copies, copyTarget, version + 1);
    }

    public Task SaveRuleAsync(RiskRuleInput input, CancellationToken cancellationToken = default) =>
        ChangeConfigurationAsync(async () =>
        {
            var factor = await _db.RiskFactors.SingleOrDefaultAsync(x =>
                x.ClientId == _currentUser.ClientId && x.IsActive && x.FactorCode == input.FactorCode, cancellationToken)
                ?? throw new RiskConfigurationException("Select an existing risk factor.");
            if (input.Id < 0 || input.Score is < 0 or > 100 || input.Remarks?.Length > 1000 || input.ValueName?.Length > 200)
                throw new RiskConfigurationException("Enter a score from 0 to 100 and keep the description within its limit.");
            var code = string.IsNullOrWhiteSpace(input.ValueCode) ? null : NormalizeCode(input.ValueCode);
            var numeric = factor.ValueType is "NUMBER" or "NUMERIC";
            if (code?.Length > 100 || (!numeric && string.IsNullOrEmpty(code)))
                throw new RiskConfigurationException("Enter a value code of at most 100 characters.");
            if (numeric && code != "DEFAULT")
            {
                if (!input.MinimumValue.HasValue || input.MinimumValue < 0 || input.MaximumValue < input.MinimumValue ||
                    input.MinimumValue > 9999999999999999.99m || input.MaximumValue > 9999999999999999.99m ||
                    decimal.Round(input.MinimumValue.Value, 2) != input.MinimumValue ||
                    (input.MaximumValue.HasValue && decimal.Round(input.MaximumValue.Value, 2) != input.MaximumValue))
                    throw new RiskConfigurationException("Enter a non-negative range with at most two decimal places. Leave maximum blank for no upper limit.");
                if (factor.FactorCode == "RECEIVER_COUNT" &&
                    (decimal.Truncate(input.MinimumValue.Value) != input.MinimumValue ||
                     (input.MaximumValue.HasValue && decimal.Truncate(input.MaximumValue.Value) != input.MaximumValue)))
                    throw new RiskConfigurationException("Receiver counts must be whole numbers.");
            }
            var draft = await RuleDraftAsync(factor.Id, input.Id, input.RowVersion, cancellationToken);
            if (code is not null && draft.Rows.Any(x => x != draft.Target && x.DeleteFg == "N" && NormalizeCode(x.ValueCode) == code))
                throw new RiskConfigurationException("This value code already exists in the draft.");
            var row = draft.Target ?? new RiskFactorRule
            {
                ClientId = _currentUser.ClientId, RiskFactorId = factor.Id,
                RuleVersion = draft.Version, CreatedBy = _currentUser.Username
            };
            if (draft.Target is null) _db.RiskFactorRules.Add(row);
            row.ValueCode = code;
            row.ValueName = input.ValueName?.Trim();
            row.MinimumValue = numeric && code != "DEFAULT" ? input.MinimumValue : null;
            row.MaximumValue = numeric && code != "DEFAULT" ? input.MaximumValue : null;
            row.Score = input.Score;
            row.Remarks = input.Remarks?.Trim();
            Stamp(row);
            await _db.SaveChangesAsync(cancellationToken);
            AddAudit(input.Id == 0 ? "Create" : "Update", "RiskFactorRule", row.Id,
                $"Draft {factor.FactorCode} v{draft.Version}: {code ?? "range"}, score {row.Score}.");
        }, cancellationToken);

    public Task DeleteRuleAsync(int id, byte[] rowVersion, CancellationToken cancellationToken = default) =>
        ChangeConfigurationAsync(async () =>
        {
            var original = await _db.RiskFactorRules.FirstOrDefaultAsync(x =>
                x.Id == id && x.ClientId == _currentUser.ClientId, cancellationToken)
                ?? throw new RiskConfigurationException("Risk score was not found.");
            var draft = await RuleDraftAsync(original.RiskFactorId, id, rowVersion, cancellationToken);
            SoftDelete(draft.Target!);
            AddAudit("Delete", "RiskFactorRule", id, $"Soft-deleted in draft v{draft.Version}; active configuration unchanged.");
        }, cancellationToken);

    public Task ActivateRulesAsync(string factorCode, CancellationToken cancellationToken = default) =>
        ChangeConfigurationAsync(async () =>
        {
            var factor = await _db.RiskFactors.SingleOrDefaultAsync(x => x.ClientId == _currentUser.ClientId &&
                x.FactorCode == factorCode && x.IsActive, cancellationToken)
                ?? throw new RiskConfigurationException("Risk factor was not found.");
            var history = await RuleHistory().Where(x => x.RiskFactorId == factor.Id).ToListAsync(cancellationToken);
            var version = history.Max(x => (int?)x.RuleVersion) ?? 0;
            var draft = history.Where(x => x.RuleVersion == version && x.ApprovalStatus == "DRAFT").ToList();
            var live = draft.Where(x => x.DeleteFg == "N").ToList();
            if (live.Count == 0) throw new RiskConfigurationException("Add at least one rule to the draft before activation.");
            ValidateRuleSet(factor, live);
            var now = DateTime.UtcNow;
            foreach (var row in history.Where(x => x.RuleVersion != version && x.IsActive))
            {
                row.IsActive = false;
                row.EffectiveTo = now;
                Stamp(row);
            }
            foreach (var row in draft)
            {
                row.ApprovalStatus = "APPROVED";
                row.ApprovedBy = _currentUser.Username;
                row.ApprovedDate = now;
                row.EffectiveFrom = now;
                row.EffectiveTo = null;
                row.IsActive = row.DeleteFg == "N";
                Stamp(row);
            }
            AddAudit("Activate", "RiskFactor", factor.Id, $"Activated {factorCode} rule version {version}, {live.Count} rules.");
        }, cancellationToken);

    private static void ValidateRuleSet(RiskFactor factor, List<RiskFactorRule> rows)
    {
        if (rows.Any(x => x.Score is < 0 or > 100))
            throw new RiskConfigurationException("All scores must be between 0 and 100.");
        if (rows.Where(x => !string.IsNullOrWhiteSpace(x.ValueCode)).GroupBy(x => NormalizeCode(x.ValueCode)).Any(x => x.Count() > 1))
            throw new RiskConfigurationException("Value codes must be unique.");
        var hasDefault = rows.Any(x => x.ValueCode == "DEFAULT");
        if (factor.ValueType is "NUMBER" or "NUMERIC")
        {
            var ranges = rows.Where(x => x.ValueCode != "DEFAULT").OrderBy(x => x.MinimumValue).ToList();
            decimal next = 0;
            var complete = true;
            var openEnd = false;
            var step = factor.FactorCode == "RECEIVER_COUNT" ? 1m : 0.01m;
            foreach (var row in ranges)
            {
                if (openEnd || row.MinimumValue is null || row.MinimumValue < next || row.MaximumValue < row.MinimumValue)
                    throw new RiskConfigurationException("Numeric ranges overlap or are invalid. Correct the draft before activation.");
                if (row.MinimumValue != next) complete = false;
                openEnd = row.MaximumValue is null;
                next = (row.MaximumValue ?? 0) + step;
            }
            if (!hasDefault && (!complete || !openEnd))
                throw new RiskConfigurationException("Cover all non-negative amounts/counts or add a DEFAULT rule for unmatched values.");
        }
        else if (!hasDefault && !(factor.FactorCode == "CUSTOMER_PEP" &&
                 rows.Any(x => x.ValueCode == "YES") && rows.Any(x => x.ValueCode == "NO")))
            throw new RiskConfigurationException("Add a DEFAULT rule to handle values not listed here.");
    }

    private async Task<(List<RiskBand> Rows, RiskBand? Target, int Version)> BandDraftAsync(
        int id, byte[] token, CancellationToken ct)
    {
        var version = await BandHistory().MaxAsync(x => (int?)x.BandVersion, ct) ?? 0;
        var rows = await BandHistory().Where(x => x.BandVersion == version).ToListAsync(ct);
        var target = rows.SingleOrDefault(x => x.Id == id && x.DeleteFg == "N");
        if (id != 0)
        {
            if (target is null) throw new RiskConfigurationException("This band is no longer current. Reload the list.");
            CheckToken(target, token);
        }
        if (rows.Count > 0 && rows.All(x => x.ApprovalStatus == "DRAFT")) return (rows, target, version);
        var copies = new List<RiskBand>();
        RiskBand? copyTarget = null;
        foreach (var row in rows.Where(x => x.DeleteFg == "N" && x.IsActive))
        {
            var copy = new RiskBand
            {
                ClientId = _currentUser.ClientId, RiskLevel = row.RiskLevel,
                MinimumScore = row.MinimumScore, MaximumScore = row.MaximumScore,
                Decision = row.Decision, Remarks = row.Remarks, BandVersion = version + 1,
                CreatedBy = _currentUser.Username, ApprovalStatus = "DRAFT"
            };
            _db.RiskBands.Add(copy);
            copies.Add(copy);
            if (row == target) copyTarget = copy;
        }
        return (copies, copyTarget, version + 1);
    }

    public Task SaveBandAsync(RiskBandInput input, CancellationToken cancellationToken = default) =>
        ChangeConfigurationAsync(async () =>
        {
            var level = input.RiskLevel.Trim().ToUpperInvariant();
            var decision = input.Decision.Trim().ToUpperInvariant();
            if (input.Id < 0 || string.IsNullOrEmpty(level) || level.Length > 20 || input.Remarks?.Length > 1000 ||
                input.MinimumScore is < 0 or > 100 || input.MaximumScore is < 0 or > 100 ||
                input.MaximumScore < input.MinimumScore || decision is not ("ALLOW" or "REVIEW" or "BLOCK"))
                throw new RiskConfigurationException("Enter a risk level, a range within 0–100, and ALLOW, REVIEW or BLOCK.");
            var draft = await BandDraftAsync(input.Id, input.RowVersion, cancellationToken);
            if (draft.Rows.Any(x => x != draft.Target && x.DeleteFg == "N" && x.RiskLevel.Equals(level, StringComparison.OrdinalIgnoreCase)))
                throw new RiskConfigurationException("This risk level already exists in the draft.");
            var row = draft.Target ?? new RiskBand
            {
                ClientId = _currentUser.ClientId, BandVersion = draft.Version, CreatedBy = _currentUser.Username
            };
            if (draft.Target is null) _db.RiskBands.Add(row);
            row.RiskLevel = level;
            row.MinimumScore = input.MinimumScore;
            row.MaximumScore = input.MaximumScore;
            row.Decision = decision;
            row.Remarks = input.Remarks?.Trim();
            Stamp(row);
            await _db.SaveChangesAsync(cancellationToken);
            AddAudit(input.Id == 0 ? "Create" : "Update", "RiskBand", row.Id,
                $"Draft v{draft.Version}: {level}, {row.MinimumScore}–{row.MaximumScore ?? 100}, {decision}.");
        }, cancellationToken);

    public Task DeleteBandAsync(int id, byte[] rowVersion, CancellationToken cancellationToken = default) =>
        ChangeConfigurationAsync(async () =>
        {
            var draft = await BandDraftAsync(id, rowVersion, cancellationToken);
            if (draft.Target is null) throw new RiskConfigurationException("Risk band was not found.");
            SoftDelete(draft.Target);
            AddAudit("Delete", "RiskBand", id, $"Soft-deleted in draft v{draft.Version}; active configuration unchanged.");
        }, cancellationToken);

    public Task ActivateBandsAsync(CancellationToken cancellationToken = default) =>
        ChangeConfigurationAsync(async () =>
        {
            var history = await BandHistory().ToListAsync(cancellationToken);
            var version = history.Max(x => (int?)x.BandVersion) ?? 0;
            var draft = history.Where(x => x.BandVersion == version && x.ApprovalStatus == "DRAFT").ToList();
            if (draft.Count == 0) throw new RiskConfigurationException("There are no draft bands to activate.");
            try { ValidateBands(draft.Where(x => x.DeleteFg == "N")); }
            catch (InvalidOperationException ex) { throw new RiskConfigurationException(ex.Message); }
            foreach (var row in history.Where(x => x.BandVersion != version && x.IsActive))
            {
                row.IsActive = false;
                Stamp(row);
            }
            foreach (var row in draft)
            {
                row.ApprovalStatus = "APPROVED";
                row.ApprovedBy = _currentUser.Username;
                row.ApprovedDate = DateTime.UtcNow;
                row.IsActive = row.DeleteFg == "N";
                Stamp(row);
            }
            AddAudit("Activate", "RiskBand", 0, $"Activated band version {version}.");
        }, cancellationToken);
}
