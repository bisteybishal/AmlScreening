using System.Security.Claims;
using ScreeningApi.Application.Interface.Repositories;
using ScreeningApi.Application.Models;

namespace ScreeningApi.Security;

public sealed class ApiActorResolver(IApiActorRepository repository)
{
    public Task<ApiActor?> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return Task.FromResult<ApiActor?>(null);

        // MapInboundClaims=false keeps the OAuth issuer's original iss/sub claim names.
        // JwtBearer validates signature, issuer, audience and expiry before this code runs.
        var issuers = principal.FindAll("iss").ToArray();
        var subjects = principal.FindAll("sub").ToArray();
        if (issuers.Length != 1 || subjects.Length != 1)
            return Task.FromResult<ApiActor?>(null);

        return repository.FindAsync(issuers[0].Value, subjects[0].Value, ct);
    }
}

