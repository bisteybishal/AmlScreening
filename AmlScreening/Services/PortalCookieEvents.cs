using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace AmlScreening.Portal.Services;

public sealed class PortalCookieEvents : CookieAuthenticationEvents
{
    private readonly IUserSessionRepository _userSessionRepository;

    public const string SessionClaim = "PortalSessionId";

    public PortalCookieEvents(
        IUserSessionRepository userSessionRepository)
    {
        _userSessionRepository = userSessionRepository;
    }

    public override async Task ValidatePrincipal(
        CookieValidatePrincipalContext context)
    {
        var user = context.Principal;

        var validuserId =
            long.TryParse(
                user?.FindFirstValue(
                    ClaimTypes.NameIdentifier),
                out var userId);
        var validclientId =
        int.TryParse(
            user?.FindFirstValue(
                AppClaimTypes.ClientId),
            out var clientId);
           var validsessionId=
            Guid.TryParse(
                user?.FindFirstValue(
                    SessionClaim),
                out var sessionId);

        if (!validuserId||!validclientId||!validsessionId)
        {
            await RejectSessionAsync(context);
            return;
        }

        var result =
            await _userSessionRepository.ValidateAsync(
                userId,
                clientId,
                sessionId);

        if (!result.Success)
        {
            await RejectSessionAsync(context);
            return;
        }
    }

    private static async Task RejectSessionAsync(
        CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();

        await context.HttpContext.SignOutAsync(
            CookieAuthenticationDefaults
                .AuthenticationScheme);
    }
}