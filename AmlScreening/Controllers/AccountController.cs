using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Runtime.InteropServices;
using System.Security.Claims;
using AppAuthenticationService = AmlScreening.Application.Interface.IAuthenticationService;
using AmlScreening.Application.Security;

namespace AmlScreening.Portal.Controllers;

public class AccountController : Controller
{
    private readonly AppAuthenticationService _authenticationService;
    private readonly AmlScreening.Application.Interface.IAuditService _auditService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        AppAuthenticationService authenticationService,
        AmlScreening.Application.Interface.IAuditService auditService,
        ILogger<AccountController> logger)
    {
        _authenticationService = authenticationService;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult Login() => View(new LoginViewModel());

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [EnableRateLimiting("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        AmlScreening.Application.Models.LoginResult result;
        try
        {
            result = await _authenticationService.LoginAsync(model.UserName, model.Password);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Login processing failed for username {Username}.", model.UserName);
            ModelState.AddModelError(string.Empty,
                "Login is temporarily unavailable. Please try again later.");
            return View(model);
        }
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(AppClaimTypes.ClientId, result.ClientId.ToString()),
            new(ClaimTypes.Name, result.Username ?? string.Empty),
            new("FullName", result.FullName ?? string.Empty),
            new(ClaimTypes.Role, result.Role ?? string.Empty)
        };
        claims.AddRange(result.Permissions.Select(permission =>
            new Claim(AppPermissions.ClaimType, permission)));
        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                AllowRefresh = true,
                ExpiresUtc = model.RememberMe
                    ? DateTimeOffset.UtcNow.AddHours(8)
                    : null
            });
        HttpContext.User = principal;
        try
        {
            await _auditService.WriteAsync(new(
                "Login", "Session", result.UserId.ToString(), "Successful portal login."));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to write login audit for user {UserId}.", result.UserId);
        }

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await _auditService.WriteAsync(new(
                "Logout", "Session", User.FindFirstValue(ClaimTypes.NameIdentifier), "Portal logout."));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to write logout audit.");
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [HttpGet]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
