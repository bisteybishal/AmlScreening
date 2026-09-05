using AmlScreening.Application.Interface;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.RiskScoringView)]
public sealed class RiskScoresController(IRiskScoringService riskScoring,
    ILogger<RiskScoresController> logger) : Controller
{
    [HttpGet("/RiskScores/Index", Order = 0)]
    [HttpGet("/RiskScoring/Index", Order = 1)] // Existing bookmarks still work.
    public async Task<IActionResult> Index(string? factorCode, int page = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await riskScoring.GetSetupAsync(factorCode, null, page, 10, cancellationToken);
            return View(RiskScoringPageViewModel.From(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to load risk scores.");
            ViewBag.ErrorMessage = "Risk scores could not be loaded. Please contact your administrator.";
            return View(new RiskScoringPageViewModel());
        }
    }

    [HttpGet, Authorize(Policy = AppPermissions.RiskScoringManage)]
    public async Task<IActionResult> Add(string factorCode, CancellationToken cancellationToken)
    {
        var setup = await riskScoring.GetSetupAsync(factorCode, null, 1, 10, cancellationToken);
        if (setup.SelectedFactor?.FactorCode != factorCode) return NotFound();
        return View("Form", new RiskRuleEditViewModel
        {
            FactorCode = factorCode, ValueType = setup.SelectedFactor.ValueType,
            ValueDescription = setup.SelectedFactor.FactorName
        });
    }

    [HttpGet, Authorize(Policy = AppPermissions.RiskScoringManage)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var rule = await riskScoring.GetRuleAsync(id, cancellationToken);
        if (rule is null) return NotFound();
        var setup = await riskScoring.GetSetupAsync(null, null, 1, 10, cancellationToken);
        var factor = setup.Factors.FirstOrDefault(x => x.Id == rule.RiskFactorId);
        if (factor is null) return NotFound();
        return View("Form", new RiskRuleEditViewModel
        {
            Id = rule.Id, FactorCode = factor.FactorCode, ValueType = factor.ValueType,
            ValueDescription = factor.FactorName, ValueCode = rule.ValueCode, ValueName = rule.ValueName,
            MinimumValue = rule.MinimumValue, MaximumValue = rule.MaximumValue,
            Score = rule.Score, Remarks = rule.Remarks, RowVersion = rule.RowVersion
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.RiskScoringManage)]
    public Task<IActionResult> Add(RiskRuleEditViewModel model, CancellationToken cancellationToken)
    {
        if (model.Id != 0) ModelState.AddModelError("", "Invalid new record.");
        return SaveRule(model, cancellationToken);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.RiskScoringManage)]
    public Task<IActionResult> Edit(RiskRuleEditViewModel model, CancellationToken cancellationToken)
    {
        if (model.Id <= 0) ModelState.AddModelError("", "Invalid record.");
        return SaveRule(model, cancellationToken);
    }

    private async Task<IActionResult> SaveRule(RiskRuleEditViewModel model, CancellationToken ct)
    {
        try
        {
            var setup = await riskScoring.GetSetupAsync(model.FactorCode, null, 1, 10, ct);
            if (setup.SelectedFactor?.FactorCode != model.FactorCode) return NotFound();
            // Do not trust editable client IDs or factor metadata from a browser.
            model.ValueType = setup.SelectedFactor.ValueType;
            model.ValueDescription = setup.SelectedFactor.FactorName;
            if (!ModelState.IsValid) return View("Form", model);
            await riskScoring.SaveRuleAsync(new RiskRuleInput
            {
                Id = model.Id, FactorCode = model.FactorCode, ValueCode = model.ValueCode,
                ValueName = model.ValueName, MinimumValue = model.MinimumValue, MaximumValue = model.MaximumValue,
                Score = model.Score, Remarks = model.Remarks, RowVersion = model.RowVersion
            }, ct);
            TempData["SuccessMessage"] = "Saved for review. Select Apply reviewed changes when ready.";
            return RedirectToAction(nameof(Index), new { factorCode = model.FactorCode });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ErrorMessage(ex));
            return View("Form", model);
        }
    }


    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.RiskScoringManage)]
    public async Task<IActionResult> Delete(int id, byte[] rowVersion, string factorCode, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid) throw new RiskConfigurationException("Reload the list and try again.");
            await riskScoring.DeleteRuleAsync(id, rowVersion, cancellationToken);
            TempData["SuccessMessage"] = "Score removed from pending changes. Apply the changes after review.";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = ErrorMessage(ex); }
        return RedirectToAction(nameof(Index), new { factorCode });
    }

    // Publication is separate from editing to preserve the document's controlled-change workflow.
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.RiskScoringManage)]
    public async Task<IActionResult> Apply(string factorCode, CancellationToken cancellationToken)
    {
        try
        {
            await riskScoring.ActivateRulesAsync(factorCode, cancellationToken);
            TempData["SuccessMessage"] = "Reviewed scores applied to new evaluations.";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = ErrorMessage(ex); }
        return RedirectToAction(nameof(Index), new { factorCode });
    }

    private string ErrorMessage(Exception ex)
    {
        if (ex is RiskConfigurationException) return ex.Message;
        logger.LogError(ex, "Risk score operation failed.");
        return "Unable to save. Please reload or contact your administrator.";
    }
}
