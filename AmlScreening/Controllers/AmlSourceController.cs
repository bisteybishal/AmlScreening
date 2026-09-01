using AmlScreening.Application.Security;
using AmlScreening.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers
{

    [Authorize(Policy = AppPermissions.AmlSourceView)]
    public class AmlSourceController : Controller
    {
        private readonly UnSyncService _unSyncService;

        public AmlSourceController(UnSyncService unSyncService)
        {
            _unSyncService = unSyncService;
        }
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncUN()
        {
            try
            {
                var count = await _unSyncService.SyncAsync();
                TempData["SuccessMessage"] = $"{count} UN records synchronized successfully.";
            }
            catch(Exception ex)
            {
                TempData["SuccessMessage"] = $"UN synchronization failed: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
