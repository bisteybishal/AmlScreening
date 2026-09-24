using AmlScreening.Application.Interface;
using AmlScreening.Application.Security;
using System.Security.Claims;

namespace AmlScreening.Portal.Services;

public sealed class CurrentUserContext
    : ICurrentUserContext
{
    private readonly IHttpContextAccessor
        _httpContextAccessor;

    public CurrentUserContext(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor =
            httpContextAccessor;
    }

    private ClaimsPrincipal? User =>
        _httpContextAccessor
            .HttpContext?
            .User;

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated == true;

    public long UserId
    {
        get
        {
            var value =
                User?.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            return long.TryParse(
                value,
                out var id)
                ? id
                : 0;
        }
    }
    public bool IsAdmin => IsAuthenticated && User?.IsInRole(AppRoles.Admin)==true;
    

    public int PartnerId
    {
        get
        {
            var value =
                User?.FindFirstValue(
                    AppClaimTypes.PartnerId);

            return int.TryParse(
                value,
                out var id)
                ? id
                : 0;
        }
    }

    public int BranchId
    {
        get
        {
            var value =
                User?.FindFirstValue(
                    AppClaimTypes.BranchId);

            return int.TryParse(
                value,
                out var id)
                ? id
                : 0;
        }
    }

    public string Username =>
        User?.Identity?.Name
        ?? string.Empty;

    public bool HasPermission(
        string permissionCode)
    {
        return User?.HasClaim(
            AppPermissions.ClaimType,
            permissionCode) == true;
    }

}
