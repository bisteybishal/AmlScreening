using AmlScreening.Application.Interface.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AmlScreening.Portal.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IAmlRecordRepository _amlRecordRepository;

        public DashboardController(IAmlRecordRepository amlRecordRepository)
        {
            _amlRecordRepository = amlRecordRepository;
        }

        [HttpGet]
        
        public async Task<IActionResult> Index()
        {
            ViewBag.FullName =
                User.FindFirst("FullName")?.Value
                ?? User.Identity?.Name
                ?? "User";

            ViewBag.Username =
                User.FindFirst("Username")?.Value
                ?? User.Identity?.Name
                ?? "User";

            ViewBag.RoleName =
                User.FindFirst(ClaimTypes.Role)?.Value
                ?? "User";

            var record = await _amlRecordRepository.GetAllAsync();
            ViewBag.totalrecords = record.Count;
            ViewBag.activerecord = record.Count(r => r.IsActive);

            return View(record);
        }
    }
}
