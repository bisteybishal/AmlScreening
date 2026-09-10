using System.Security.Claims;
using CustomerKyc.Infrastructure;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace CustomerKyc.Controllers;

public sealed class TokenController(PartnerRegistry registry) : ControllerBase
{
    [AllowAnonymous, HttpPost("/connect/token"), EnableRateLimiting("token")]
    public async Task<IActionResult> Token(CancellationToken ct)
    {
        // OpenIddict validates the client's secret and grant permissions BEFORE this action.
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OAuth request missing.");
        if (!request.IsClientCredentialsGrantType()) return BadRequest();
        var partner = await registry.FindAsync(request.ClientId!, ct);
        if (partner == null)
            return Forbid(new AuthenticationProperties(new Dictionary<string,string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidClient,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Partner access is disabled."
            }), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var identity = new ClaimsIdentity("OAuth2", Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, request.ClientId!);
        identity.SetClaim("tenant_id", partner.ClientId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        identity.SetDestinations(_ => [Destinations.AccessToken]);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetResources("customer-kyc");
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
