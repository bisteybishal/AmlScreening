using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.Risk.View)]
public sealed class RiskScoreSetupController : Controller
{
    private readonly IRiskScoringService _riskScoring;
    private readonly IErrorLogService _errorLogService;

    public RiskScoreSetupController(IRiskScoringService riskScoring, IErrorLogService errorLogService)
    {
        _riskScoring = riskScoring;
        _errorLogService = errorLogService;
    }

    [HttpGet("/RiskScoreSetup/Index", Order = 0)]
    [HttpGet("/RiskScoring/Setup", Order = 1)] // Existing menu links still work.
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _riskScoring.GetSetupAsync(null, null, 1, 10, cancellationToken);
            return View(RiskScoringPageViewModel.From(result));
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
                            ? $"Unable to load risk ranges. Error reference: {errorId}"
                            : "Risk ranges could not be loaded. Please contact your administrator."
                });
        }



    }

    [HttpGet, Authorize(Policy = AppPermissions.Risk.Manage)]
    public async Task<IActionResult> Edit(CancellationToken cancellationToken)
    {
        try
        {

            var result = await _riskScoring.GetSetupAsync(null, null, 1, 10, cancellationToken);
            if (result.Bands.Count == 0)
            {
                return View(new RiskScoreSetupEditViewModel
                {
                    Bands =
                    [
                        new() { RiskLevel = "LOW", MinimumScore = 0, MaximumScore = 29, Decision = "ALLOW" },
                    new() { RiskLevel = "MEDIUM", MinimumScore = 30, MaximumScore = 59, Decision = "REVIEW" },
                    new() { RiskLevel = "HIGH", MinimumScore = 60, MaximumScore = 79, Decision = "REVIEW" },
                    new() { RiskLevel = "CRITICAL", MinimumScore = 80, MaximumScore = 100, Decision = "BLOCK" }
                    ]
                });
            }
            return View(new RiskScoreSetupEditViewModel
            {
                Bands = result.Bands.Select(b => new RiskBandEditViewModel
                {
                    Id = b.Id,
                    RiskLevel = b.RiskLevel,
                    MinimumScore = b.MinimumScore,
                    MaximumScore = b.MaximumScore,
                    Decision = b.Decision,
                    Remarks = b.Remarks,
                    RowVersion = b.RowVersion
                }).ToList()
            });
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
                            ? $"Unable to Edit risk ranges. Error reference: {errorId}"
                            : "Risk ranges could not be Edite. Please contact your administrator."
                });
        }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.Risk.Manage)]
    public async Task<IActionResult> Edit(RiskScoreSetupEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            // Save all adjoining ranges together, never one half of a boundary change.
            await _riskScoring.SaveBandRangesAsync(model.Bands.Select(b => new RiskBandInput
            {
                Id = b.Id,
                RiskLevel = b.RiskLevel,
                Decision = b.Decision,
                MinimumScore = b.MinimumScore,
                MaximumScore = b.MaximumScore,
                Remarks = b.Remarks,
                RowVersion = b.RowVersion
            }).ToList(), cancellationToken);
            TempData["SuccessMessage"] = "Ranges saved as Pending. Another administrator must review and approve.";
            return RedirectToAction(nameof(Index));
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
                            ? $"Unable to Edit risk ranges. Error reference: {errorId}"
                            : "Risk ranges could not be Edited. Please contact your administrator."
                });
        }
    }

    //[HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.Risk.Approve)]
    //public async Task<IActionResult> Apply(string reviewToken, CancellationToken cancellationToken)
    //{
    //    try
    //    {
    //        await _riskScoring.ActivateBandsAsync(reviewToken, cancellationToken);
    //        TempData["SuccessMessage"] = "Ranges approved. New evaluations use the approved settings.";
    //    }
    //    catch (Exception ex)
    //    {
    //        var errorId =
    //            await LogErrorAsync(
    //                ex,
    //                nameof(Index));


    //        return View(
    //            new RiskScoringPageViewModel
    //            {
    //                ErrorMessage =
    //                    errorId.HasValue
    //                        ? $"Unable to load risk ranges. Error reference: {errorId}"
    //                        : "Risk ranges could not be loaded. Please contact your administrator."
    //            });
    //    }
    //}

    [HttpGet]
    public async Task<IActionResult> Review(CancellationToken cancellationToken)
    {
        try
        {
            var review = await _riskScoring.GetApprovalReviewAsync("Bands", null, cancellationToken);
            return View("~/Views/Shared/RiskApprovalReview.cshtml", review);
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
                            ? $"Unable to load risk ranges. Error reference: {errorId}"
                            : "Risk ranges could not be loaded. Please contact your administrator."
                });
        }
    }
    [HttpGet]
    [Authorize(
  Policy = AppPermissions.Risk.Manage)]
    public async Task<IActionResult> EditModel(
  CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _riskScoring.GetSetupAsync(
                    null,
                    null,
                    1,
                    10,
                    cancellationToken);

            var model =
                result.PendingModel ??
                result.ActiveModel;

            if (model == null)
            {
                return View(
                    "Model",
                    new RiskModelEditViewModel
                    {
                        CustomerWeightPercent = 40,
                        TransactionWeightPercent = 40,
                        ScreeningWeightPercent = 20
                    });
            }

            return View(
                "Model",
                new RiskModelEditViewModel
                {
                    Id = model.Id,

                    CustomerWeightPercent =
                        model.CustomerWeightPercent,

                    TransactionWeightPercent =
                        model.TransactionWeightPercent,

                    ScreeningWeightPercent =
                        model.ScreeningWeightPercent,

                    RowVersion =
                        model.RowVersion
                });
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
                            ? $"Unable to load risk ranges. Error reference: {errorId}"
                            : "Risk ranges could not be loaded. Please contact your administrator."
                });
        }
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
    Policy = AppPermissions.Risk.Manage)]
    public async Task<IActionResult> EditModel(
    RiskModelEditViewModel model,
    CancellationToken cancellationToken)
    {
        var total =
            model.CustomerWeightPercent +
            model.TransactionWeightPercent +
            model.ScreeningWeightPercent;

        if (total != 100m)
        {
            ModelState.AddModelError(
                string.Empty,
                "Customer, Transaction and Screening weights must total exactly 100%.");
        }

        if (!ModelState.IsValid)
        {
            return View(
                "Model",
                model);
        }

        try
        {
            await _riskScoring.SaveModelAsync(
                new RiskModelInput
                {
                    Id =
                        model.Id,

                    CustomerWeightPercent =
                        model.CustomerWeightPercent,

                    TransactionWeightPercent =
                        model.TransactionWeightPercent,

                    ScreeningWeightPercent =
                        model.ScreeningWeightPercent,

                    RowVersion =
                        model.RowVersion
                },
                cancellationToken);

            TempData["SuccessMessage"] =
                "Risk model saved as Pending. A different authorized checker must approve it.";

            return RedirectToAction(
                nameof(Index));
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
                            ? $"Unable to load risk ranges. Error reference: {errorId}"
                            : "Risk ranges could not be loaded. Please contact your administrator."
                });
        }
    }


    [HttpGet]
    public async Task<IActionResult> ReviewModel(
   CancellationToken cancellationToken)
    {
        try
        {
            var review =
                await _riskScoring
                    .GetApprovalReviewAsync(
                        "Model",
                        null,
                        cancellationToken);

            return View(
                "~/Views/Shared/RiskApprovalReview.cshtml",
                review);
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
                            ? $"Unable to load risk ranges. Error reference: {errorId}"
                            : "Risk ranges could not be loaded. Please contact your administrator."
                });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
    Policy = AppPermissions.Risk.Approve)]
    public async Task<IActionResult> ApplyModel(
    string reviewToken,
    CancellationToken cancellationToken)
    {
        try
        {
            await _riskScoring.ActivateModelAsync(
                reviewToken,
                cancellationToken);

            TempData["SuccessMessage"] =
                "Risk model approved. New evaluations now use the approved model.";
            return RedirectToAction(
          nameof(Index));
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
                            ? $"Unable to load risk ranges. Error reference: {errorId}"
                            : "Risk ranges could not be loaded. Please contact your administrator."
                });
        }
      
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
