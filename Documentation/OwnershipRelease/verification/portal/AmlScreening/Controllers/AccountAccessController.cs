using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Security;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers;

[Authorize(Roles = "Admin")]
[Authorize(Policy = AppPermissions.RolesManage)]
[Authorize(Policy = AppPermissions.AmlRecordsViewAll)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountAccessController(IAccountAccessRepository repository, ILogger<AccountAccessController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(long id)
    {
        var account = await repository.GetAsync(id);
        if (account is null) return NotFound();
        return View(new AccountAccessViewModel { Account = account, Applications = await repository.GetApplicationsAsync(id) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Portal(long userId, bool enabled) => SaveAsync(userId,
        () => repository.SetPortalAccessAsync(userId, enabled), "Portal access saved. UserId, ClientId and customer ownership are unchanged.");

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Link(LinkApiApplicationRequest request)
    {
        if (!ModelState.IsValid || !request.IdentityVerified)
        {
            TempData["ErrorMessage"] = "Enter the issuer and subject and confirm that you verified them with your OAuth provider.";
            return Task.FromResult<IActionResult>(RedirectToAction(nameof(Index), new { id = request.UserId }));
        }
        return SaveAsync(request.UserId, () => repository.LinkApplicationAsync(request.UserId, request.Issuer, request.Subject),
            "API application linked to this existing account. Module permissions and source grants are still required.");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Application(long userId, int applicationId, bool enabled) => SaveAsync(userId,
        () => repository.SetApplicationActiveAsync(userId, applicationId, enabled), "API application access saved.");

    private async Task<IActionResult> SaveAsync(long userId, Func<Task> save, string message)
    {
        try { await save(); TempData["SuccessMessage"] = message; }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException exception) { TempData["ErrorMessage"] = exception.Message; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Account access update failed for user {UserId}.", userId);
            TempData["ErrorMessage"] = "The change could not be saved. No success was confirmed; review the account before retrying.";
        }
        return RedirectToAction(nameof(Index), new { id = userId });
    }
}
