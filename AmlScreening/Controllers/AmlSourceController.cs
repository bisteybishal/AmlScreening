using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Provider;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Application.Services;
using AmlScreening.Portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AmlScreening.Portal.Controllers
{

    [Authorize(Policy = AppPermissions.AmlSource.View)]
    public sealed class AmlSourceController : Controller
    {
        private readonly IAmlSourceSyncService _amlSourceSyncService;
        private readonly IErrorLogService _errorLogService;

        public AmlSourceController(
            IAmlSourceSyncService amlSourceSyncService,
            IErrorLogService errorLogService)
        {
            _amlSourceSyncService = amlSourceSyncService;
            _errorLogService = errorLogService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.AmlSource.Sync)]
        public async Task<IActionResult> Sync(
        string? sourceCode,
        CancellationToken cancellationToken = default)
        {
            var normalizedSourceCode =
                sourceCode?
                    .Trim()
                    .ToUpperInvariant()
                ?? string.Empty;


            if (normalizedSourceCode
                is not ("UN" or "EU"))
            {
                TempData["ErrorMessage"] =
                    "The selected AML source is invalid.";

                return RedirectToAction(
                    nameof(Index));
            }


            try
            {
                var count =
                    await _amlSourceSyncService
                        .SyncAsync(
                            normalizedSourceCode,
                            cancellationToken);


                TempData["SuccessMessage"] =
                    $"{count:N0} {normalizedSourceCode} record(s) synchronized successfully.";
            }
            catch (OperationCanceledException)
                when (
                    cancellationToken
                        .IsCancellationRequested)
            {
                TempData["ErrorMessage"] =
                    "AML source synchronization was cancelled.";
            }
            catch (Exception ex)
            {
                var errorId =
                    await LogErrorAsync(
                        ex,
                        nameof(Sync));


                TempData["ErrorMessage"] =
                    errorId.HasValue
                        ? $"AML source synchronization failed. Error reference: {errorId}"
                        : "AML source synchronization failed.";
            }


            return RedirectToAction(
                nameof(Index));
        }

      
        private Task<long?> LogErrorAsync(
        Exception exception,
        string action)
        {
            return _errorLogService
                .LogAsync(
                    exception,
                    nameof(AmlSourceController),
                    action,
                    User.FindFirstValue(
                        AppClaimTypes.PartnerCode));
        }

    }
}
