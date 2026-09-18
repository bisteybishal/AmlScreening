using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Common;
using AmlScreening.Domain.Entities.Compliance;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace AmlScreening.Infrastructure.Services;

public sealed partial class RiskScoringService
{
    public Task SaveBandRangesAsync(IReadOnlyList<RiskBandInput> ranges, CancellationToken cancellationToken = default) =>
        ChangeConfigurationAsync(async () =>
        {
            var version = await BandHistory().MaxAsync(x => (int?)x.BandVersion, cancellationToken) ?? 0;
            if (version == 0)
            {
                if (ranges.Count == 0 || ranges.Any(x => x.Id != 0))
                    throw new RiskConfigurationException("Reload the initial range form.");
                var initial = ranges.Select(x => new RiskBand
                {
                    ClientId = _currentUser.ClientId,
                    BandVersion = 1,
                    RiskLevel = x.RiskLevel.Trim().ToUpperInvariant(),
                    MinimumScore = x.MinimumScore,
                    MaximumScore = x.MaximumScore,
                    Decision = x.Decision.Trim().ToUpperInvariant(),
                    Remarks = x.Remarks?.Trim(),
                    MakerUserId = _currentUser.UserId,
                    CreatedBy = _currentUser.Username,
                    ApprovalStatus = "PENDING"
                }).ToList();
                if (initial.Any(x => string.IsNullOrWhiteSpace(x.RiskLevel) || x.RiskLevel.Length > 20 || x.Remarks?.Length > 1000) ||
                    initial.Select(x => x.RiskLevel).Distinct().Count() != initial.Count)
                    throw new RiskConfigurationException("Enter unique risk levels and valid explanations.");
                try { ValidateBands(initial); }
                catch (InvalidOperationException ex) { throw new RiskConfigurationException(ex.Message); }
                _db.RiskBands.AddRange(initial);
                AddAudit("Create", "RiskBand", 0, "Created initial Pending risk ranges.");
                return;
            }
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
                    RiskLevel = row.RiskLevel,
                    Decision = row.Decision,
                    MinimumScore = input.MinimumScore,
                    MaximumScore = input.MaximumScore,
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
                row.ApprovalStatus = "PENDING";
                row.MinimumScore = input.MinimumScore;
                row.MaximumScore = input.MaximumScore;
                row.Remarks = input.Remarks;
                Stamp(row);
            }
            AddAudit("Update", "RiskBand", 0, $"Saved all ranges to draft v{draft.Version}.");
        }, cancellationToken);

    // One SQL transaction saves both the change and its audit record.
    // The transaction-owned lock prevents two editors creating competing drafts.
    private async Task ChangeConfigurationAsync(Func<Task> change, CancellationToken ct, string requiredPermission = AppPermissions.RiskScoringManage)
    {
        EnsureConfigurationAccess(requiredPermission);

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

    // This guard also protects callers that do not go through an MVC controller.
    private void EnsureConfigurationAccess(string permission)
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.ClientId <= 0 ||
            _currentUser.UserId <= 0 ||
            !_currentUser.HasPermission(permission))
            throw new RiskConfigurationException(
                "Only an authorized administrator for this client can access risk configuration.");
    }

    private IQueryable<RiskFactorRule> RuleHistory() => _db.RiskFactorRules
        .IgnoreQueryFilters(["SoftDeleteFilter"])
        .Where(x => x.ClientId == _currentUser.ClientId);

    private IQueryable<RiskBand> BandHistory() => _db.RiskBands
        .IgnoreQueryFilters(["SoftDeleteFilter"])
        .Where(x => x.ClientId == _currentUser.ClientId);

    private IQueryable<RiskModel> ModelHistory() => _db.RiskModels.IgnoreQueryFilters(["SoftDeleteFilter"])
        .Where(x => x.ClientId == _currentUser.ClientId);

    private static void CheckToken(BaseEntity entity, byte[] token)
    {
        if (token is not { Length: 8 } || !entity.RowVersion.SequenceEqual(token))
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

    // Only the selected record is copied for an edit/delete. Adding copies nothing.
    private async Task<(List<RiskFactorRule> Rows, RiskFactorRule? Target, int Version)> RuleDraftAsync(
        int factorId, int id, byte[] token, CancellationToken ct)
    {
        var rows = await RuleHistory().Where(x => x.RiskFactorId == factorId).ToListAsync(ct);
        var version = (rows.Max(x => (int?)x.RuleVersion) ?? 0) + 1;
        var current = rows.Where(x => (x.ApprovalStatus == "APPROVED" && x.IsActive && x.DeleteFg == "N")
            || IsPending(x.ApprovalStatus)).ToList();
        if (id == 0) return (current, null, version);

        var target = current.SingleOrDefault(x => x.Id == id)
            ?? throw new RiskConfigurationException("This score is no longer current. Reload the list.");
        CheckToken(target, token);
        if (IsPending(target.ApprovalStatus))
        {
            EnsureDraftMaker([target.MakerUserId]);
            if (target.DeleteFg != "N")
                throw new RiskConfigurationException("This score has a pending deletion.");
            return (current, target, target.RuleVersion);
        }
        if (current.Any(x => IsPending(x.ApprovalStatus) && x.ReplacesRuleId == id))
            throw new RiskConfigurationException("This score already has a pending change. Review it first.");

        var copy = new RiskFactorRule
        {
            ClientId = _currentUser.ClientId,
            RiskFactorId = factorId,
            ReplacesRuleId = target.Id,
            ValueCode = target.ValueCode,
            ValueName = target.ValueName,
            MinimumValue = target.MinimumValue,
            MaximumValue = target.MaximumValue,
            Score = target.Score,
            Remarks = target.Remarks,
            RuleVersion = version,
            CreatedBy = _currentUser.Username,
            MakerUserId = _currentUser.UserId,
            ApprovalStatus = "PENDING"
        };
        _db.RiskFactorRules.Add(copy);
        current.Remove(target);
        current.Add(copy);
        return (current, copy, version);
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
            if (code is not null && draft.Rows.Any(x => x != draft.Target && x.Id != draft.Target?.ReplacesRuleId && x.DeleteFg == "N" && NormalizeCode(x.ValueCode) == code))
                throw new RiskConfigurationException("This value code already exists in the draft.");
            var row = draft.Target ?? new RiskFactorRule
            {
                ClientId = _currentUser.ClientId,
                RiskFactorId = factor.Id,
                RuleVersion = draft.Version,
                CreatedBy = _currentUser.Username,
                MakerUserId = _currentUser.UserId,
                ApprovalStatus = "PENDING"
            };
            if (draft.Target is null) _db.RiskFactorRules.Add(row);
            row.ValueCode = code;
            row.ValueName = input.ValueName?.Trim();
            row.MinimumValue = numeric && code != "DEFAULT" ? input.MinimumValue : null;
            row.MaximumValue = numeric && code != "DEFAULT" ? input.MaximumValue : null;
            row.ApprovalStatus = "PENDING";
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
        if (rows.Count > 0 && rows.All(x => x.ApprovalStatus is "DRAFT" or "PENDING"))
        {
            EnsureDraftMaker(rows.Select(x => x.MakerUserId));
            return (rows, target, version);
        }
        var copies = new List<RiskBand>();
        RiskBand? copyTarget = null;
        foreach (var row in rows.Where(x => x.DeleteFg == "N" && x.IsActive))
        {
            var copy = new RiskBand
            {
                ClientId = _currentUser.ClientId,
                RiskLevel = row.RiskLevel,
                MinimumScore = row.MinimumScore,
                MaximumScore = row.MaximumScore,
                Decision = row.Decision,
                Remarks = row.Remarks,
                BandVersion = version + 1,
                CreatedBy = _currentUser.Username,
                ApprovalStatus = "PENDING",
                MakerUserId = _currentUser.UserId
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
                ClientId = _currentUser.ClientId,
                BandVersion = draft.Version,
                CreatedBy = _currentUser.Username,
                MakerUserId = _currentUser.UserId,
                ApprovalStatus = "PENDING"
            };
            if (draft.Target is null) _db.RiskBands.Add(row);
            row.ApprovalStatus = "PENDING";
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

    private static void ValidateModel(decimal customerWeight, decimal transactionWeight, decimal screeningWeight)
    {
        if (customerWeight < 0 || customerWeight > 100 ||
            transactionWeight < 0 || transactionWeight > 100 ||
            screeningWeight < 0 || screeningWeight > 100)
        {
            throw new RiskConfigurationException(
                "Every model weight must be between 0 and 100.");
        }
        var total=customerWeight+transactionWeight+screeningWeight;
        if (total != 100m)
        {
            throw new RiskConfigurationException(
           "Customer, Transaction and Screening weights must total exactly 100%.");
        }
        if(decimal.Round(customerWeight,2)!=customerWeight||
            decimal.Round(transactionWeight,2)!=transactionWeight||
           decimal.Round(screeningWeight,2)!=screeningWeight)
        {
            throw new RiskConfigurationException(
                "Risk model weights can contain at most two decimal places.");
        }
    }
    public Task SaveModelAsync(
    RiskModelInput input,
    CancellationToken cancellationToken = default) =>
    ChangeConfigurationAsync(
        async () =>
        {
            ValidateModel(
                input.CustomerWeightPercent,
                input.TransactionWeightPercent,
                input.ScreeningWeightPercent);

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
                    .OrderByDescending(
                        x => x.ModelVersion)
                    .ToList();

            if (pendingModels.Count > 1)
            {
                throw new RiskConfigurationException(
                    "More than one pending risk model exists. Resolve the configuration before editing.");
            }

            RiskModel model;

            if (pendingModels.Count == 1)
            {
                // Existing pending version:
                // only its original maker may continue editing it.
                model = pendingModels[0];

                EnsureDraftMaker(
                    [model.MakerUserId]);

                if (input.Id != model.Id)
                {
                    throw new RiskConfigurationException(
                        "A pending risk model already exists. Reload the page and edit that version.");
                }

                CheckToken(
                    model,
                    input.RowVersion);
            }
            else
            {
                // Editing an approved model creates a new version.
                if (input.Id > 0)
                {
                    var source =
                        history.SingleOrDefault(
                            x =>
                                x.Id == input.Id &&
                                x.DeleteFg == "N");

                    if (source == null)
                    {
                        throw new RiskConfigurationException(
                            "The risk model changed. Reload the page.");
                    }

                    CheckToken(
                        source,
                        input.RowVersion);

                    if (source.ApprovalStatus !=
                            "APPROVED")
                    {
                        throw new RiskConfigurationException(
                            "Only an approved model can be used to create a new model version.");
                    }
                }

                var nextVersion =
                    (history.Max(
                        x => (int?)x.ModelVersion)
                        ?? 0) + 1;

                model = new RiskModel
                {
                    ClientId =
                        _currentUser.ClientId,

                    ModelCode =
                        "AML-RISK",

                    ModelVersion =
                        nextVersion,

                    ApprovalStatus =
                        "PENDING",

                    MakerUserId =
                        _currentUser.UserId,

                    CreatedBy =
                        _currentUser.Username,

                    // Pending model is not active.
                    IsActive = false,

                    EffectiveFrom =
                        DateTime.UtcNow
                };

                _db.RiskModels.Add(model);
            }

            model.CustomerWeightPercent =
                input.CustomerWeightPercent;

            model.TransactionWeightPercent =
                input.TransactionWeightPercent;

            model.ScreeningWeightPercent =
                input.ScreeningWeightPercent;

            model.ApprovalStatus =
                "PENDING";

            model.ApprovedBy = null;
            model.ApprovedByUserId = null;
            model.ApprovedDate = null;

            model.IsActive = false;

            Stamp(model);

            await _db.SaveChangesAsync(
                cancellationToken);

            AddAudit(
                input.Id == 0
                    ? "Create"
                    : "Update",
                "RiskModel",
                model.Id,
                $"Saved pending risk model v{model.ModelVersion}: " +
                $"Customer={model.CustomerWeightPercent}%, " +
                $"Transaction={model.TransactionWeightPercent}%, " +
                $"Screening={model.ScreeningWeightPercent}%.");
        },
        cancellationToken);

}
