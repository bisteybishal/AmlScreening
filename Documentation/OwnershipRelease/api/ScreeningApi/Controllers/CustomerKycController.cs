using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScreeningApi.Application.Models;
using ScreeningApi.Application.Services;
using ScreeningApi.Security;

namespace ScreeningApi.Controllers;

[ApiController]
[Authorize(Policy="AuthenticatedClient")]
[Route("api/customer-kyc")]
public sealed class CustomerKycController(CustomerKycService service, ApiActorResolver actors,
    ILogger<CustomerKycController> logger) : ControllerBase
{
    [HttpPost]
    public Task<IActionResult> Process(CustomerKycRequest request, CancellationToken ct) =>
        ExecuteAsync(async actor => Ok(ToResponse(await service.ProcessAsync(actor,request,ct))),ct);

    [HttpGet("{resultId:long}")]
    public Task<IActionResult> GetResult(long resultId, CancellationToken ct) =>
        ExecuteAsync(async actor =>
        {
            var result = await service.GetResultAsync(actor,resultId,ct);
            // A different user's ID is indistinguishable from an unknown result.
            return result is null ? NotFound() : Ok(ToResponse(result));
        },ct);

    private async Task<IActionResult> ExecuteAsync(
        Func<ApiActor,Task<IActionResult>> action, CancellationToken ct)
    {
        try
        {
            var actor = await actors.ResolveAsync(User,ct);
            return actor is null ? Forbid() : await action(actor);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (KycClientException) { return Forbid(); }
        catch (KycCustomerException)
        {
            return Problem(title:"This customer reference is inactive or unavailable.",statusCode:409);
        }
        catch (ArgumentException ex) { return Problem(title:ex.Message,statusCode:400); }
        catch (KycConfigurationException ex)
        {
            logger.LogWarning(ex,"KYC configuration is incomplete; trace {TraceId}",HttpContext.TraceIdentifier);
            return Problem(title:"Approved customer risk configuration is incomplete or ambiguous.",statusCode:409);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,"KYC failed; trace {TraceId}",HttpContext.TraceIdentifier);
            return Problem(title:"Customer KYC is temporarily unavailable.",statusCode:503);
        }
    }

    private static object ToResponse(CustomerKycResponse result) => new
    {
        clientReference = result.CustomerReference,
        screeningStatus = result.AmlResult.ScreeningStatus,
        isUnMatch = result.AmlResult.IsUnMatch,
        isEuMatch = result.AmlResult.IsEuMatch,
        isPep = result.AmlResult.IsPep,
        highestMatchScore = result.AmlResult.HighestMatchScore,
        customerRiskScore = result.RiskScore,
        riskLevel = result.RiskLevel,
        decision = result.Decision,
        reasons = result.AmlResult.Reasons,
        correlationId = result.AmlResult.CorrelationId,
        resultId = result.ResultId,
        customerId = result.CustomerId,
        calculatedAtUtc = result.CalculatedAtUtc
    };
}

