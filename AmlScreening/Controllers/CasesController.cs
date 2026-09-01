using AmlScreening.Application.Interface;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities.Compliance;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.CasesView)]
public sealed class CasesController : Controller
{
    private readonly IAmlWorkflowService _workflow;
    private readonly ILogger<CasesController> _logger;

    public CasesController(IAmlWorkflowService workflow, ILogger<CasesController> logger)
    {
        _workflow = workflow;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try { return View(await _workflow.GetCasesAsync()); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load compliance cases.");
            ViewBag.ErrorMessage = "Cases could not be loaded.";
            return View(Array.Empty<ComplianceCase>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var complianceCase = await _workflow.GetCaseAsync(id);
        return complianceCase is null ? NotFound() : View(complianceCase);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.CasesManage)]
    public async Task<IActionResult> Update(CaseUpdateModel model)
    {
        if (!ModelState.IsValid) return RedirectToAction(nameof(Details), new { id = model.Id });
        try
        {
            if (!await _workflow.UpdateCaseAsync(model.Id, model.Status, model.Disposition, model.Notes))
                return NotFound();
            TempData["SuccessMessage"] = "Case updated successfully.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to update case {CaseId}.", model.Id);
            TempData["ErrorMessage"] = "Case could not be updated.";
        }
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }
}
