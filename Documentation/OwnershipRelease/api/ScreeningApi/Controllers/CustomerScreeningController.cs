using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ScreeningApi.Controllers;

// Retire the old name-only path, which did not resolve an owner or save KYC results.
// Authentication runs BEFORE returning 410. Move callers to POST /api/customer-kyc.
[ApiController]
[Authorize(Policy="AuthenticatedClient")]
[Route("api/customer-screening")]
public sealed class CustomerScreeningController : ControllerBase
{
    [HttpPost]
    public IActionResult ScreenCustomer() =>
        Problem(title:"This endpoint has been retired. Use POST /api/customer-kyc.",
            statusCode:StatusCodes.Status410Gone);
}

