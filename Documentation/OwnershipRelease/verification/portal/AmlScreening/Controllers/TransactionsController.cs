using AmlScreening.Application.Interface;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities.Compliance;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.TransactionsView)]
public sealed class TransactionsController : Controller
{
    private readonly IAmlWorkflowService _workflow;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        IAmlWorkflowService workflow,
        ILogger<TransactionsController> logger)
    {
        _workflow = workflow;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try { return View(await _workflow.GetTransactionsAsync()); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load AML transactions.");
            ViewBag.ErrorMessage = "Transactions could not be loaded.";
            return View(Array.Empty<AmlTransaction>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var details = await _workflow.GetTransactionDetailsAsync(id);
            return details is null ? NotFound() : View(new TransactionDetailsViewModel { Details = details });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load transaction {TransactionId}.", id);
            TempData["ErrorMessage"] = "Transaction details could not be loaded.";
            return RedirectToAction(nameof(Index));
        }
    }

}
