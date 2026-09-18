using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AmlScreening.Portal.Controllers;


[Authorize(
    Policy = AppPermissions.Risk.View)]
public sealed class RiskScoresController
    : Controller
{
    private readonly IRiskScoringService
        _riskScoring;

    private readonly IErrorLogService
        _errorLogService;


    public RiskScoresController(
        IRiskScoringService riskScoring,
        IErrorLogService errorLogService)
    {
        _riskScoring =
            riskScoring;

        _errorLogService =
            errorLogService;
    }


    // =========================================================
    // INDEX
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Index(
        string? factorCode,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            page =
                Math.Max(
                    1,
                    page);


            factorCode =
                string.IsNullOrWhiteSpace(
                    factorCode)

                    ? null

                    : factorCode.Trim()
                        .ToUpperInvariant();


            var result =
                await _riskScoring
                    .GetSetupAsync(
                        factorCode,
                        null,
                        page,
                        10,
                        cancellationToken);


            var model =
                RiskScoringPageViewModel
                    .From(result);


            return View(model);
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Index));


            return View(
                new RiskScoringPageViewModel
                {
                    ErrorMessage =
                        errorId.HasValue
                            ? $"Risk scores could not be loaded. Error reference: {errorId}"
                            : "Risk scores could not be loaded."
                });
        }
    }


    // =========================================================
    // ADD GET
    // =========================================================

    [HttpGet]
    [Authorize(
        Policy = AppPermissions.Risk.Manage)]
    public async Task<IActionResult> Add(
        string factorCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            factorCode =
                factorCode?
                    .Trim()
                    .ToUpperInvariant()
                ?? string.Empty;


            if (string.IsNullOrWhiteSpace(
                    factorCode))
            {
                return BadRequest();
            }


            var setup =
                await _riskScoring
                    .GetSetupAsync(
                        factorCode,
                        null,
                        1,
                        10,
                        cancellationToken);


            var factor =
                setup.SelectedFactor;


            if (factor == null
                ||
                !string.Equals(
                    factor.FactorCode,
                    factorCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }


            var model =
                new RiskRuleEditViewModel
                {
                    FactorCode =
                        factor.FactorCode,

                    ValueType =
                        factor.ValueType,

                    ValueDescription =
                        factor.FactorName
                };


            return View(
                "Form",
                model);
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Add));


            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Unable to open the risk score form. Error reference: {errorId}"
                    : "Unable to open the risk score form.";


            return RedirectToAction(
                nameof(Index));
        }
    }


    // =========================================================
    // ADD POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = AppPermissions.Risk.Manage)]
    public async Task<IActionResult> Add(
        RiskRuleEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.Id != 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "Invalid new risk score.");
        }


        return await SaveRuleAsync(
            model,
            cancellationToken);
    }


    // =========================================================
    // EDIT GET
    // =========================================================

    [HttpGet]
    [Authorize(
        Policy = AppPermissions.Risk.Manage)]
    public async Task<IActionResult> Edit(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id <= 0)
            {
                return NotFound();
            }


            var rule =
                await _riskScoring
                    .GetRuleAsync(
                        id,
                        cancellationToken);


            if (rule == null)
            {
                return NotFound();
            }


            var setup =
                await _riskScoring
                    .GetSetupAsync(
                        null,
                        null,
                        1,
                        10,
                        cancellationToken);


            var factor =
                setup.Factors
                    .FirstOrDefault(
                        x =>
                            x.Id ==
                            rule.RiskFactorId);


            if (factor == null)
            {
                return NotFound();
            }


            var model =
                new RiskRuleEditViewModel
                {
                    Id =
                        rule.Id,

                    FactorCode =
                        factor.FactorCode,

                    ValueType =
                        factor.ValueType,

                    ValueDescription =
                        factor.FactorName,

                    ValueCode =
                        rule.ValueCode,

                    ValueName =
                        rule.ValueName,

                    MinimumValue =
                        rule.MinimumValue,

                    MaximumValue =
                        rule.MaximumValue,

                    Score =
                        rule.Score,

                    Remarks =
                        rule.Remarks,

                    RowVersion =
                        rule.RowVersion
                };


            return View(
                "Form",
                model);
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Edit));


            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Unable to open the risk score. Error reference: {errorId}"
                    : "Unable to open the risk score.";


            return RedirectToAction(
                nameof(Index));
        }
    }


    // =========================================================
    // EDIT POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = AppPermissions.Risk.Manage)]
    public async Task<IActionResult> Edit(
        RiskRuleEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.Id <= 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "Invalid risk score.");
        }


        return await SaveRuleAsync(
            model,
            cancellationToken);
    }


    // =========================================================
    // SAVE ADD / EDIT
    // =========================================================

    private async Task<IActionResult>
        SaveRuleAsync(
            RiskRuleEditViewModel model,
            CancellationToken cancellationToken)
    {
        try
        {
            model.FactorCode =
                model.FactorCode?
                    .Trim()
                    .ToUpperInvariant()
                ?? string.Empty;


            var setup =
                await _riskScoring
                    .GetSetupAsync(
                        model.FactorCode,
                        null,
                        1,
                        10,
                        cancellationToken);


            var factor =
                setup.SelectedFactor;


            if (factor == null
                ||
                !string.Equals(
                    factor.FactorCode,
                    model.FactorCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }


            /*
             * Never trust factor metadata
             * supplied by browser.
             */

            model.ValueType =
                factor.ValueType;

            model.ValueDescription =
                factor.FactorName;


            if (!ModelState.IsValid)
            {
                return View(
                    "Form",
                    model);
            }


            var input =
                new RiskRuleInput
                {
                    Id =
                        model.Id,

                    FactorCode =
                        factor.FactorCode,

                    ValueCode =
                        NullIfWhiteSpace(
                            model.ValueCode),

                    ValueName =
                        NullIfWhiteSpace(
                            model.ValueName),

                    MinimumValue =
                        model.MinimumValue,

                    MaximumValue =
                        model.MaximumValue,

                    Score =
                        model.Score,

                    Remarks =
                        NullIfWhiteSpace(
                            model.Remarks),

                    RowVersion =
                        model.RowVersion
                };


            await _riskScoring
                .SaveRuleAsync(
                    input,
                    cancellationToken);


            TempData["SuccessMessage"] =
                model.Id == 0
                    ? "Risk score created as Pending. A different authorized checker must approve it."
                    : "Risk score change saved as Pending. A different authorized checker must approve it.";


            return RedirectToAction(
                nameof(Index),
                new
                {
                    factorCode =
                        factor.FactorCode
                });
        }
        catch (RiskConfigurationException ex)
        {
            ModelState.AddModelError(
                string.Empty,
                ex.Message);


            await ReloadFactorMetadataAsync(
                model,
                cancellationToken);


            return View(
                "Form",
                model);
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(SaveRuleAsync));


            ModelState.AddModelError(
                string.Empty,
                errorId.HasValue
                    ? $"Unable to save risk score. Error reference: {errorId}"
                    : "Unable to save risk score.");


            await ReloadFactorMetadataAsync(
                model,
                cancellationToken);


            return View(
                "Form",
                model);
        }
    }


    // =========================================================
    // DELETE
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = AppPermissions.Risk.Manage)]
    public async Task<IActionResult> Delete(
        int id,
        byte[] rowVersion,
        string factorCode,
        CancellationToken cancellationToken = default)
    {
        factorCode =
            factorCode?
                .Trim()
                .ToUpperInvariant()
            ?? string.Empty;


        try
        {
            if (id <= 0)
            {
                throw new RiskConfigurationException(
                    "Invalid risk score.");
            }


            if (rowVersion is not
                { Length: > 0 })
            {
                throw new RiskConfigurationException(
                    "The record changed. Reload the list and try again.");
            }


            await _riskScoring
                .DeleteRuleAsync(
                    id,
                    rowVersion,
                    cancellationToken);


            TempData["SuccessMessage"] =
                "Risk score deletion saved as Pending. A different authorized checker must approve it.";
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] =
                ex.Message;
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Delete));


            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Unable to delete risk score. Error reference: {errorId}"
                    : "Unable to delete risk score.";
        }


        return RedirectToAction(
            nameof(Index),
            new
            {
                factorCode
            });
    }


    // =========================================================
    // REVIEW
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Review(
        string factorCode,
        int ruleId,
        CancellationToken cancellationToken = default)
    {
        factorCode =
            factorCode?
                .Trim()
                .ToUpperInvariant()
            ?? string.Empty;


        try
        {
            if (ruleId <= 0
                ||
                string.IsNullOrWhiteSpace(
                    factorCode))
            {
                return NotFound();
            }


            var review =
                await _riskScoring
                    .GetApprovalReviewAsync(
                        "Rules",
                        factorCode,
                        cancellationToken,
                        ruleId);


            return View(
                "~/Views/Shared/RiskApprovalReview.cshtml",
                review);
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] =
                ex.Message;
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Review));


            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Unable to load approval review. Error reference: {errorId}"
                    : "Unable to load approval review.";
        }


        return RedirectToAction(
            nameof(Index),
            new
            {
                factorCode
            });
    }


    // =========================================================
    // APPROVE
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = AppPermissions.Risk.Approve)]
    public async Task<IActionResult> Apply(
        string factorCode,
        int ruleId,
        string reviewToken,
        CancellationToken cancellationToken = default)
    {
        factorCode =
            factorCode?
                .Trim()
                .ToUpperInvariant()
            ?? string.Empty;


        try
        {
            if (ruleId <= 0)
            {
                throw new RiskConfigurationException(
                    "Invalid risk score.");
            }


            if (string.IsNullOrWhiteSpace(
                    reviewToken))
            {
                throw new RiskConfigurationException(
                    "Approval review expired. Reload and review the change.");
            }


            await _riskScoring
                .ActivateRulesAsync(
                    factorCode,
                    ruleId,
                    reviewToken,
                    cancellationToken);


            TempData["SuccessMessage"] =
                "Risk score change approved successfully.";
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] =
                ex.Message;
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Apply));


            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Unable to approve risk score. Error reference: {errorId}"
                    : "Unable to approve risk score.";
        }


        return RedirectToAction(
            nameof(Index),
            new
            {
                factorCode
            });
    }


    // =========================================================
    // FACTOR METADATA
    // =========================================================

    private async Task ReloadFactorMetadataAsync(
        RiskRuleEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                model.FactorCode))
        {
            return;
        }


        var setup =
            await _riskScoring
                .GetSetupAsync(
                    model.FactorCode,
                    null,
                    1,
                    10,
                    cancellationToken);


        var factor =
            setup.SelectedFactor;


        if (factor == null)
        {
            return;
        }


        model.ValueType =
            factor.ValueType;

        model.ValueDescription =
            factor.FactorName;
    }


    private static string?
        NullIfWhiteSpace(
            string? value)
    {
        return string.IsNullOrWhiteSpace(
                value)

            ? null

            : value.Trim();
    }


    // =========================================================
    // ERROR LOG
    // =========================================================

    private Task<long?> LogErrorAsync(
        Exception exception,
        string action)
    {
        return _errorLogService
            .LogAsync(
                exception,
                nameof(RiskScoresController),
                action,
                User.FindFirstValue(
                    AppClaimTypes.PartnerCode));
    }
}