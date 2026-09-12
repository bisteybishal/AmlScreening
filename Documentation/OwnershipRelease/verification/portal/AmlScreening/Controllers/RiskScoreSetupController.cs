using AmlScreening.Application.Interface;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.RiskScoringView)]
[Authorize(Roles = "Admin")]
public sealed class RiskScoreSetupController(
    IRiskScoringService riskScoring,
    ILogger<RiskScoreSetupController> logger) : Controller
{
    [HttpGet("/RiskScoreSetup/Index", Order = 0)]
    [HttpGet("/RiskScoring/Setup", Order = 1)] // Existing menu links still work.
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var result = await riskScoring.GetSetupAsync(null, null, 1, 10, cancellationToken);
            return View(RiskScoringPageViewModel.From(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to load risk ranges.");
            ViewBag.ErrorMessage = "Risk ranges could not be loaded. Please contact your administrator.";
            return View(new RiskScoringPageViewModel());
        }
    }

    [HttpGet, Authorize(Policy = AppPermissions.RiskScoringManage)]
    public async Task<IActionResult> Edit(CancellationToken cancellationToken)
    {
        var result = await riskScoring.GetSetupAsync(null, null, 1, 10, cancellationToken);
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
                Id = b.Id, RiskLevel = b.RiskLevel, MinimumScore = b.MinimumScore,
                MaximumScore = b.MaximumScore, Decision = b.Decision,
                Remarks = b.Remarks, RowVersion = b.RowVersion
            }).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.RiskScoringManage)]
    public async Task<IActionResult> Edit(RiskScoreSetupEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            // Save all adjoining ranges together, never one half of a boundary change.
            await riskScoring.SaveBandRangesAsync(model.Bands.Select(b => new RiskBandInput
            {
                Id = b.Id, RiskLevel = b.RiskLevel, Decision = b.Decision,
                MinimumScore = b.MinimumScore, MaximumScore = b.MaximumScore,
                Remarks = b.Remarks, RowVersion = b.RowVersion
            }).ToList(), cancellationToken);
            TempData["SuccessMessage"] = "Ranges saved as Pending. Another administrator must review and approve.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ErrorMessage(ex));
            return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.RiskScoringApprove)]
    public async Task<IActionResult> Apply(string reviewToken, CancellationToken cancellationToken)
    {
        try
        {
            await riskScoring.ActivateBandsAsync(reviewToken, cancellationToken);
            TempData["SuccessMessage"] = "Ranges approved. New evaluations use the approved settings.";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = ErrorMessage(ex); }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Review(CancellationToken cancellationToken)
    {
        try
        {
            var review = await riskScoring.GetApprovalReviewAsync("Bands", null, cancellationToken);
            return View("~/Views/Shared/RiskApprovalReview.cshtml", review);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ErrorMessage(ex);
            return RedirectToAction(nameof(Index));
        }
    }

    private string ErrorMessage(Exception ex)
    {
        if (ex is RiskConfigurationException) return ex.Message;
        logger.LogError(ex, "Risk range operation failed.");
        return "Unable to save. Please reload or contact your administrator.";
    }
}
