using AmlScreening.Application.Interface.Provider;
using AmlScreening.Application.Security;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers
{

    [Authorize(Policy = AppPermissions.AmlSourceView)]
    public sealed class AmlSourceController : Controller
    {
        private readonly IAmlSourceSyncService _amlSyncService;
        private readonly IAuditService _auditService;
        private readonly ILogger<AmlSourceController> _logger;

        public AmlSourceController(
            IAmlSourceSyncService amlSyncService,
            IAuditService auditService,
            ILogger<AmlSourceController> logger)
        {
            _amlSyncService = amlSyncService;
            _auditService = auditService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.AmlSourceSync)]
        public async Task<IActionResult> Sync(string? sourceCode, CancellationToken cancellationToken)
        {
            var normalizedSourceCode = sourceCode?.Trim().ToUpperInvariant();
            if (normalizedSourceCode is not ("UN" or "EU"))
            {
                TempData["ErrorMessage"] = "The selected AML source is invalid.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var count = await _amlSyncService.SyncAsync(normalizedSourceCode);
                TempData["SuccessMessage"] =
                    $"{count:N0} {normalizedSourceCode} record(s) synchronized successfully.";

                await TryWriteAuditAsync(
                    new AuditEvent(
                        "Synchronize",
                        "AmlSource",
                        normalizedSourceCode,
                        $"Synchronized {count} record(s)."),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AML source synchronization failed for {SourceCode}.",
                    normalizedSourceCode);
                TempData["ErrorMessage"] =
                    $"{normalizedSourceCode} synchronization failed. Review the server log and try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task TryWriteAuditAsync(
            AuditEvent auditEvent,
            CancellationToken cancellationToken)
        {
            try
            {
                await _auditService.WriteAsync(auditEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AML source synchronization succeeded, but its audit entry could not be written.");
            }
        }

    }
}
