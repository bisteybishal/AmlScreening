using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Portal.Services;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using AppAuthenticationService = AmlScreening.Application.Interface.IAuthenticationService;


namespace AmlScreening.Portal.Controllers;

public class AccountController : Controller
{
    private readonly AppAuthenticationService _authenticationService;
    private readonly AmlScreening.Application.Interface.IAuditService _auditService;
    private readonly ILogger<AccountController> _logger;
    private readonly IUserSessionRepository _userRepository;

    public AccountController(
        AppAuthenticationService authenticationService,
        AmlScreening.Application.Interface.IAuditService auditService,
        ILogger<AccountController> logger, IUserSessionRepository userRepository)
    {
        _authenticationService = authenticationService;
        _auditService = auditService;
        _logger = logger;
        _userRepository = userRepository;
    }

    [HttpGet]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel());
    }

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [EnableRateLimiting("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        if (!ModelState.IsValid)
            return View(model);

        LoginResult result;
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
        var userid = result.UserId;
        var clientid = result.ClientId;
        if (userid <= 0 || clientid <= 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "Your account configuration is incomplete. Contact your administrator.");

            return View(model);
        }
        var sessionid = Guid.NewGuid();
        try
        {
            var sessionresult = await _userRepository.StartAsync(userid, clientid, sessionid);
            if (!sessionresult.Success)
            {
                ModelState.AddModelError(string.Empty,
                    "Your account is already signed in, or access is unavailable. " +
                    "Log out from the other browser or wait for the session to expire.");

                return View(model);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unable to reserve session for user {UserId}.",
                userid);


            await ReleaseSessionSafelyAsync(
                userid,
                clientid,
                sessionid);
            ModelState.AddModelError(
               string.Empty,
               "Login is temporarily unavailable. Please try again.");

            return View(model);

        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(AppClaimTypes.ClientId, result.ClientId.ToString()),
            new(ClaimTypes.Name, result.Username ?? string.Empty),
            new("FullName", result.FullName ?? string.Empty),
            new(ClaimTypes.Role, result.Role ?? string.Empty),
            new(
                PortalCookieEvents.SessionClaim,
                sessionid.ToString("D"))
        };
        foreach (var permission in result.Permissions
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.Ordinal))
        {
            claims.Add(
                new Claim(
                    AppPermissions.ClaimType,
                    permission));
        }


        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        try
        {
            await HttpContext.SignInAsync(
               CookieAuthenticationDefaults.AuthenticationScheme,
               principal,
               new AuthenticationProperties
               {
                   IsPersistent = model.RememberMe,
                    AllowRefresh = true
               });
        }


        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to issue login cookie for user {UserId}.",
                userid);

            await ReleaseSessionSafelyAsync(
                userid,
                clientid,
                sessionid);

            ModelState.AddModelError(
                string.Empty,
                "Login could not be completed. Please try again.");

            return View(model);
        }
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
        if (long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
            int.TryParse(User.FindFirstValue(AppClaimTypes.ClientId), out var clientId) &&
            Guid.TryParse(User.FindFirstValue(PortalCookieEvents.SessionClaim), out var sessionId))
            await ReleaseSessionSafelyAsync(userId, clientId, sessionId);
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
    private async Task ReleaseSessionSafelyAsync(
        long userId,
        int clientId,
        Guid sessionId)
    {
        try
        {
            await _userRepository.EndAsync(
                userId,
                clientId,
                sessionId);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to release session for user {UserId}.",
                userId);
        }
    }
}
