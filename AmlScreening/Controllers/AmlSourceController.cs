using AmlScreening.Application.Interface.Provider;
using AmlScreening.Application.Security;
using AmlScreening.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers
{

    [Authorize(Policy = AppPermissions.AmlSourceView)]
    public class AmlSourceController : Controller
    {
        private readonly IAmlSourceSyncService _amlSyncService;

        public AmlSourceController(IAmlSourceSyncService amlSyncService)
        {
            _amlSyncService = amlSyncService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.AmlSourceSync)]
        public async Task<IActionResult> SyncUN(string sourcecode)
        {
            var validSourceCodes = sourcecode.Trim().ToUpperInvariant();
            if (validSourceCodes is not ("UN" or "EU"))
            {
                TempData["ErrorMessage"] =
                "The selected AML source is invalid.";

                return RedirectToAction(nameof(Index));
            }
            try
            {
                var count = await _amlSyncService.SyncAsync(validSourceCodes);
                TempData["SuccessMessage"] = $"{count}{validSourceCodes} UN records synchronized successfully.";
            }
            catch (Exception ex)
            {
                TempData["SuccessMessage"] = $"UN synchronization failed: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

    }
}
