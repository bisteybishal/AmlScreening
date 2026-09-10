using CustomerKyc.Application;
using CustomerKyc.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Abstractions;

namespace CustomerKyc.Controllers;

[ApiController, Route("api/customer-screening")]
[Authorize(Policy = "customer-screening"), EnableRateLimiting("kyc")]
public sealed class CustomerScreeningController(KycRepository repository,
    PartnerRegistry registry, ILogger<CustomerScreeningController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ScreeningResponse>> Post(ScreeningRequest request,
        [FromHeader(Name="Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
            return BadRequest(new {message="Idempotency-Key header must contain 1–100 characters."});
        if (string.IsNullOrWhiteSpace(request.ClientReference) || string.IsNullOrWhiteSpace(request.Customer.FullName))
            return BadRequest(new {message="Customer reference and name are required."});
        if (request.Customer.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(new {message="Date of birth cannot be in the future."});
        try
        {
            var oauthId = User.GetClaim(OpenIddictConstants.Claims.Subject);
            if (oauthId == null) return Unauthorized();
            var partner = await registry.FindAsync(oauthId, ct);
            if (partner == null || User.GetClaim("tenant_id") != partner.ClientId.ToString(System.Globalization.CultureInfo.InvariantCulture))
                return Forbid();
            return Ok(await repository.ProcessAsync(partner, oauthId, request, idempotencyKey, ct));
        }
        catch (RequestConflictException) { return Conflict(new {message="This retry key was already used for different input."}); }
        catch (PartnerDeniedException) { return Forbid(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // No request bodies, identification numbers, client secrets or tokens in logs.
            logger.LogError(ex, "KYC processing failed. TraceId {TraceId}", HttpContext.TraceIdentifier);
            return Problem(statusCode:503, title:"Screening could not be completed. Retry with the same Idempotency-Key.");
        }
    }
}
