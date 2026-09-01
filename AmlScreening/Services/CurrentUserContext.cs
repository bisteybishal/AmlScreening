using AmlScreening.Application.Interface;
using AmlScreening.Application.Security;
using System.Security.Claims;

namespace AmlScreening.Portal.Services;

public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public long UserId => ParseRequiredLongClaim(ClaimTypes.NameIdentifier);

    public int ClientId => ParseRequiredClaim(AppClaimTypes.ClientId);

    public string Username => Principal?.Identity?.Name ?? "Unknown";

    public bool HasPermission(string permissionCode) =>
        Principal?.HasClaim(AppPermissions.ClaimType, permissionCode) == true;

    private int ParseRequiredClaim(string claimType)
    {
        var value = Principal?.FindFirstValue(claimType);
        return int.TryParse(value, out var parsed) ? parsed : 0;
    }

    private long ParseRequiredLongClaim(string claimType)
    {
        var value = Principal?.FindFirstValue(claimType);
        return long.TryParse(value, out var parsed) ? parsed : 0;
    }
}
