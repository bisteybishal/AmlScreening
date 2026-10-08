using AmlScreening.Application.Models;
using AmlScreening.Domain.Common;
using AmlScreening.Domain.Entities.Compliance;
using Microsoft.EntityFrameworkCore;

namespace AmlScreening.Infrastructure.Services;

public sealed partial class RiskScoringService
{
    public Task SaveBandRangesAsync(IReadOnlyList<RiskBandInput> ranges, CancellationToken cancellationToken = default) =>
        ChangeConfigurationAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(ranges);
            if (ranges.Count == 0 || ranges.Any(x => x is null))
                throw new RiskConfigurationException("At least one valid risk range is required.");

            var version = await BandHistory().MaxAsync(x => (int?)x.BandVersion, cancellationToken) ?? 0;
            if (version == 0)
            {
                if (ranges.Count == 0 || ranges.Any(x => x.Id != 0))
                    throw new RiskConfigurationException("Reload the initial range form.");
                var initial = ranges.Select(x => new RiskBand
                {
                    PartnerId = _currentUser.PartnerId,
                    BandVersion = 1,
                    RiskLevel = (x.RiskLevel ?? string.Empty).Trim().ToUpperInvariant(),
                    MinimumScore = x.MinimumScore,
                    MaximumScore = x.MaximumScore,
                    Decision = (x.Decision ?? string.Empty).Trim().ToUpperInvariant(),
                    Remarks = x.Remarks?.Trim(),
                    MakerUserId = _currentUser.UserId,
                    CreatedBy = _currentUser.Username,
                    ApprovalStatus = "PENDING",
                    IsActive = false
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
                .Where(x => x.BandVersion == version && x.DeleteFg == "N" &&
                    (x.ApprovalStatus == "PENDING" || x.ApprovalStatus == "DRAFT" ||
                     (x.ApprovalStatus == "APPROVED" && x.IsActive)))
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
        }, cancellationToken, AppPermissions.RiskScoreSetup.Manage);

    // One SQL transaction saves both the change and its audit record.
    // The transaction-owned lock prevents two editors creating competing drafts.
    private async Task ChangeConfigurationAsync(Func<Task> change, CancellationToken ct, string requiredPermission = AppPermissions.RiskScore.Manage)
    {
        EnsureConfigurationAccess(requiredPermission);

        var previouslyTracked = _db.ChangeTracker.Entries()
            .Select(x => x.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var resource = $"AML-RiskConfiguration-{_currentUser.PartnerId}";
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
        catch (Exception exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            // A rollback does not reset EF's change tracker. Discard this
            // service's failed configuration changes before the context is reused.
            foreach (var entry in _db.ChangeTracker.Entries().ToList())
            {
                if (entry.Entity is RiskFactorRule or RiskBand or RiskModel ||
                    (entry.Entity is AmlScreening.Domain.Entities.AuditLog &&
                     !previouslyTracked.Contains(entry.Entity)))
                    entry.State = EntityState.Detached;
            }
            if (exception is DbUpdateConcurrencyException)
                throw new RiskConfigurationException(
                    "This entry changed. Reload the list before editing again.");
            throw;
        }
    }

    // This guard also protects callers that do not go through an MVC controller.
    private void EnsureConfigurationAccess(string permission)
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.PartnerId <= 0 ||
            _currentUser.UserId <= 0 ||
            !_currentUser.HasPermission(permission))
            throw new RiskConfigurationException(
                "Only an authorized administrator for this client can access risk configuration.");
    }

    private IQueryable<RiskFactorRule> RuleHistory() => _db.RiskFactorRules
        .IgnoreQueryFilters(["SoftDeleteFilter"])
        .Where(x => x.PartnerId == _currentUser.PartnerId);

    private IQueryable<RiskBand> BandHistory() => _db.RiskBands
        .IgnoreQueryFilters(["SoftDeleteFilter"])
        .Where(x => x.PartnerId == _currentUser.PartnerId);

    private IQueryable<RiskModel> ModelHistory() => _db.RiskModels.IgnoreQueryFilters(["SoftDeleteFilter"])
        .Where(x => x.PartnerId == _currentUser.PartnerId);

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
            PartnerId = _currentUser.PartnerId,
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
            ApprovalStatus = "PENDING",
            IsActive = false
        };
        _db.RiskFactorRules.Add(copy);
        current.Remove(target);
        current.Add(copy);
        return (current, copy, version);
    }

    public Task SaveRuleAsync(RiskRuleInput input, CancellationToken cancellationToken = default) =>
        ChangeConfigurationAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(input);
            input.FactorCode = (input.FactorCode ?? string.Empty).Trim().ToUpperInvariant();
            var factor = await _db.RiskFactors.SingleOrDefaultAsync(x =>
                x.PartnerId == _currentUser.PartnerId && x.IsActive && x.FactorCode == input.FactorCode, cancellationToken)
                ?? throw new RiskConfigurationException("Select an existing risk factor.");
            if (input.Id < 0 || input.Score is < 0 or > 100 || input.Remarks?.Length > 1000 || input.ValueName?.Length > 200)
                throw new RiskConfigurationException("Enter a score from 0 to 100 and keep the description within its limit.");
            var code = string.IsNullOrWhiteSpace(input.ValueCode) ? null : NormalizeCode(input.ValueCode);
            var numeric = factor.ValueType.ToUpperInvariant() is "NUMBER" or "NUMERIC";
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
                PartnerId = _currentUser.PartnerId,
                RiskFactorId = factor.Id,
                RuleVersion = draft.Version,
                CreatedBy = _currentUser.Username,
                MakerUserId = _currentUser.UserId,
                ApprovalStatus = "PENDING",
                IsActive = false
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
                x.Id == id && x.PartnerId == _currentUser.PartnerId, cancellationToken)
                ?? throw new RiskConfigurationException("Risk score was not found.");
            var draft = await RuleDraftAsync(original.RiskFactorId, id, rowVersion, cancellationToken);
            SoftDelete(draft.Target!);
            AddAudit("Delete", "RiskFactorRule", id, $"Soft-deleted in draft v{draft.Version}; active configuration unchanged.");
        }, cancellationToken);

    private static void ValidateRuleSet(
    RiskFactor factor,
    List<RiskFactorRule> rows)
    {
        if (rows.Count == 0)
        {
            throw new RiskConfigurationException(
                "At least one risk score must be configured.");
        }


        // Score validation.
        if (rows.Any(x =>
            x.Score < 0 ||
            x.Score > 100))
        {
            throw new RiskConfigurationException(
                "All scores must be between 0 and 100.");
        }


        // Duplicate ValueCode validation.
        var duplicateCode =
            rows
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x.ValueCode))
                .GroupBy(x =>
                    NormalizeCode(
                        x.ValueCode))
                .Any(x =>
                    x.Count() > 1);


        if (duplicateCode)
        {
            throw new RiskConfigurationException(
                "Value codes must be unique.");
        }


        var isNumeric =
            factor.ValueType.Equals(
                "NUMBER",
                StringComparison.OrdinalIgnoreCase)
            ||
            factor.ValueType.Equals(
                "NUMERIC",
                StringComparison.OrdinalIgnoreCase);


        // ============================
        // NUMERIC FACTOR
        // ============================
        if (isNumeric)
        {
            var ranges =
                rows
                    .Where(x =>
                        !string.Equals(
                            x.ValueCode,
                            "DEFAULT",
                            StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x =>
                        x.MinimumValue)
                    .ToList();


            decimal? previousMaximum =
                null;


            foreach (var row in ranges)
            {
                if (!row.MinimumValue.HasValue)
                {
                    throw new RiskConfigurationException(
                        "Enter a minimum value for every numeric range.");
                }


                if (row.MinimumValue.Value < 0)
                {
                    throw new RiskConfigurationException(
                        "Minimum value cannot be negative.");
                }


                if (row.MaximumValue.HasValue &&
                    row.MaximumValue.Value <
                    row.MinimumValue.Value)
                {
                    throw new RiskConfigurationException(
                        "Maximum value cannot be less than minimum value.");
                }


                if (previousMaximum.HasValue &&
                    row.MinimumValue.Value <=
                    previousMaximum.Value)
                {
                    throw new RiskConfigurationException(
                        "Numeric ranges must not overlap.");
                }


                previousMaximum =
                    row.MaximumValue;
            }


            // DEFAULT is optional.
            return;
        }


        // ============================
        // PEP
        // ============================
        if (string.Equals(
            factor.FactorCode,
            "CUSTOMER_PEP",
            StringComparison.OrdinalIgnoreCase))
        {
            var hasYes =
                rows.Any(x =>
                    NormalizeCode(
                        x.ValueCode)
                    == "YES");


            var hasNo =
                rows.Any(x =>
                    NormalizeCode(
                        x.ValueCode)
                    == "NO");


            if (!hasYes || !hasNo)
            {
                throw new RiskConfigurationException(
                    "PEP scoring must contain both YES and NO.");
            }
        }


        // ============================
        // TEXT FACTORS
        // ============================
        //
        // OCCUPATION
        // SOURCE_OF_FUNDS
        // PURPOSE
        // RECEIVING_COUNTRY
        // RELATIONSHIP
        //
        // DEFAULT IS OPTIONAL.
    }

    private async Task<(
      List<RiskBand> Rows,
      RiskBand? Target,
      int Version)>
      BandDraftAsync(
          int id,
          byte[] token,
          CancellationToken cancellationToken)
    {
        // 1. Get latest band version.
        var version =
            await BandHistory()
                .MaxAsync(
                    x => (int?)x.BandVersion,
                    cancellationToken)
            ?? 0;


        // 2. Get bands from latest version.
        var rows =
            await BandHistory()
                .Where(x =>
                    x.BandVersion == version)
                .ToListAsync(
                    cancellationToken);


        // 3. Find requested band when editing.
        var target =
            id > 0
                ? rows.FirstOrDefault(x =>
                    x.Id == id &&
                    x.DeleteFg == "N")
                : null;


        if (id > 0)
        {
            if (target == null)
            {
                throw new RiskConfigurationException(
                    "This band is no longer current. Reload the list.");
            }

            CheckToken(
                target,
                token);
        }


        // 4. If latest version is already pending,
        // continue editing the same pending version.
        var hasPendingDraft =
            rows.Count > 0 &&
            rows.All(x =>
                IsPending(
                    x.ApprovalStatus));


        if (hasPendingDraft)
        {
            EnsureDraftMaker(
                rows.Select(x =>
                    x.MakerUserId));

            return (
                rows,
                target,
                version);
        }


        // 5. Otherwise create next pending version
        // from currently active approved bands.
        var newVersion =
            version + 1;

        var copies =
            new List<RiskBand>();

        RiskBand? copyTarget =
            null;


        foreach (var current in rows.Where(x =>
                     x.DeleteFg == "N" &&
                     x.IsActive))
        {
            var copy =
                CreateBandCopy(
                    current,
                    newVersion);

            _db.RiskBands.Add(
                copy);

            copies.Add(
                copy);


            if (current.Id == id)
            {
                copyTarget =
                    copy;
            }
        }


        return (
            copies,
            copyTarget,
            newVersion);
    }

    public async Task SaveBandAsync(
     RiskBandInput input,
     CancellationToken cancellationToken = default)
    {
        await ChangeConfigurationAsync(
            async () =>
            {
                var level =
                    (input.RiskLevel ?? string.Empty)
                        .Trim()
                        .ToUpperInvariant();

                var decision =
                    (input.Decision ?? string.Empty)
                        .Trim()
                        .ToUpperInvariant();


                // 1. Validate input.
                ValidateBandInput(
                    input,
                    level,
                    decision);


                // 2. Get/create pending version.
                var draft =
                    await BandDraftAsync(
                        input.Id,
                        input.RowVersion,
                        cancellationToken);


                // 3. Prevent duplicate risk level.
                var duplicate =
                    draft.Rows.Any(x =>
                        x != draft.Target &&
                        x.DeleteFg == "N" &&
                        x.RiskLevel.Equals(
                            level,
                            StringComparison.OrdinalIgnoreCase));


                if (duplicate)
                {
                    throw new RiskConfigurationException(
                        "This risk level already exists in the draft.");
                }


                // 4. Update existing pending band
                // or create new pending band.
                var row =
                    draft.Target
                    ?? new RiskBand
                    {
                        PartnerId =
                            _currentUser.PartnerId,

                        BandVersion =
                            draft.Version,

                        CreatedBy =
                            _currentUser.Username,

                        MakerUserId =
                            _currentUser.UserId,

                        ApprovalStatus =
                            "PENDING",

                        IsActive =
                            false
                    };


                if (draft.Target == null)
                {
                    _db.RiskBands.Add(
                        row);
                }


                // 5. Set values.
                row.RiskLevel =
                    level;

                row.MinimumScore =
                    input.MinimumScore;

                row.MaximumScore =
                    input.MaximumScore;

                row.Decision =
                    decision;

                row.Remarks =
                    input.Remarks?.Trim();

                row.ApprovalStatus =
                    "PENDING";

                row.IsActive =
                    false;


                Stamp(
                    row);


                // 6. Save.
                await _db.SaveChangesAsync(
                    cancellationToken);


                // 7. Audit.
                AddAudit(
                    input.Id == 0
                        ? "Create"
                        : "Update",
                    "RiskBand",
                    row.Id,
                    $"Draft v{draft.Version}: " +
                    $"{level}, " +
                    $"{row.MinimumScore}–{row.MaximumScore}, " +
                    $"{decision}.");
            },
            cancellationToken,
            AppPermissions.RiskScoreSetup.Manage);
    }

    public Task DeleteBandAsync(int id, byte[] rowVersion, CancellationToken cancellationToken = default) =>
        ChangeConfigurationAsync(async () =>
        {
            var draft = await BandDraftAsync(id, rowVersion, cancellationToken);
            if (draft.Target is null) throw new RiskConfigurationException("Risk band was not found.");
            SoftDelete(draft.Target);
            AddAudit("Delete", "RiskBand", id, $"Soft-deleted in draft v{draft.Version}; active configuration unchanged.");
        }, cancellationToken, AppPermissions.RiskScoreSetup.Manage);

    private static void ValidateModel(decimal customerWeight, decimal transactionWeight, decimal screeningWeight)
    {
        if (customerWeight < 0 || customerWeight > 100 ||
            transactionWeight < 0 || transactionWeight > 100 ||
            screeningWeight < 0 || screeningWeight > 100)
        {
            throw new RiskConfigurationException(
                "Every model weight must be between 0 and 100.");
        }
        var total = customerWeight + transactionWeight + screeningWeight;
        if (total != 100m)
        {
            throw new RiskConfigurationException(
           "Customer, Transaction and Screening weights must total exactly 100%.");
        }
        if (decimal.Round(customerWeight, 2) != customerWeight ||
            decimal.Round(transactionWeight, 2) != transactionWeight ||
           decimal.Round(screeningWeight, 2) != screeningWeight)
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
            ArgumentNullException.ThrowIfNull(input);
            if (input.Id < 0)
                throw new RiskConfigurationException("Invalid risk model.");

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
                if (input.Id == 0 && history.Any(x => x.DeleteFg == "N"))
                    throw new RiskConfigurationException(
                        "A risk model already exists. Reload and edit its current version.");

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

                    if (!source.IsActive || source.ApprovalStatus !=
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
                    PartnerId =
                        _currentUser.PartnerId,

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
        cancellationToken,
        AppPermissions.RiskScoreSetup.Manage);

    private RiskBand CreateBandCopy(
    RiskBand source,
    int version)
    {
        return new RiskBand
        {
            PartnerId =
                _currentUser.PartnerId,

            RiskLevel =
                source.RiskLevel,

            MinimumScore =
                source.MinimumScore,

            MaximumScore =
                source.MaximumScore,

            Decision =
                source.Decision,

            Remarks =
                source.Remarks,

            BandVersion =
                version,

            CreatedBy =
                _currentUser.Username,

            MakerUserId =
                _currentUser.UserId,

            ApprovalStatus =
                "PENDING",

            IsActive =
                false
        };
    }
    private static void ValidateBandInput(
    RiskBandInput input,
    string level,
    string decision)
    {
        if (input.Id < 0)
        {
            throw new RiskConfigurationException(
                "Invalid risk band.");
        }


        if (string.IsNullOrWhiteSpace(level) ||
            level.Length > 20)
        {
            throw new RiskConfigurationException(
                "Enter a valid risk level.");
        }


        if (input.Remarks?.Length > 1000)
        {
            throw new RiskConfigurationException(
                "Remarks cannot exceed 1000 characters.");
        }


        if (input.MinimumScore < 0 ||
            input.MinimumScore > 100)
        {
            throw new RiskConfigurationException(
                "Minimum score must be between 0 and 100.");
        }


        if (input.MaximumScore.HasValue &&
            (input.MaximumScore < 0 ||
             input.MaximumScore > 100))
        {
            throw new RiskConfigurationException(
                "Maximum score must be between 0 and 100.");
        }


        if (input.MaximumScore.HasValue &&
            input.MaximumScore <
            input.MinimumScore)
        {
            throw new RiskConfigurationException(
                "Maximum score cannot be less than minimum score.");
        }


        if (decision is not
            ("ALLOW" or "REVIEW" or "BLOCK"))
        {
            throw new RiskConfigurationException(
                "Decision must be ALLOW, REVIEW or BLOCK.");
        }
    }

}
