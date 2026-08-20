using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AmlScreening.Portal.Controllers
{
    public class DashboardController : Controller
    {
        [HttpGet]
        
        public IActionResult Index()
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

            return View();
        }
    }
}
