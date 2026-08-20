
using AmlScreening.Application.Interface.Services;
using AmlScreening.Portal.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SecurityClaim = System.Security.Claims.Claim;
using SecurityClaimTypes = System.Security.Claims.ClaimTypes;

namespace AmlScreening.Portal.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthenticationService _authenticationService;

        public AccountController(IAuthenticationService authenticationService)
        {
            _authenticationService = authenticationService;
        }


        [HttpGet]
        public IActionResult Login()
        {

            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            UserModel model)
        {

            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var result = await _authenticationService.LoginAsync(model.UserName, model.Password);
            if (!result.Success)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.Message);

                return View(model);
            }

            var claims = new List<SecurityClaim>
{
    new SecurityClaim(
        SecurityClaimTypes.NameIdentifier,
        result.UserId.ToString()),

    new SecurityClaim(
        SecurityClaimTypes.Name,
        result.Username ?? string.Empty),

    new SecurityClaim(
        "FullName",
        result.FullName ?? string.Empty),

    new SecurityClaim(
        SecurityClaimTypes.Role,
        result.Role ?? string.Empty)
};
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                 CookieAuthenticationDefaults
                     .AuthenticationScheme,
                 principal);

            return RedirectToAction(
                "Index",
                "Dashboard");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme);

            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}

