using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace AmlScreening.Portal.Services;

public sealed class PortalCookieEvents
    : CookieAuthenticationEvents
{
    private readonly IUserSessionRepository
        _sessionRepository;


    public PortalCookieEvents(
        IUserSessionRepository sessionRepository)
    {
        _sessionRepository =
            sessionRepository;
    }


    public override async Task ValidatePrincipal(
        CookieValidatePrincipalContext context)
    {
        var userIdValue =
            context.Principal?
                .FindFirstValue(
                    ClaimTypes.NameIdentifier);

        var partnerValue =
            context.Principal?
                .FindFirstValue(
                    AppClaimTypes.PartnerId);

        var branchValue =
            context.Principal?
                .FindFirstValue(
                    AppClaimTypes.BranchId);

        var sessionValue =
            context.Principal?
                .FindFirstValue(
                    AppClaimTypes.SessionId);


        if (!long.TryParse(
                userIdValue,
                out var userId)

            ||

            !int.TryParse(
                partnerValue,
                out var partnerId)

            ||

            !int.TryParse(
                branchValue,
                out var branchId)

            ||

            !Guid.TryParse(
                sessionValue,
                out var sessionId))
        {
            await RejectAsync(context);
            return;
        }


        var ipAddress =
            context.HttpContext
                .Connection
                .RemoteIpAddress?
                .ToString()
            ?? "unknown";


        var valid =
            await _sessionRepository
                .ValidateAsync(
                    sessionId,
                    userId,
                    partnerId,
                    branchId,
                    ipAddress);


        if (!valid)
        {
            await RejectAsync(context);
        }
    }


    private static async Task RejectAsync(
        CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();

        await context.HttpContext
            .SignOutAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme);
    }
}