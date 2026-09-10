using Microsoft.AspNetCore.Mvc;
using ScreeningApi.Application.Interface.Services;
using ScreeningApi.Application.Models;

namespace ScreeningApi.Controllers;

[ApiController]
[Route("api/customer-kyc")]
public sealed class CustomerKycController(
    ICustomerKycService service,
    ILogger<CustomerKycController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CustomerScreeningResponse>> Check(
        [FromBody] CustomerScreeningRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
            return BadRequest(new { message = "Provide Idempotency-Key, maximum 100 characters." });
        try
        {
            return Ok(await service.CheckAsync(request, idempotencyKey, ct));
        }
        catch (RequestConflictException)
        {
            return Conflict(new { message = "This Idempotency-Key was already used with a different request." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Customer KYC failed. TraceId: {TraceId}", HttpContext.TraceIdentifier);
            return Problem(statusCode: 503, title: "Customer KYC could not be completed.",
                extensions: new Dictionary<string, object?> { ["traceId"] = HttpContext.TraceIdentifier });
        }
    }
}
