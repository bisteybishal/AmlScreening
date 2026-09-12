using AmlScreening.Application.Interface;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities.Compliance;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.AlertsView)]
public sealed class AlertsController : Controller
{
    private readonly IAmlWorkflowService _workflow;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(IAmlWorkflowService workflow, ILogger<AlertsController> logger)
    {
        _workflow = workflow;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try { return View(await _workflow.GetAlertsAsync()); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load compliance alerts.");
            ViewBag.ErrorMessage = "Alerts could not be loaded.";
            return View(Array.Empty<ComplianceAlert>());
        }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.AlertsManage)]
    public async Task<IActionResult> Update(AlertUpdateModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Select a valid alert status.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            if (!await _workflow.UpdateAlertAsync(model.Id, model.Status, model.Resolution))
                return NotFound();
            TempData["SuccessMessage"] = "Alert updated successfully.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to update alert {AlertId}.", model.Id);
            TempData["ErrorMessage"] = "Alert could not be updated.";
        }
        return RedirectToAction(nameof(Index));
    }
}
