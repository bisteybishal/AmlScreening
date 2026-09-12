using AmlScreening.Application.Interface.Provider;
using AmlScreening.Application.Security;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AmlScreening.Application.Interface.Repositories;

namespace AmlScreening.Portal.Controllers
{

    [Authorize(Policy = AppPermissions.AmlSourceView)]
    public sealed class AmlSourceController : Controller
    {
        private readonly IAmlSourceSyncService _amlSyncService;
        private readonly IAuditService _auditService;
        private readonly ILogger<AmlSourceController> _logger;
        private readonly IErrorRepository _errorRepository;

        public AmlSourceController(
            IAmlSourceSyncService amlSyncService,
            IAuditService auditService,
            ILogger<AmlSourceController> logger, IErrorRepository errorRepository)
        {
            _amlSyncService = amlSyncService;
            _auditService = auditService;
            _logger = logger;
            _errorRepository = errorRepository;
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
                var errorLog = new ErrorLogModel
                {
                    ErrorDescription = ex.Message,
                    PartnerCode = normalizedSourceCode,
                    ErrorNumber = ex.HResult.ToString(),
                    Script = "AmlSourceController.Sync",
                    ErrorScript = ex.StackTrace ?? string.Empty,
                    QueryString = Request.QueryString.ToString(),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                };
               await _errorRepository.LogErrorAsync(errorLog);
                
                TempData["ErrorMessage"] =
                    $"{normalizedSourceCode} {ex.Message}";
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
