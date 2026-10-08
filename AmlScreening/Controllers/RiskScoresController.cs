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

    // LIST RULES
  

    [HttpGet]
    public async Task<IActionResult> Index(string? factorCode,int page = 1,CancellationToken cancellationToken = default)
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

            // If user manually changes URL
            // to invalid factor code.
            if (!string.IsNullOrEmpty(factorCode) &&
                !result.Factors.Any(x =>
                    string.Equals(
                        x.FactorCode,
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
            return ShowIndexError(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var message = await GetErrorMessageAsync(
                ex,
                nameof(Index),
                "Unable to load risk scores.");

            return ShowIndexError(message);
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

            if (factor == null)
                return NotFound();

            var model =
                new RiskRuleEditViewModel
                {
                    FactorCode =
                        factor.FactorCode,

                    ValueType =
                        factor.ValueType
                            .ToUpperInvariant(),

                    ValueDescription =
                        factor.FactorName
                };


            return View("Form", model);
               
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

        return RedirectToAction(
                 nameof(Index),
                 new { factorCode });
    }

    // =====================================================
    // ADD RULE — POST
    // =====================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.RiskScore.Manage)]
    public async Task<IActionResult> Add(
        RiskRuleEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.Id != 0)
        {
            return BadRequest("Invalid new risk score.");
        }
        return await SaveRuleAsync(
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

            if (rule == null)
                return NotFound();

            var blocked = GetEditBlockReason(rule);

            if (blocked !=null)
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

            var factor = await FindFactorByRuleAsync(rule, cancellationToken);

            if (factor == null)
                return NotFound();

            var model= new RiskRuleEditViewModel
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
                };
            return View(
              "Form",
              model);
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
    public async Task<IActionResult> Edit(
        RiskRuleEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.Id <= 0)
        {
            return BadRequest(
                "Invalid risk score.");
        }

        return await SaveRuleAsync(
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
        // Clean values.
        model.FactorCode =
            NormalizeFactorCode(
                model.FactorCode);

        model.ValueCode =
            Clean(
                model.ValueCode);

        model.ValueName =
            Clean(
                model.ValueName);

        model.Remarks =
            Clean(
                model.Remarks);


        if (string.IsNullOrWhiteSpace(
                model.FactorCode))
        {
            return BadRequest(
                "Select a risk factor.");
        }


        try
        {
            // Never trust Factor information
            // posted from browser.
            var factor =
                await FindFactorAsync(
                    model.FactorCode,
                    cancellationToken);

            if (factor == null)
            {
                return NotFound();
            }


            model.FactorCode =
                factor.FactorCode;

            model.ValueType =
                factor.ValueType
                    .ToUpperInvariant();

            model.ValueDescription =
                factor.FactorName;


            // ==================================
            // EDIT VALIDATION
            // ==================================

            if (!isNew)
            {
                var currentRule =
                    await _riskScoring
                        .GetRuleAsync(
                            model.Id,
                            cancellationToken);

                if (currentRule == null)
                {
                    return NotFound();
                }


                // Prevent changing FactorCode
                // using browser/dev tools.
                if (currentRule.RiskFactorId !=
                    factor.Id)
                {
                    return BadRequest(
                        "Risk factor cannot be changed.");
                }


                var blocked =
                    GetEditBlockReason(
                        currentRule);

                if (blocked != null)
                {
                    TempData["ErrorMessage"] =
                        blocked;

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            factorCode =
                                factor.FactorCode
                        });
                }


                if (model.RowVersion is not
                    { Length: 8 })
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Reload the score before saving.");
                }
            }


            if (!ModelState.IsValid)
            {
                return View(
                    "Form",
                    model);
            }


            // ==================================
            // SAVE AS PENDING
            // ==================================

            var input =
                new RiskRuleInput
                {
                    Id =
                        model.Id,

                    FactorCode =
                        factor.FactorCode,

                    ValueCode =
                        model.ValueCode,

                    ValueName =
                        model.ValueName,

                    MinimumValue =
                        model.MinimumValue,

                    MaximumValue =
                        model.MaximumValue,

                    Score =
                        model.Score,

                    Remarks =
                        model.Remarks,

                    RowVersion =
                        model.RowVersion
                };


            await _riskScoring.SaveRuleAsync(
                input,
                cancellationToken);


            TempData["SuccessMessage"] =
                isNew
                    ? "Risk score created as Pending. A different user must approve it."
                    : "Risk score updated as Pending. A different user must approve it.";


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

            return View(
                "Form",
                model);
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException)
        {
            var message =
                await GetErrorMessageAsync(
                    ex,
                    isNew
                        ? nameof(Add)
                        : nameof(Edit),
                    "Unable to save the risk score.");

            ModelState.AddModelError(
                string.Empty,
                message);

            return View(
                "Form",
                model);
        }
    }


    // =====================================================
    // REQUEST DELETION
    // =====================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
       Policy = AppPermissions.RiskScore.Manage)]
    public async Task<IActionResult> Delete(
       int id,
       byte[]? rowVersion,
       CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return BadRequest(
                "Invalid risk score.");
        }


        if (rowVersion is not
            { Length: 8 })
        {
            TempData["ErrorMessage"] =
                "Reload the page before deleting.";

            return RedirectToAction(
                nameof(Index));
        }


        string? factorCode =
            null;


        try
        {
            var rule =
                await _riskScoring
                    .GetRuleAsync(
                        id,
                        cancellationToken);

            if (rule == null)
            {
                return NotFound();
            }


            var factor =
                await FindFactorByRuleAsync(
                    rule,
                    cancellationToken);

            if (factor == null)
            {
                return NotFound();
            }


            factorCode =
                factor.FactorCode;


            var blocked =
                GetEditBlockReason(
                    rule);

            if (blocked != null)
            {
                TempData["ErrorMessage"] =
                    blocked;
            }
            else
            {
                await _riskScoring
                    .DeleteRuleAsync(
                        id,
                        rowVersion,
                        cancellationToken);


                TempData["SuccessMessage"] =
                    "Deletion saved as Pending. " +
                    "A different user must approve it.";
            }
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] =
                ex.Message;
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException)
        {
            TempData["ErrorMessage"] =
                await GetErrorMessageAsync(
                    ex,
                    nameof(Delete),
                    "Unable to delete the risk score.");
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
        factorCode =
            NormalizeFactorCode(
                factorCode);


        if (string.IsNullOrWhiteSpace(
                factorCode)
            ||
            ruleId <= 0)
        {
            return NotFound();
        }


        try
        {
            var review =
                await _riskScoring
                    .GetApprovalReviewAsync(
                        "Rules",
                        factorCode,
                        cancellationToken,
                        ruleId);


            return View(
                ReviewView,
                review);
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] =
                ex.Message;
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException)
        {
            TempData["ErrorMessage"] =
                await GetErrorMessageAsync(
                    ex,
                    nameof(Review),
                    "Unable to open the review.");
        }


        return RedirectToAction(
            nameof(Index),
            new { factorCode });
    }


    // =====================================================
    // APPROVE
    // =====================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = AppPermissions.RiskScore.Approve)]
    public async Task<IActionResult> Apply(
        string? factorCode,
        int ruleId,
        string? reviewToken,
        CancellationToken cancellationToken = default)
    {
        factorCode =
            NormalizeFactorCode(
                factorCode);


        if (string.IsNullOrWhiteSpace(
                factorCode)
            ||
            ruleId <= 0)
        {
            return BadRequest(
                "Invalid approval request.");
        }


        if (string.IsNullOrWhiteSpace(
                reviewToken))
        {
            TempData["ErrorMessage"] =
                "Open the review again before approving.";

            return RedirectToAction(
                nameof(Index),
                new { factorCode });
        }


        try
        {
            await _riskScoring
                .ActivateRulesAsync(
                    factorCode,
                    ruleId,
                    reviewToken,
                    cancellationToken);


            TempData["SuccessMessage"] =
                "Risk score approved successfully.";
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] =
                ex.Message;
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException)
        {
            TempData["ErrorMessage"] =
                await GetErrorMessageAsync(
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

    private async Task<RiskFactor?>
        FindFactorAsync(
            string factorCode,
            CancellationToken cancellationToken)
    {
        var setup =
            await _riskScoring
                .GetSetupAsync(
                    factorCode,
                    null,
                    1,
                    PageSize,
                    cancellationToken);


        return setup.Factors
            .FirstOrDefault(x =>
                string.Equals(
                    x.FactorCode,
                    factorCode,
                    StringComparison.OrdinalIgnoreCase));
    }


    private async Task<RiskFactor?>
        FindFactorByRuleAsync(
            RiskFactorRule rule,
            CancellationToken cancellationToken)
    {
        var setup =
            await _riskScoring
                .GetSetupAsync(
                    null,
                    null,
                    1,
                    PageSize,
                    cancellationToken);


        return setup.Factors
            .FirstOrDefault(x =>
                x.Id ==
                rule.RiskFactorId);
    }


    private string? GetEditBlockReason(
        RiskFactorRule rule)
    {
        // Deleted rule cannot be changed.
        if (rule.DeleteFg != "N")
        {
            return
                "This risk score has been deleted.";
        }


        // Pending score:
        // only maker can edit it.
        if (rule.ApprovalStatus is
            "PENDING" or "DRAFT")
        {
            var validUserId =
                long.TryParse(
                    User.FindFirstValue(
                        ClaimTypes.NameIdentifier),
                    out var userId);


            if (!validUserId ||
                userId != rule.MakerUserId)
            {
                return
                    "Only the maker can change this pending score.";
            }


            return null;
        }


        // Only current approved score
        // can start another update.
        if (rule.ApprovalStatus != "APPROVED"
            ||
            !rule.IsActive)
        {
            return
                "This risk score is no longer active.";
        }


        return null;
    }


    private IActionResult ShowIndexError(
        string message)
    {
        return View(
            "Index",
            new RiskScoringPageViewModel
            {
                ErrorMessage =
                    message
            });
    }


    private async Task<string>
        GetErrorMessageAsync(
            Exception exception,
            string action,
            string message)
    {
        var errorId =
            await _errorLogService.LogAsync(
                exception,
                nameof(RiskScoresController),
                action,
                User.FindFirstValue(
                    AppClaimTypes.PartnerCode));


        return errorId.HasValue
            ? $"{message} Error reference: {errorId.Value}."
            : message;
    }


    private static string NormalizeFactorCode(
        string? value)
    {
        return string.IsNullOrWhiteSpace(
                value)
            ? string.Empty
            : value
                .Trim()
                .ToUpperInvariant();
    }


    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(
                value)
            ? null
            : value.Trim();
    }
}