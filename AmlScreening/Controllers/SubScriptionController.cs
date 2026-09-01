using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.SubscriptionView)]
public sealed class SubscriptionController : Controller
{
    private const int PageSize = 6;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(
        ISubscriptionRepository subscriptionRepository,
        ILogger<SubscriptionController> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        try
        {
            var requestedPage = Math.Max(1, page);
            var result = await _subscriptionRepository.GetPageAsync(requestedPage, PageSize);
            var totalPages = Math.Max(1, result.TotalPages);
            var currentPage = Math.Clamp(requestedPage, 1, totalPages);
            if (currentPage != requestedPage)
                result = await _subscriptionRepository.GetPageAsync(currentPage, PageSize);

            return View(new SubscriptionPageViewModel
            {
                Plans = result.Items,
                Page = currentPage,
                PageSize = PageSize,
                TotalRecords = result.TotalCount,
                TotalPages = totalPages
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load subscription plans.");
            return View(new SubscriptionPageViewModel
            {
                ErrorMessage = "Subscription plans could not be loaded. Please try again later."
            });
        }
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.SubscriptionCreate)]
    public IActionResult Add() => View(new SubscriptionAddModel());

    [HttpPost]
    [Authorize(Policy = AppPermissions.SubscriptionCreate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(SubscriptionAddModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var planId = await _subscriptionRepository.CreateSubscriptionAsync(new SubscriptionPlan
            {
                SourceCode = model.SourceCode.Trim(),
                SourceName = model.SourceName.Trim(),
                Rate = model.Rate,
                DurationMonths = model.DurationMonths,
                IsActive = model.IsActive
            });
            TempData["SuccessMessage"] = $"Subscription plan {planId} added successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to create subscription plan {SourceCode}.", model.SourceCode);
            ModelState.AddModelError(string.Empty, "The subscription plan could not be created. Please try again.");
            return View(model);
        }
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.SubscriptionDelete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, byte[] rowVersion)
    {
        try
        {
            if (!await _subscriptionRepository.DeleteAsync(id, rowVersion))
            {
                TempData["ErrorMessage"] = "The plan was changed or already deleted. Refresh and try again.";
            }
            else
            {
                TempData["SuccessMessage"] = "Subscription plan deleted successfully.";
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to soft delete subscription plan {PlanId}.", id);
            TempData["ErrorMessage"] = "The subscription plan could not be deleted. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.SubscriptionEdit)]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var plan = await _subscriptionRepository.GetByIdAsync(id);
            if (plan is null)
                return NotFound();

            return View(new SubscriptionAddModel
            {
                PlanId = plan.PlanId,
                RowVersion = plan.RowVersion,
                SourceCode = plan.SourceCode,
                SourceName = plan.SourceName,
                Rate = plan.Rate,
                DurationMonths = plan.DurationMonths,
                IsActive = plan.IsActive
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load subscription plan {PlanId}.", id);
            TempData["ErrorMessage"] = "The subscription plan could not be loaded.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.SubscriptionEdit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SubscriptionAddModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var updated = await _subscriptionRepository.UpdateAsync(new SubscriptionPlan
            {
                PlanId = model.PlanId,
                RowVersion = model.RowVersion,
                SourceCode = model.SourceCode.Trim(),
                SourceName = model.SourceName.Trim(),
                Rate = model.Rate,
                DurationMonths = model.DurationMonths,
                IsActive = model.IsActive
            });

            if (!updated)
            {
                ModelState.AddModelError(string.Empty,
                    "This plan was changed by another user. Reload it and try again.");
                return View(model);
            }

            TempData["SuccessMessage"] = "Subscription plan updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to update subscription plan {PlanId}.", model.PlanId);
            ModelState.AddModelError(string.Empty, "The subscription plan could not be updated. Please try again.");
            return View(model);
        }
    }
}
