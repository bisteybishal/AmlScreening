
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using AppAuthenticationService =
    AmlScreening.Application.Interface.Services.IAuthenticationService;


namespace AmlScreening.Portal.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppAuthenticationService _authenticationService;

        public AccountController(AppAuthenticationService authenticationService)
        {
            _authenticationService = authenticationService;
        }


        [HttpGet]
        public IActionResult Login()
        {

            return View(new LoginViewModel());
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model)
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
            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    result.UserId.ToString()),

                new Claim(
                    ClaimTypes.Name,
                    result.Username ?? string.Empty),

                new Claim(
                    "FullName",
                    result.FullName ?? string.Empty),

                new Claim(
                    ClaimTypes.Role,
                    result.Role ?? string.Empty)
            };
            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
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

