using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.Subscription.View)]
public sealed class SubscriptionController : Controller
{
    private const int PageSize = 6;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IErrorLogService _errorLogService;

    public SubscriptionController(
        ISubscriptionRepository subscriptionRepository,
         IErrorLogService errorLogService)
    {
        _subscriptionRepository = subscriptionRepository;
        _errorLogService = errorLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestedPage = Math.Max(1, page);
            var result = await _subscriptionRepository.GetPageAsync(requestedPage, PageSize, cancellationToken);
            var totalPages = Math.Max(1, result.TotalPages);
            var currentPage = Math.Clamp(requestedPage, 1, totalPages);
            if (currentPage != requestedPage)
                result = await _subscriptionRepository.GetPageAsync(currentPage, PageSize, cancellationToken);

            return View(new SubscriptionPageViewModel
            {
                Plans = result.Items,
                Page = currentPage,
                PageSize = PageSize,
                TotalRecords = result.TotalCount,
                TotalPages = totalPages
            });
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Index));


            return View(
                new SubscriptionPageViewModel
                {
                    ErrorMessage =
                        errorId.HasValue
                            ? $"Subscription plans could not be loaded. Error reference: {errorId}"
                            : "Subscription plans could not be loaded."
                });
        }
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.Subscription.Manage)]
    public IActionResult Add()
    {
        return View(
          new SubscriptionAddModel
          {
              IsActive = true
          });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.Subscription.Manage)]
    public async Task<IActionResult> Add(
        SubscriptionAddModel model,
        CancellationToken cancellationToken = default)
    {
        try
        {
            model.SourceCode =
                model.SourceCode?
                    .Trim()
                    .ToUpperInvariant()
                ?? string.Empty;


            model.SourceName =
                model.SourceName?
                    .Trim()
                ?? string.Empty;


            if (!ModelState.IsValid)
            {
                return View(model);
            }


            /*
             * Current database already has
             * SubscriptionKey / ExpireDate.
             *
             * Keep them temporarily.
             *
             * Later these should move to
             * PartnerSubscriptions because a
             * plan itself should not expire.
             */

            var createdAtUtc =
                DateTime.UtcNow;


            var plan =
                new SubscriptionPlan
                {
                    SourceCode =
                        model.SourceCode,

                    SourceName =
                        model.SourceName,

                    Rate =
                        model.Rate,

                    DurationMonths =
                        model.DurationMonths,

                    IsActive =
                        model.IsActive,

                    SubscriptionKey =
                        GenerateSubscriptionKey(),

                    ExpireDate =
                        createdAtUtc
                            .AddMonths(
                                model.DurationMonths)
                };


            var planId =
                await _subscriptionRepository
                    .CreateSubscriptionAsync(
                        plan,
                        cancellationToken);


            TempData["SuccessMessage"] =
                $"Subscription plan {planId} created successfully.";


            return RedirectToAction(
                nameof(Index));
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Add));


            ModelState.AddModelError(
                string.Empty,
                errorId.HasValue
                    ? $"Subscription plan could not be created. Error reference: {errorId}"
                    : "Subscription plan could not be created.");


            return View(model);
        }
    }


    // =========================================================
    // EDIT GET
    // =========================================================

    [HttpGet]
    [Authorize(Policy = AppPermissions.Subscription.Manage)]
    public async Task<IActionResult> Edit(
        int id)
    {
        try
        {
            if (id <= 0)
            {
                return NotFound();
            }


            var plan =
                await _subscriptionRepository
                    .GetByIdAsync(id);


            if (plan == null)
            {
                return NotFound();
            }


            var model =
                new SubscriptionAddModel
                {
                    PlanId =
                        plan.PlanId,

                    RowVersion =
                        plan.RowVersion,

                    SourceCode =
                        plan.SourceCode,

                    SourceName =
                        plan.SourceName,

                    Rate =
                        plan.Rate,

                    DurationMonths =
                        plan.DurationMonths,

                    IsActive =
                        plan.IsActive
                };


            return View(model);
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Edit));


            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Subscription plan could not be loaded. Error reference: {errorId}"
                    : "Subscription plan could not be loaded.";


            return RedirectToAction(
                nameof(Index));
        }
    }


    // =========================================================
    // EDIT POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.Subscription.Manage)]
    public async Task<IActionResult> Edit(
        SubscriptionAddModel model)
    {
        model.SourceCode =
            model.SourceCode?
                .Trim()
                .ToUpperInvariant()
            ?? string.Empty;


        model.SourceName =
            model.SourceName?
                .Trim()
            ?? string.Empty;


        if (model.PlanId <= 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "Invalid subscription plan.");
        }


        if (model.RowVersion is not
            { Length: > 0 })
        {
            ModelState.AddModelError(
                string.Empty,
                "The subscription plan version is missing. Reload and try again.");
        }


        if (!ModelState.IsValid)
        {
            return View(model);
        }


        try
        {
            var updated =
                await _subscriptionRepository
                    .UpdateAsync(
                        new SubscriptionPlan
                        {
                            PlanId =
                                model.PlanId,

                            RowVersion =
                                model.RowVersion,

                            SourceCode =
                                model.SourceCode,

                            SourceName =
                                model.SourceName,

                            Rate =
                                model.Rate,

                            DurationMonths =
                                model.DurationMonths,

                            IsActive =
                                model.IsActive
                        });


            if (!updated)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "This plan was changed by another user. Reload and try again.");

                return View(model);
            }


            TempData["SuccessMessage"] =
                "Subscription plan updated successfully.";


            return RedirectToAction(
                nameof(Index));
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Edit));


            ModelState.AddModelError(
                string.Empty,
                errorId.HasValue
                    ? $"Subscription plan could not be updated. Error reference: {errorId}"
                    : "Subscription plan could not be updated.");


            return View(model);
        }
    }


    // =========================================================
    // DELETE
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.Subscription.Manage)]
    public async Task<IActionResult> Delete(
        int id,
        byte[] rowVersion)
    {
        try
        {
            if (id <= 0 ||
                rowVersion is not { Length: > 0 })
            {
                TempData["ErrorMessage"] =
                    "Invalid subscription plan.";

                return RedirectToAction(
                    nameof(Index));
            }


            var deleted =
                await _subscriptionRepository
                    .DeleteAsync(
                        id,
                        rowVersion);


            if (!deleted)
            {
                TempData["ErrorMessage"] =
                    "The plan was changed or already deleted. Refresh and try again.";
            }
            else
            {
                TempData["SuccessMessage"] =
                    "Subscription plan deleted successfully.";
            }
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Delete));


            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Subscription plan could not be deleted. Error reference: {errorId}"
                    : "Subscription plan could not be deleted.";
        }


        return RedirectToAction(
            nameof(Index));
    }


    // =========================================================
    // KEY
    // =========================================================

    private static string GenerateSubscriptionKey()
    {
        /*
         * 32 random bytes:
         * much better than timestamp-based keys.
         */

        var bytes =
            RandomNumberGenerator
                .GetBytes(32);


        return Convert
            .ToHexString(bytes);
    }


    // =========================================================
    // ERROR LOG
    // =========================================================

    private Task<long?> LogErrorAsync(
        Exception exception,
        string action)
    {
        return _errorLogService
            .LogAsync(
                exception,
                nameof(SubscriptionController),
                action,
                User.FindFirstValue(
                    AppClaimTypes.PartnerCode));
    }
}
