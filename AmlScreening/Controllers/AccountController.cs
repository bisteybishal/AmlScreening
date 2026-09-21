using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Security;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;


namespace AmlScreening.Portal.Controllers;

public class AccountController : Controller
{
    private readonly IUserSessionRepository _sessionRepository;
    private readonly IAuthenticationRepository _authenticationRepository;
    private readonly IErrorLogService _errorLogService;
    private readonly PasswordHasher<object> _passwordHasher = new();

    public AccountController(IUserSessionRepository sessionRepository, IAuthenticationRepository authenticationRepository, IErrorLogService errorLogService)
    {
        _sessionRepository = sessionRepository;
        _authenticationRepository = authenticationRepository;
        _errorLogService = errorLogService;
    }
 [AllowAnonymous]
    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);
		 string? partnerCode = string.Empty;
        try
        {
            var loginuser = await _authenticationRepository.GetByUsernameAsync(model.UserName.Trim());
            if (loginuser == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid username or password.");

                return View(model);
            }
			partnerCode = loginuser.PartnerCode;
            var passwordResult = _passwordHasher.VerifyHashedPassword(null!, loginuser.PasswordHash, model.Password);

            if (passwordResult == PasswordVerificationResult.Failed)
            {
                await _authenticationRepository.RecordFailedLoginAsync(loginuser.UserId);

                ModelState.AddModelError(
                    string.Empty,
                    "Invalid username or password.");

                return View(model);
            }
            if (!loginuser.Permissions.Contains(AppPermissions.Portal.Access, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Portal access is not enabled.");

                return View(model);
            }
            var sessionId =
               Guid.NewGuid();


            var ipAddress =
                HttpContext
                    .Connection
                    .RemoteIpAddress?
                    .ToString()
                ?? "unknown";


            var userAgent =
                Request.Headers
                    .UserAgent
                    .ToString();


            var sessionResult =
                await _sessionRepository.StartAsync(
                    sessionId,
                    loginuser.UserId,
                    loginuser.PartnerId,
                    loginuser.BranchId,
                    ipAddress,
                    userAgent);


            if (sessionResult == -2)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "This user already has an active session.");

                return View(model);
            }


            if (sessionResult != 1)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Unable to start login session.");

                return View(model);
            }


            var claims =
                new List<Claim>
                {
                new(
                    ClaimTypes.NameIdentifier,
                    loginuser.UserId.ToString()),

                new(
                    ClaimTypes.Name,
                    loginuser.Username),

                new(
                    AppClaimTypes.PartnerId,
                    loginuser.PartnerId.ToString()),

                new(
                    AppClaimTypes.BranchId,
                    loginuser.BranchId.ToString()),
					 new(
        AppClaimTypes.PartnerCode,
        loginuser.PartnerCode),

                new(
                    AppClaimTypes.PartnerName,
                    loginuser.PartnerName),

                new(
                    AppClaimTypes.BranchName,
                    loginuser.BranchName),

                new(
                    AppClaimTypes.SessionId,
                    sessionId.ToString("D"))
                };


            foreach (var role in loginuser.Roles)
            {
                claims.Add(
                    new Claim(
                        ClaimTypes.Role,
                        role));
            }


            foreach (var permission
                     in loginuser.Permissions)
            {
                claims.Add(
                    new Claim(
                        AppPermissions.ClaimType,
                        permission));
            }


            var identity =
                new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);


            var principal =
                new ClaimsPrincipal(identity);


            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,

                    AllowRefresh = true,

                    ExpiresUtc =
                        DateTimeOffset.UtcNow
                            .AddHours(8)
                });


            return RedirectToAction(
                "Index",
                "Dashboard");
        }
        catch (Exception ex)
        {
            var errorId =
                await _errorLogService.LogAsync(
                    ex,
                    nameof(AccountController),
                    nameof(Login),
                    partnerCode);

            ModelState.AddModelError(
                string.Empty,
                errorId.HasValue
                    ? $"Unable to sign in. Error reference: {errorId}"
                    : "Unable to sign in.");

            return View(model);
        }
    }


    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        try
        {

            var userIdValue =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            var sessionValue =
                User.FindFirstValue(
                    AppClaimTypes.SessionId);


            if (long.TryParse(
                    userIdValue,
                    out var userId)
                &&
                Guid.TryParse(
                    sessionValue,
                    out var sessionId))
            {
                await _sessionRepository
                    .EndAsync(
                        sessionId,
                        userId);
            }


            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme);


            return RedirectToAction(
                nameof(Login));
        }
        catch (Exception ex)
        {
            await _errorLogService.LogAsync(
                ex,
                nameof(AccountController),
                nameof(Logout),
                User.FindFirstValue(
                    AppClaimTypes.PartnerCode));

            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme);

            return RedirectToAction(
                nameof(Login));
        }
    }
}
