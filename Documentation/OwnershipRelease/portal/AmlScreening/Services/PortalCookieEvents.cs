using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace AmlScreening.Portal.Services;

public sealed class PortalCookieEvents(IUserSessionRepository sessions, IUserRepository users,
    ILogger<PortalCookieEvents> logger) : CookieAuthenticationEvents
{
    public const string SessionClaim = "PortalSessionId";
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (!long.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0 ||
            !int.TryParse(principal?.FindFirstValue(AppClaimTypes.ClientId), out var clientId) || clientId <= 0 ||
            !Guid.TryParse(principal?.FindFirstValue(SessionClaim), out var sessionId))
        {
            await RejectAsync(context);
            return;
        }
        try
        {
            if (!(await sessions.ValidateAsync(userId, clientId, sessionId)).Success)
            {
                await RejectAsync(context);
                return;
            }
            var account = await users.GetUserAsync(principal!.Identity?.Name ?? "");
            if (account is null || !account.IsActive || account.Id != userId || account.ClientId != clientId ||
                !account.CanAccessPortal)
            {
                await RejectAsync(context);
                return;
            }
            // Refresh role/permission claims for THIS request; revoked privileges do not
            // remain usable until the person next logs in. Session ID/ownership stay unchanged.
            var claims = principal.Claims.Where(c => c.Type != AppPermissions.ClaimType && c.Type != ClaimTypes.Role).ToList();
            claims.Add(new Claim(ClaimTypes.Role, account.Role));
            foreach (var permission in await users.GetPermissionsAsync(userId))
                claims.Add(new Claim(AppPermissions.ClaimType, permission));
            context.ReplacePrincipal(new ClaimsPrincipal(new ClaimsIdentity(claims,
                CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role)));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Portal session validation unavailable for account {UserId}.", userId);
            await RejectAsync(context);
        }
    }
    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
