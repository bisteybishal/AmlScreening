using CustomerKyc.Application;
using CustomerKyc.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CustomerKyc.Controllers;

[ApiController, Route("api/customer-screening"), EnableRateLimiting("local-kyc")]
public sealed class CustomerScreeningController(
    KycRepository repository, IConfiguration configuration,
    ILogger<CustomerScreeningController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ScreeningResponse>> Post(ScreeningRequest request,
        [FromHeader(Name="Idempotency-Key")] string? retryKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(retryKey) || retryKey.Length>100)
            return BadRequest(new {message="Provide an Idempotency-Key header of 1–100 characters."});
        if (string.IsNullOrWhiteSpace(request.ClientReference) || string.IsNullOrWhiteSpace(request.Customer.FullName))
            return BadRequest(new {message="Customer reference and name are required."});
        if (request.Customer.DateOfBirth>DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(new {message="Date of birth cannot be in the future."});
        try
        {
            // Trusted LOCAL configuration only. Never take ClientId from body/header.
            var clientId=configuration.GetValue<int>("LocalTesting:ClientId");
            var allowClear=configuration.GetValue<bool>("LocalTesting:AllowClearDecisions");
            return Ok(await repository.ProcessAsync(clientId,allowClear,request,retryKey,ct));
        }
        catch(RequestConflictException)
        {
            return Conflict(new {message="This Idempotency-Key was already used for different customer input."});
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) {throw;}
        catch(Exception exception)
        {
            logger.LogError(exception,"Local KYC processing failed. TraceId {TraceId}",HttpContext.TraceIdentifier);
            return Problem(statusCode:503,title:"Screening failed. Retry with the same Idempotency-Key.");
        }
    }
}
