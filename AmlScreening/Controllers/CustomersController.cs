using AmlScreening.Application.Interface;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities.Compliance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.Customers.View)]
public sealed class CustomersController : Controller
{
    private readonly IAmlWorkflowService _workflow;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        IAmlWorkflowService workflow,
        ILogger<CustomersController> logger)
    {
        _workflow = workflow;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try { return View(await _workflow.GetCustomersAsync()); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load customers.");
            ViewBag.ErrorMessage = "Customers could not be loaded.";
            return View(Array.Empty<Customer>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var customer = await _workflow.GetCustomerAsync(id);
            return customer is null ? NotFound() : View(customer);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load customer {CustomerId}.", id);
            TempData["ErrorMessage"] = "Customer details could not be loaded.";
            return RedirectToAction(nameof(Index));
        }
    }
}
