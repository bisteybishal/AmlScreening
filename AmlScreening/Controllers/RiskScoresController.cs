using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities.Compliance;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.RiskScore.View)]
public sealed class RiskScoresController : Controller
{
    private const int PageSize = 10;

    private const string ReviewView =
        "~/Views/Shared/RiskApprovalReview.cshtml";

    private readonly IRiskScoringService _riskScoring;
    private readonly IErrorLogService _errorLogService;

    public RiskScoresController(
        IRiskScoringService riskScoring,
        IErrorLogService errorLogService)
    {
        _riskScoring = riskScoring;
        _errorLogService = errorLogService;
    }

    // =====================================================
    // LIST RULES
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> Index(
        string? factorCode,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        factorCode = NormalizeFactorCode(factorCode);

        try
        {
            var result = await _riskScoring.GetSetupAsync(
                factorCode,
                null,
                Math.Max(1, page),
                PageSize,
                cancellationToken);

            // Do not silently display another factor when an
            // explicitly requested factor does not exist.
            if (!string.IsNullOrEmpty(factorCode) &&
                !result.Factors.Any(factor =>
                    string.Equals(
                        factor.FactorCode,
                        factorCode,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return NotFound();
            }

            return View(
                "Index",
                RiskScoringPageViewModel.From(result));
        }
        catch (RiskConfigurationException ex)
        {
            return IndexError(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var message = await GetErrorMessageAsync(
                ex,
                nameof(Index),
                "Unable to load risk scores.");

            return IndexError(message);
        }
    }

    // =====================================================
    // ADD RULE — GET
    // =====================================================

    [HttpGet]
    [Authorize(Policy = AppPermissions.RiskScore.Manage)]
    public async Task<IActionResult> Add(
        string? factorCode,
        CancellationToken cancellationToken = default)
    {
        factorCode = NormalizeFactorCode(factorCode);

        if (string.IsNullOrEmpty(factorCode))
            return BadRequest("Select a risk factor.");

        try
        {
            var factor = await FindFactorAsync(
                factorCode,
                cancellationToken);

            if (factor is null)
                return NotFound();

            return View(
                "Form",
                new RiskRuleEditViewModel
                {
                    FactorCode = factor.FactorCode,
                    ValueType = factor.ValueType.ToUpperInvariant(),
                    ValueDescription = factor.FactorName
                });
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["ErrorMessage"] = await GetErrorMessageAsync(
                ex,
                nameof(Add),
                "Unable to open the risk score form.");
        }

        return RedirectToAction(nameof(Index));
    }

    // =====================================================
    // ADD RULE — POST
    // =====================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.RiskScore.Manage)]
    public Task<IActionResult> Add(
        RiskRuleEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        return SaveRuleAsync(
            model,
            isNew: true,
            cancellationToken: cancellationToken);
    }

    // =====================================================
    // EDIT RULE — GET
    // =====================================================

    [HttpGet]
    [Authorize(Policy = AppPermissions.RiskScore.Manage)]
    public async Task<IActionResult> Edit(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return NotFound();

        try
        {
            // The service enforces the current partner.
            var rule = await _riskScoring.GetRuleAsync(
                id,
                cancellationToken);

            if (rule is null)
                return NotFound();

            var blocked = GetEditBlockReason(rule);

            if (blocked is not null)
            {
                TempData["ErrorMessage"] = blocked;
                return RedirectToAction(nameof(Index));
            }

            var setup = await _riskScoring.GetSetupAsync(
                null,
                null,
                1,
                PageSize,
                cancellationToken);

            var factor = setup.Factors.FirstOrDefault(
                item => item.Id == rule.RiskFactorId);

            if (factor is null)
                return NotFound();

            return View(
                "Form",
                new RiskRuleEditViewModel
                {
                    Id = rule.Id,
                    FactorCode = factor.FactorCode,
                    ValueType = factor.ValueType.ToUpperInvariant(),
                    ValueDescription = factor.FactorName,
                    ValueCode = rule.ValueCode,
                    ValueName = rule.ValueName,
                    MinimumValue = rule.MinimumValue,
                    MaximumValue = rule.MaximumValue,
                    Score = rule.Score,
                    Remarks = rule.Remarks,
                    RowVersion = rule.RowVersion
                });
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["ErrorMessage"] = await GetErrorMessageAsync(
                ex,
                nameof(Edit),
                "Unable to open the risk score.");
        }

        return RedirectToAction(nameof(Index));
    }

    // =====================================================
    // EDIT RULE — POST
    // =====================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.RiskScore.Manage)]
    public Task<IActionResult> Edit(
        RiskRuleEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        return SaveRuleAsync(
            model,
            isNew: false,
            cancellationToken: cancellationToken);
    }

    // =====================================================
    // SHARED SAVE
    // =====================================================

    private async Task<IActionResult> SaveRuleAsync(
        RiskRuleEditViewModel model,
        bool isNew,
        CancellationToken cancellationToken)
    {
        var action = isNew ? nameof(Add) : nameof(Edit);

        // Prevent an Add request from updating an existing rule,
        // or an Edit request from creating a new rule.
        if ((isNew && model.Id != 0) ||
            (!isNew && model.Id <= 0))
        {
            return BadRequest("Invalid risk score request.");
        }

        model.FactorCode = NormalizeFactorCode(model.FactorCode);
        model.ValueCode = Clean(model.ValueCode);
        model.ValueName = Clean(model.ValueName);
        model.Remarks = Clean(model.Remarks);

        // These are display properties. Never trust posted values.
        model.ValueType = string.Empty;
        model.ValueDescription = string.Empty;

        ModelState.Remove(nameof(model.ValueType));
        ModelState.Remove(nameof(model.ValueDescription));

        if (string.IsNullOrEmpty(model.FactorCode))
            return BadRequest("Select a risk factor.");

        var metadataLoaded = false;

        try
        {
            var factor = await FindFactorAsync(
                model.FactorCode,
                cancellationToken);

            if (factor is null)
                return NotFound();

            model.FactorCode = factor.FactorCode;
            model.ValueType = factor.ValueType.ToUpperInvariant();
            model.ValueDescription = factor.FactorName;
            metadataLoaded = true;

            if (!isNew)
            {
                var existing = await _riskScoring.GetRuleAsync(
                    model.Id,
                    cancellationToken);

                if (existing is null)
                    return NotFound();

                // A rule cannot be moved to another factor by
                // changing the hidden FactorCode field.
                if (existing.RiskFactorId != factor.Id)
                    return BadRequest("The rule belongs to another factor.");

                var blocked = GetEditBlockReason(existing);

                if (blocked is not null)
                {
                    TempData["ErrorMessage"] = blocked;

                    return RedirectToAction(
                        nameof(Index),
                        new { factorCode = factor.FactorCode });
                }

                if (model.RowVersion is not { Length: 8 })
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Reload the rule before saving. " +
                        "Its version is missing or invalid.");
                }
            }

            if (!ModelState.IsValid)
                return View("Form", model);

            await _riskScoring.SaveRuleAsync(
                new RiskRuleInput
                {
                    Id = model.Id,
                    FactorCode = factor.FactorCode,
                    ValueCode = model.ValueCode,
                    ValueName = model.ValueName,
                    MinimumValue = model.MinimumValue,
                    MaximumValue = model.MaximumValue,
                    Score = model.Score,
                    Remarks = model.Remarks,
                    RowVersion = model.RowVersion
                },
                cancellationToken);

            TempData["SuccessMessage"] = isNew
                ? "Risk score created as Pending. A different authorized checker must approve it."
                : "Risk score change saved as Pending. A different authorized checker must approve it.";

            return RedirectToAction(
                nameof(Index),
                new { factorCode = factor.FactorCode });
        }
        catch (RiskConfigurationException ex)
        {
            if (!metadataLoaded)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Form", model);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var message = await GetErrorMessageAsync(
                ex,
                action,
                "Unable to save the risk score.");

            if (!metadataLoaded)
            {
                TempData["ErrorMessage"] = message;
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, message);
            return View("Form", model);
        }
    }

    // =====================================================
    // REQUEST DELETION
    // =====================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.RiskScore.Manage)]
    public async Task<IActionResult> Delete(
        int id,
        byte[]? rowVersion,
        string? factorCode,
        CancellationToken cancellationToken = default)
    {
        factorCode = NormalizeFactorCode(factorCode);

        if (id <= 0)
            return BadRequest("Invalid risk score.");

        if (rowVersion is not { Length: 8 })
        {
            TempData["ErrorMessage"] =
                "Reload the list before deleting. " +
                "The record version is missing or invalid.";

            return RedirectToAction(
                nameof(Index),
                new { factorCode });
        }

        try
        {
            var rule = await _riskScoring.GetRuleAsync(
                id,
                cancellationToken);

            if (rule is null)
                return NotFound();

            var setup = await _riskScoring.GetSetupAsync(
                null,
                null,
                1,
                PageSize,
                cancellationToken);

            var factor = setup.Factors.FirstOrDefault(
                item => item.Id == rule.RiskFactorId);

            if (factor is null)
                return NotFound();

            // Use the stored rule's factor for navigation.
            factorCode = factor.FactorCode;

            var blocked = GetEditBlockReason(rule);

            if (blocked is not null)
            {
                TempData["ErrorMessage"] = blocked;
            }
            else
            {
                await _riskScoring.DeleteRuleAsync(
                    id,
                    rowVersion,
                    cancellationToken);

                TempData["SuccessMessage"] =
                    "Deletion saved as Pending. A different authorized " +
                    "checker must approve it.";
            }
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["ErrorMessage"] = await GetErrorMessageAsync(
                ex,
                nameof(Delete),
                "Unable to request deletion.");
        }

        return RedirectToAction(
            nameof(Index),
            new { factorCode });
    }

    // =====================================================
    // REVIEW PENDING CHANGE
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> Review(
        string? factorCode,
        int ruleId,
        CancellationToken cancellationToken = default)
    {
        factorCode = NormalizeFactorCode(factorCode);

        if (ruleId <= 0 || string.IsNullOrEmpty(factorCode))
            return NotFound();

        try
        {
            // The review service also handles pending deletions.
            // Do not replace this with the ordinary GetRuleAsync.
            var review = await _riskScoring.GetApprovalReviewAsync(
                "Rules",
                factorCode,
                cancellationToken,
                ruleId);

            return View(ReviewView, review);
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["ErrorMessage"] = await GetErrorMessageAsync(
                ex,
                nameof(Review),
                "Unable to open the approval review.");
        }

        return RedirectToAction(
            nameof(Index),
            new { factorCode });
    }

    // =====================================================
    // APPROVE PENDING CHANGE
    // =====================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.RiskScore.Approve)]
    public async Task<IActionResult> Apply(
        string? factorCode,
        int ruleId,
        string? reviewToken,
        CancellationToken cancellationToken = default)
    {
        factorCode = NormalizeFactorCode(factorCode);

        if (ruleId <= 0 || string.IsNullOrEmpty(factorCode))
            return BadRequest("Invalid risk score approval request.");

        if (string.IsNullOrWhiteSpace(reviewToken))
        {
            TempData["ErrorMessage"] =
                "Open the review again before approving.";

            return RedirectToAction(
                nameof(Index),
                new { factorCode });
        }

        try
        {
            // Service checks partner, maker/checker separation,
            // draft version and the resulting rule configuration.
            await _riskScoring.ActivateRulesAsync(
                factorCode,
                ruleId,
                reviewToken,
                cancellationToken);

            TempData["SuccessMessage"] =
                "Risk score change approved successfully.";
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["ErrorMessage"] = await GetErrorMessageAsync(
                ex,
                nameof(Apply),
                "Unable to approve the risk score.");
        }

        return RedirectToAction(
            nameof(Index),
            new { factorCode });
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private async Task<RiskFactor?> FindFactorAsync(
        string factorCode,
        CancellationToken cancellationToken)
    {
        var setup = await _riskScoring.GetSetupAsync(
            factorCode,
            null,
            1,
            PageSize,
            cancellationToken);

        return setup.Factors.FirstOrDefault(factor =>
            string.Equals(
                factor.FactorCode,
                factorCode,
                StringComparison.OrdinalIgnoreCase));
    }

    private string? GetEditBlockReason(RiskFactorRule rule)
    {
        if (rule.DeleteFg != "N")
            return "This rule has been deleted or has a pending deletion.";

        if (rule.ApprovalStatus is "PENDING" or "DRAFT")
        {
            var validUserId = long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId);

            if (!validUserId ||
                userId <= 0 ||
                rule.MakerUserId != userId)
            {
                return "Only the maker can change this pending rule.";
            }

            return null;
        }

        if (rule.ApprovalStatus != "APPROVED" || !rule.IsActive)
            return "This rule is no longer current. Open its latest version.";

        return null;
    }

    private IActionResult IndexError(string message)
    {
        ViewBag.ErrorMessage = message;

        return View(
            "Index",
            new RiskScoringPageViewModel
            {
                ErrorMessage = message
            });
    }

    private async Task<string> GetErrorMessageAsync(
        Exception exception,
        string action,
        string message)
    {
        var errorId = await _errorLogService.LogAsync(
            exception,
            nameof(RiskScoresController),
            action,
            User.FindFirstValue(AppClaimTypes.PartnerCode));

        return errorId.HasValue
            ? $"{message} Error reference: {errorId.Value}."
            : message;
    }

    private static string NormalizeFactorCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}