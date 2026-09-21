using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities.Compliance;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic;
using System.Security.Claims;

namespace AmlScreening.Portal.Controllers;

[Authorize(
    Policy = AppPermissions.Compliance.View)]
public sealed class ComplianceRulesController
    : Controller
{
    private readonly IComplianceRuleRepository
        _repository;


    public ComplianceRulesController(
        IComplianceRuleRepository repository)
    {
        _repository =
            repository;
    }


    // =========================================
    // INDEX
    // =========================================

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId =
            GetCurrentUserId();


        var rules =
            await _repository
                .GetAllAsync();


        var canEdit =
            User.HasClaim(
                AppPermissions.ClaimType,
                AppPermissions.Compliance.Manage);


        var canDelete =
            User.HasClaim(
                AppPermissions.ClaimType,
                AppPermissions.Compliance.Manage);


        var canApprove =
            User.HasClaim(
                AppPermissions.ClaimType,
                AppPermissions.Compliance.Approve);


        var model =
            rules.Select(
                rule =>
                    new ComplianceRuleViewModel
                    {
                        Id =
                            rule.Id,

                        PartnerId =
                            rule.PartnerId,

                        RuleCode =
                            rule.RuleCode,

                        RuleName =
                            rule.RuleName,

                        RuleType =
                            rule.RuleType,

                        ThresholdAmount =
                            rule.ThresholdAmount,

                        Currency =
                            rule.Currency,

                        LookbackMinutes =
                            rule.LookbackMinutes,

                        TransactionCount =
                            rule.TransactionCount,

                        RiskScore =
                            rule.RiskScore,

                        Action =
                            rule.Action,

                        RuleVersion =
                            rule.RuleVersion,

                        Priority =
                            rule.Priority,

                        ApprovalStatus =
                            rule.ApprovalStatus,

                        EffectiveFrom =
                            rule.EffectiveFrom,

                        EffectiveTo =
                            rule.EffectiveTo,

                        IsActive =
                            rule.IsActive,

                        MakerUserId =
                            rule.MakerUserId,

                        ApprovedByUserId =
                            rule.ApprovedByUserId,

                        ApprovedBy =
                            rule.ApprovedBy,

                        ApprovedDate =
                            rule.ApprovedDate,

                        RowVersion =
                            rule.RowVersion,


                        // Pending version:
                        // only maker can edit/delete.
                        CanEdit =
                            canEdit &&
                            rule.ApprovalStatus ==
                                "PENDING"
                                &&
                            rule.MakerUserId ==
                                userId,

                        CanDelete =
                            canDelete &&
                            rule.ApprovalStatus ==
                                "PENDING"
                                &&
                            rule.MakerUserId ==
                                userId,


                        // Checker:
                        // same user cannot approve.
                        CanApprove =
                            canApprove &&
                            rule.ApprovalStatus ==
                                "PENDING"
                                &&
                            rule.MakerUserId !=
                                userId
                    })
                .ToList();


        return View(model);
    }


    // =========================================
    // ADD
    // =========================================

    [HttpGet]
    [Authorize(
        Policy =
            AppPermissions.Compliance.Manage)]
    public IActionResult Add()
    {
        return View(
            new ComplianceRuleViewModel
            {
                RuleType =
                    "AmountThreshold",

                Currency =
                    "NPR",

                RiskScore =
                    30,

                Action =
                    "REVIEW",

                Priority =
                    100
            });
    }


    [HttpPost]
    [Authorize(
        Policy =
            AppPermissions.Compliance.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(
        ComplianceRuleViewModel model)
    {
        ValidateRule(model);


        if (!ModelState.IsValid)
        {
            return View(model);
        }


        var rule =
            new ComplianceRule
            {
                RuleCode =
                    model.RuleCode.Trim(),

                RuleName =
                    model.RuleName.Trim(),

                RuleType =
                    model.RuleType.Trim(),

                ThresholdAmount =
                    model.ThresholdAmount,

                Currency =
                    model.Currency?
                        .Trim()
                        .ToUpperInvariant(),

                LookbackMinutes =
                    model.LookbackMinutes,

                TransactionCount =
                    model.TransactionCount,

                RiskScore =
                    model.RiskScore,

                Action =
                    model.Action
                        .Trim()
                        .ToUpperInvariant(),

                Priority =
                    model.Priority
            };


        await _repository
            .CreatePendingAsync(
                rule,
                GetCurrentUserId(),
                GetCurrentUsername());


        TempData["SuccessMessage"] =
            "Compliance rule saved as Pending. A different authorized user from the same client must approve it.";


        return RedirectToAction(
            nameof(Index));
    }


    // =========================================
    // EDIT GET
    // =========================================

    [HttpGet]
    [Authorize(
        Policy =
            AppPermissions.Compliance.Manage)]
    public async Task<IActionResult> Edit(
        int id)
    {
        var rule =
            await _repository
                .GetByIdAsync(id);


        if (rule == null)
        {
            return NotFound();
        }


        return View(
            new ComplianceRuleViewModel
            {
                Id =
                    rule.Id,

                PartnerId =
                    rule.PartnerId,

                RowVersion =
                    rule.RowVersion,

                RuleCode =
                    rule.RuleCode,

                RuleName =
                    rule.RuleName,

                RuleType =
                    rule.RuleType,

                ThresholdAmount =
                    rule.ThresholdAmount,

                Currency =
                    rule.Currency,

                LookbackMinutes =
                    rule.LookbackMinutes,

                TransactionCount =
                    rule.TransactionCount,

                RiskScore =
                    rule.RiskScore,

                Action =
                    rule.Action,

                RuleVersion =
                    rule.RuleVersion,

                Priority =
                    rule.Priority,

                ApprovalStatus =
                    rule.ApprovalStatus,

                EffectiveFrom =
                    rule.EffectiveFrom,

                EffectiveTo =
                    rule.EffectiveTo,

                IsActive =
                    rule.IsActive,

                MakerUserId =
                    rule.MakerUserId
            });
    }


    // =========================================
    // EDIT POST
    // =========================================

    [HttpPost]
    [Authorize(
        Policy =
            AppPermissions.Compliance.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        ComplianceRuleViewModel model)
    {
        ValidateRule(model);


        if (!ModelState.IsValid)
        {
            return View(model);
        }


        var source =
            await _repository
                .GetByIdAsync(
                    model.Id);


        if (source == null)
        {
            return NotFound();
        }


        var userId =
            GetCurrentUserId();

        var username =
            GetCurrentUsername();


        var changedRule =
            new ComplianceRule
            {
                Id =
                    source.Id,

                RowVersion =
                    model.RowVersion,

                RuleCode =
                    source.RuleCode,

                RuleName =
                    model.RuleName.Trim(),

                RuleType =
                    model.RuleType.Trim(),

                ThresholdAmount =
                    model.ThresholdAmount,

                Currency =
                    model.Currency?
                        .Trim()
                        .ToUpperInvariant(),

                LookbackMinutes =
                    model.LookbackMinutes,

                TransactionCount =
                    model.TransactionCount,

                RiskScore =
                    model.RiskScore,

                Action =
                    model.Action
                        .Trim()
                        .ToUpperInvariant(),

                Priority =
                    model.Priority
            };


        if (source.ApprovalStatus ==
            "PENDING")
        {
            if (source.MakerUserId !=
                userId)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Only the original maker can edit this pending rule.");

                return View(model);
            }


            var updated =
                await _repository
                    .UpdatePendingAsync(
                        changedRule,
                        userId,
                        username);


            if (!updated)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "This rule changed after you opened it. Reload it and try again.");

                return View(model);
            }


            TempData["SuccessMessage"] =
                "Pending compliance rule updated successfully.";
        }
        else if (
            source.ApprovalStatus ==
            "APPROVED")
        {
            await _repository
                .CreatePendingVersionAsync(
                    source.Id,
                    changedRule,
                    userId,
                    username);


            TempData["SuccessMessage"] =
                "A new Pending version was created. The currently approved version remains active until the new version is approved.";
        }
        else
        {
            TempData["ErrorMessage"] =
                "This compliance rule cannot currently be edited.";
        }


        return RedirectToAction(
            nameof(Index));
    }


    // =========================================
    // REVIEW
    // =========================================

    [HttpGet]
    public async Task<IActionResult> Review(
        int id)
    {
        var pending =
            await _repository
                .GetPendingForReviewAsync(id);


        if (pending == null)
        {
            return NotFound();
        }


        var currentUserId =
            GetCurrentUserId();


        var active =
            await _repository
                .GetActiveVersionAsync(
                    pending.RuleCode);


        var hasApprovePermission =
            User.HasClaim(
                AppPermissions.ClaimType,
                AppPermissions.Compliance.Approve);


        var isMaker =
            pending.MakerUserId ==
            currentUserId;


        var model =
            new ComplianceRuleReviewViewModel
            {
                ActiveRule =
                    active,

                PendingRule =
                    pending,

                CanApprove =
                    hasApprovePermission &&
                    !isMaker,

                ApprovalMessage =
                    isMaker
                        ? "You created this rule. A different authorized user from the same client must approve it."
                        : !hasApprovePermission
                            ? "You do not have Compliance Rules Approve permission."
                            : null
            };


        return View(model);
    }


    // =========================================
    // APPROVE
    // =========================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy =
            AppPermissions.Compliance.Approve)]
    public async Task<IActionResult> Approve(
        int id,
        string rowVersion)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(
                    rowVersion))
            {
                TempData["ErrorMessage"] =
                    "Invalid rule version.";

                return RedirectToAction(
                    nameof(Index));
            }


            var bytes =
                Convert.FromBase64String(
                    rowVersion);


            var approved =
                await _repository
                    .ApproveAsync(
                        id,
                        GetCurrentUserId(),
                        GetCurrentUsername(),
                        bytes);


            TempData[
                approved
                    ? "SuccessMessage"
                    : "ErrorMessage"] =
                approved
                    ? "Compliance rule approved successfully."
                    : "Pending compliance rule was not found or changed before approval.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] =
                ex.Message;
        }


        return RedirectToAction(
            nameof(Index));
    }


    // =========================================
    // DELETE PENDING
    // =========================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.Compliance.Manage)]
    public async Task<IActionResult> Delete(
        int id, string rowVersion)
    {
        try
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] =
                    "Invalid compliance rule.";

                return RedirectToAction(
                    nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(
               rowVersion))
            {
                TempData["ErrorMessage"] =
                    "The rule version is missing. Reload the page and try again.";

                return RedirectToAction(
                    nameof(Index));
            }


            byte[] bytes;
            try
            {
                bytes =
                    Convert.FromBase64String(
                        rowVersion);
            }
            catch (FormatException)
            {
                TempData["ErrorMessage"] =
                    "Invalid compliance rule version.";

                return RedirectToAction(
                    nameof(Index));
            }
            var deleted =
                await _repository
                    .DeletePendingAsync(
                        id,
                        GetCurrentUserId(),
                        GetCurrentUsername(), bytes);


            TempData[
                deleted
                    ? "SuccessMessage"
                    : "ErrorMessage"] =
                deleted
                    ? "Pending compliance rule deleted successfully."
                    : "Only your own pending compliance rule can be deleted.";

        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] =
                ex.Message;
        }
        return RedirectToAction(
            nameof(Index));
    }


    // =========================================
    // VALIDATION
    // =========================================

    private void ValidateRule(
     ComplianceRuleViewModel model)
    {
        // ==================================
        // BASIC REQUIRED FIELDS
        // ==================================

        if (string.IsNullOrWhiteSpace(
                model.RuleCode))
        {
            ModelState.AddModelError(
                nameof(model.RuleCode),
                "Rule code is required.");
        }


        if (string.IsNullOrWhiteSpace(
                model.RuleName))
        {
            ModelState.AddModelError(
                nameof(model.RuleName),
                "Rule name is required.");
        }


        // ==================================
        // RULE TYPE
        // ==================================

        var ruleType =
            model.RuleType?
                .Trim();


        var allowedRuleTypes =
            new[]
            {
            "AmountThreshold",
            "Velocity"
            };


        if (string.IsNullOrWhiteSpace(
                ruleType)
            ||
            !allowedRuleTypes.Contains(
                ruleType,
                StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                nameof(model.RuleType),
                "Select a valid rule type.");
        }


        // ==================================
        // ACTION
        // ==================================

        var action =
            model.Action?
                .Trim()
                .ToUpperInvariant();
        model.Action = action ?? string.Empty;
        model.Currency = model.Currency?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(model.Currency) &&
            !System.Text.RegularExpressions.Regex.IsMatch(model.Currency, "^[A-Z]{3}$"))
            ModelState.AddModelError(nameof(model.Currency), "Use a three-letter currency code.");


        var allowedActions =
            new[]
            {
            "ALLOW",
            "REVIEW",
            "BLOCK"
            };


        if (string.IsNullOrWhiteSpace(
                action)
            ||
            !allowedActions.Contains(
                action,
                StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                nameof(model.Action),
                "Select a valid action.");
        }


        // ==================================
        // RISK SCORE
        // ==================================

        if (model.RiskScore < 0
            ||
            model.RiskScore > 100)
        {
            ModelState.AddModelError(
                nameof(model.RiskScore),
                "Risk score must be between 0 and 100.");
        }


        // ==================================
        // PRIORITY
        // ==================================

        if (model.Priority <= 0)
        {
            ModelState.AddModelError(
                nameof(model.Priority),
                "Priority must be greater than zero.");
        }


        // ==================================
        // AMOUNT THRESHOLD
        // ==================================

        if (string.Equals(
                ruleType,
                "AmountThreshold",
                StringComparison.OrdinalIgnoreCase))
        {
            if (!model.ThresholdAmount.HasValue
                ||
                model.ThresholdAmount.Value <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.ThresholdAmount),
                    "Threshold amount must be greater than zero.");
            }


            if (string.IsNullOrWhiteSpace(
                    model.Currency))
            {
                ModelState.AddModelError(
                    nameof(model.Currency),
                    "Currency is required for an amount threshold rule.");
            }
        }


        // ==================================
        // VELOCITY
        // ==================================

        if (string.Equals(
                ruleType,
                "Velocity",
                StringComparison.OrdinalIgnoreCase))
        {
            if (!model.LookbackMinutes.HasValue
                ||
                model.LookbackMinutes.Value <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.LookbackMinutes),
                    "Lookback minutes must be greater than zero.");
            }


            if (!model.TransactionCount.HasValue
                ||
                model.TransactionCount.Value <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.TransactionCount),
                    "Transaction count must be greater than zero.");
            }
        }
    }
    // =========================================
    // CURRENT USER
    // =========================================

    private long GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);


        if (!long.TryParse(
                value,
                out var userId))
        {
            throw new UnauthorizedAccessException(
                "Current user id is invalid.");
        }


        return userId;
    }


    private string GetCurrentUsername()
    {
        return User.FindFirstValue(
                   ClaimTypes.Name)
               ??
               User.Identity?.Name
               ??
               "Unknown";
    }
}
