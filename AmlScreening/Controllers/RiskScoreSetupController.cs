using AmlScreening.Application.Interface;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.RiskScoringView)]
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
            TempData["ErrorMessage"] = "Risk ranges are not configured. Ask your administrator to complete database setup.";
            return RedirectToAction(nameof(Index));
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
                Id = b.Id, MinimumScore = b.MinimumScore, MaximumScore = b.MaximumScore,
                Remarks = b.Remarks, RowVersion = b.RowVersion
            }).ToList(), cancellationToken);
            TempData["SuccessMessage"] = "Ranges saved for review. Apply reviewed changes when ready.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ErrorMessage(ex));
            return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.RiskScoringManage)]
    public async Task<IActionResult> Apply(CancellationToken cancellationToken)
    {
        try
        {
            await riskScoring.ActivateBandsAsync(cancellationToken);
            TempData["SuccessMessage"] = "Reviewed ranges applied to new evaluations.";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = ErrorMessage(ex); }
        return RedirectToAction(nameof(Index));
    }

    private string ErrorMessage(Exception ex)
    {
        if (ex is RiskConfigurationException) return ex.Message;
        logger.LogError(ex, "Risk range operation failed.");
        return "Unable to save. Please reload or contact your administrator.";
    }
}
