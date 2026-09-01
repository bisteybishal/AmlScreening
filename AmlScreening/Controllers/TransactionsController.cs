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

    [HttpGet, Authorize(Policy = AppPermissions.TransactionsCreate)]
    public async Task<IActionResult> Add() => View(await PopulateCustomersAsync(new TransactionInputModel()));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.TransactionsCreate)]
    public async Task<IActionResult> Add(TransactionInputModel model)
    {
        if (model.TransactionDate > DateTime.Now.AddMinutes(5))
            ModelState.AddModelError(nameof(model.TransactionDate), "Transaction date cannot be in the future.");
        if (!ModelState.IsValid) return View(await PopulateCustomersAsync(model));

        try
        {
            var id = await _workflow.CreateTransactionAsync(new AmlTransaction
            {
                TransactionReference = model.TransactionReference.Trim(),
                CustomerId = model.CustomerId,
                SenderName = model.SenderName.Trim(),
                ReceiverName = model.ReceiverName.Trim(),
                Amount = model.Amount,
                Currency = model.Currency.Trim().ToUpperInvariant(),
                OriginCountry = model.OriginCountry.Trim(),
                DestinationCountry = model.DestinationCountry.Trim(),
                Channel = model.Channel.Trim(),
                TransactionDate = model.TransactionDate.ToUniversalTime()
            });

            if (model.EvaluateImmediately &&
                User.HasClaim(AppPermissions.ClaimType, AppPermissions.TransactionsEvaluate))
            {
                await _workflow.EvaluateAsync(id);
            }

            TempData["SuccessMessage"] = model.EvaluateImmediately
                ? "Transaction saved and evaluated."
                : "Transaction saved successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to create transaction {Reference}.", model.TransactionReference);
            ModelState.AddModelError(string.Empty,
                "Transaction could not be saved. Check that the reference is unique.");
            return View(await PopulateCustomersAsync(model));
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

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AppPermissions.TransactionsEvaluate)]
    public async Task<IActionResult> Evaluate(int id)
    {
        try
        {
            if (await _workflow.EvaluateAsync(id) is null) return NotFound();
            TempData["SuccessMessage"] = "AML evaluation completed.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "AML evaluation failed for transaction {TransactionId}.", id);
            TempData["ErrorMessage"] = "AML evaluation failed. Review the rule and AML data configuration.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<TransactionInputModel> PopulateCustomersAsync(TransactionInputModel model)
    {
        model.Customers = await _workflow.GetCustomersAsync();
        return model;
    }
}
