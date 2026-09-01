using AmlScreening.Application.Interface;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities.Compliance;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.CustomersView)]
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

    [HttpGet, Authorize(Policy = AppPermissions.CustomersCreate)]
    public IActionResult Add() => View(new CustomerInputModel());

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.CustomersCreate)]
    public async Task<IActionResult> Add(CustomerInputModel model)
    {
        if (model.DateOfBirth > DateTime.Today)
            ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth cannot be in the future.");
        if (!ModelState.IsValid) return View(model);

        try
        {
            await _workflow.CreateCustomerAsync(ToEntity(model));
            TempData["SuccessMessage"] = "Customer added successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to add customer {Reference}.", model.CustomerReference);
            ModelState.AddModelError(string.Empty, "Customer could not be saved. Check that the reference is unique.");
            return View(model);
        }
    }

    [HttpGet, Authorize(Policy = AppPermissions.CustomersEdit)]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _workflow.GetCustomerAsync(id);
        return customer is null ? NotFound() : View(FromEntity(customer));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.CustomersEdit)]
    public async Task<IActionResult> Edit(CustomerInputModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            if (!await _workflow.UpdateCustomerAsync(ToEntity(model)))
            {
                ModelState.AddModelError(string.Empty,
                    "This customer was changed by another user. Reload and try again.");
                return View(model);
            }
            TempData["SuccessMessage"] = "Customer updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to update customer {CustomerId}.", model.Id);
            ModelState.AddModelError(string.Empty, "Customer could not be updated.");
            return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.CustomersDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            if (!await _workflow.DeleteCustomerAsync(id)) return NotFound();
            TempData["SuccessMessage"] = "Customer deleted successfully.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to delete customer {CustomerId}.", id);
            TempData["ErrorMessage"] = "Customer could not be deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    private static Customer ToEntity(CustomerInputModel model) => new()
    {
        Id = model.Id,
        RowVersion = model.RowVersion,
        CustomerReference = model.CustomerReference.Trim(),
        FullName = model.FullName.Trim(),
        DateOfBirth = model.DateOfBirth,
        Country = model.Country.Trim(),
        Nationality = model.Nationality.Trim(),
        Occupation = model.Occupation.Trim(),
        SourceOfFunds = model.SourceOfFunds.Trim(),
        IsPep = model.IsPep,
        IsActive = model.IsActive
    };

    private static CustomerInputModel FromEntity(Customer customer) => new()
    {
        Id = customer.Id,
        RowVersion = customer.RowVersion,
        CustomerReference = customer.CustomerReference,
        FullName = customer.FullName,
        DateOfBirth = customer.DateOfBirth,
        Country = customer.Country,
        Nationality = customer.Nationality,
        Occupation = customer.Occupation,
        SourceOfFunds = customer.SourceOfFunds,
        IsPep = customer.IsPep,
        IsActive = customer.IsActive
    };
}
