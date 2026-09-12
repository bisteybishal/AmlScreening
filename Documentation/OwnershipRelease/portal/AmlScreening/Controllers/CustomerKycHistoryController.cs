using AmlScreening.Application.Security;
using AmlScreening.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.CustomersView)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CustomerKycHistoryController(AmlScreeningDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? customerId, CancellationToken ct)
    {
        // OwnerFilter is enforced for normal users; system administrators can view all.
        var query = db.CustomerKycResults.AsNoTracking();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        return View(await query.OrderByDescending(x => x.Id).Take(100).ToListAsync(ct));
    }
    [HttpGet]
    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var result = await db.CustomerKycResults.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return result is null ? NotFound() : View(result);
    }
}
