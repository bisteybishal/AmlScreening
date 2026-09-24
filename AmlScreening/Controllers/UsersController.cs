using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using AppUser = AmlScreening.Domain.Entities.User;

namespace AmlScreening.Portal.Controllers;

[Authorize(Roles = AppRoles.Admin)]
[Authorize(Policy = AppPermissions.Users.Manage)]
public sealed class UsersController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly IErrorLogService _errorLogService;

    public UsersController(
        IUserRepository userRepository,
        IPasswordService passwordService,
        IErrorLogService errorLogService)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _errorLogService = errorLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        var result = await _userRepository.GetPartnersAsync(
            Math.Max(1, page),
            10);

        return View(new UsersPageViewModel
        {
            Partners = result.Items,
            TotalPartners = result.TotalCount,
            TotalUsers = result.TotalUsers,
            ActivePartners = result.ActiveCount,
            IActivePartners = result.InactiveCount,
            Page = result.PageNumber,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.Partners.Manage)]
    public IActionResult CreatePartner()
    {
        return View(new CreatePartnerViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.Partners.Manage)]
    public async Task<IActionResult> CreatePartner(
        CreatePartnerViewModel model)
    {
        model.PartnerCode = model.PartnerCode?.Trim() ?? string.Empty;
        model.PartnerName = model.PartnerName?.Trim() ?? string.Empty;
        model.Address = Clean(model.Address);
        model.Email = Clean(model.Email);
        model.PhoneNumber = Clean(model.PhoneNumber);
        model.Country = Clean(model.Country);
        model.City = Clean(model.City);

        if (!ModelState.IsValid)
            return View(model);

        if (await _userRepository.PartnerCodeExistsAsync(model.PartnerCode))
        {
            ModelState.AddModelError(
                nameof(model.PartnerCode),
                "This partner code already exists.");

            return View(model);
        }

        var partnerId = 0;

        var saved = await TrySaveAsync(async () =>
        {
            partnerId = await _userRepository.CreatePartnerAsync(
                new Partner
                {
                    PartnerCode = model.PartnerCode,
                    PartnerName = model.PartnerName,
                    Address = model.Address,
                    Country=model.Country,
                    City=model.City,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    IsActive = model.IsActive
                });
        }, nameof(CreatePartner));

        if (!saved)
            return View(model);

        TempData["SuccessMessage"] = "Partner created successfully.";

        if (!model.IsActive)
            return RedirectToAction(nameof(Index));

        return RedirectToAction(
            nameof(CreateBranch),
            new { partnerId });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.Partners.Manage)]
    public async Task<IActionResult> EditPartner(int partnerId)
    {
        var partner = await _userRepository.GetPartnerByIdAsync(partnerId);

        if (partner is null)
            return NotFound();

        return View(new EditPartnerViewModel
        {
            PartnerId = partner.PartnerId,
            PartnerCode = partner.PartnerCode,
            PartnerName = partner.PartnerName,
            Address = partner.Address,
            Country = partner.Country,
            City = partner.City,
            Email = partner.Email,
            PhoneNumber = partner.PhoneNumber,
            IsActive = partner.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.Partners.Manage)]
    public async Task<IActionResult> EditPartner(
        EditPartnerViewModel model)
    {
        var existing = await _userRepository.GetPartnerByIdAsync(
            model.PartnerId);

        if (existing is null)
            return NotFound();

        model.PartnerCode = model.PartnerCode?.Trim() ?? string.Empty;
        model.PartnerName = model.PartnerName?.Trim() ?? string.Empty;
        model.Address = Clean(model.Address);
        model.Email = Clean(model.Email);
        model.PhoneNumber = Clean(model.PhoneNumber);
        model.Country = Clean(model.Country);
        model.City = Clean(model.City);

        if (!model.IsActive &&
            model.PartnerId == CurrentIntClaim(AppClaimTypes.PartnerId))
        {
            ModelState.AddModelError(
                nameof(model.IsActive),
                "You cannot deactivate the partner you are signed in under.");
        }

        if (!ModelState.IsValid)
            return View(model);

        if (await _userRepository.PartnerCodeExistsAsync(
            model.PartnerCode,
            model.PartnerId))
        {
            ModelState.AddModelError(
                nameof(model.PartnerCode),
                "This partner code already exists.");

            return View(model);
        }

        var saved = await TrySaveAsync(async () =>
        {
            existing.PartnerCode = model.PartnerCode;
            existing.PartnerName = model.PartnerName;
            existing.Address = model.Address;
            existing.Country = model.Country;
            existing.City = model.City;
            existing.Email = model.Email;
            existing.PhoneNumber = model.PhoneNumber;
            existing.IsActive = model.IsActive;

            if (!await _userRepository.UpdatePartnerAsync(existing))
                throw new InvalidOperationException("Partner was not found.");
        }, nameof(EditPartner));

        if (!saved)
            return View(model);

        TempData["SuccessMessage"] = "Partner updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> PartnerBranchesDetail(int partnerId)
    {
        var partner = await _userRepository.GetPartnerByIdAsync(partnerId);

        if (partner is null)
            return NotFound();

        return View(new PartnerBranchesViewModel
        {
            PartnerId = partner.PartnerId,
            PartnerCode = partner.PartnerCode,
            PartnerName = partner.PartnerName,
            IsActive = partner.IsActive,
            Branches = await _userRepository.GetBranchesByPartnerAsync(
                partner.PartnerId)
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.Partners.Manage)]
    public async Task<IActionResult> CreateBranch(int partnerId)
    {
        var partner = await _userRepository.GetPartnerByIdAsync(partnerId);

        if (partner is null)
            return NotFound();

        if (!partner.IsActive)
            return BadRequest("Activate the partner before adding a branch.");

        return View(new CreateBranchViewModel
        {
            PartnerId = partner.PartnerId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.Partners.Manage)]
    public async Task<IActionResult> CreateBranch(
        CreateBranchViewModel model)
    {
        var partner = await _userRepository.GetPartnerByIdAsync(
            model.PartnerId);

        if (partner is null)
            return NotFound();

        model.BranchName = model.BranchName?.Trim() ?? string.Empty;
        model.Address = Clean(model.Address);
        model.Email = Clean(model.Email);
        model.PhoneNumber = Clean(model.PhoneNumber);

        if (!partner.IsActive)
        {
            ModelState.AddModelError(
                string.Empty,
                "Activate the partner before adding a branch.");
        }

        if (!ModelState.IsValid)
            return View(model);

        if (await _userRepository.BranchNameExistsAsync(
            partner.PartnerId,
            model.BranchName))
        {
            ModelState.AddModelError(
                nameof(model.BranchName),
                "This branch name already exists under this partner.");

            return View(model);
        }

        var branchId = 0;

        var saved = await TrySaveAsync(async () =>
        {
            branchId = await _userRepository.CreateBranchAsync(
                new PartnerBranch
                {
                    PartnerId = partner.PartnerId,
                    BranchName = model.BranchName,
                    Address = model.Address,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    IsActive = model.IsActive
                });
        }, nameof(CreateBranch));

        if (!saved)
            return View(model);

        TempData["SuccessMessage"] = "Branch created successfully.";

        if (!model.IsActive)
        {
            return RedirectToAction(
                nameof(PartnerBranchesDetail),
                new { partnerId = partner.PartnerId });
        }

        return RedirectToAction(
            nameof(CreateUser),
            new
            {
                partnerId = partner.PartnerId,
                branchId
            });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.Partners.Manage)]
    public async Task<IActionResult> EditBranchDetail(int branchId)
    {
        var branch = await _userRepository.GetBranchByIdAsync(branchId);

        if (branch is null)
            return NotFound();

        return View(new EditBranchViewModel
        {
            BranchId = branch.BranchId,
            PartnerId = branch.PartnerId,
            BranchName = branch.BranchName,
            Address = branch.Address,
            Email = branch.Email,
            PhoneNumber = branch.PhoneNumber,
            IsActive = branch.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.Partners.Manage)]
    public async Task<IActionResult> EditBranchDetail(
        EditBranchViewModel model)
    {
        var existing = await _userRepository.GetBranchByIdAsync(
            model.BranchId);

        if (existing is null)
            return NotFound();

        if (existing.PartnerId != model.PartnerId)
            return BadRequest("The branch does not belong to this partner.");

        model.BranchName = model.BranchName?.Trim() ?? string.Empty;
        model.Address = Clean(model.Address);
        model.Email = Clean(model.Email);
        model.PhoneNumber = Clean(model.PhoneNumber);

        if (!model.IsActive &&
            model.BranchId == CurrentIntClaim(AppClaimTypes.BranchId))
        {
            ModelState.AddModelError(
                nameof(model.IsActive),
                "You cannot deactivate the branch you are signed in under.");
        }

        if (!ModelState.IsValid)
            return View(model);

        if (await _userRepository.BranchNameExistsAsync(
            existing.PartnerId,
            model.BranchName,
            existing.BranchId))
        {
            ModelState.AddModelError(
                nameof(model.BranchName),
                "This branch name already exists under this partner.");

            return View(model);
        }

        var saved = await TrySaveAsync(async () =>
        {
            existing.BranchName = model.BranchName;
            existing.Address = model.Address;
            existing.Email = model.Email;
            existing.PhoneNumber = model.PhoneNumber;
            existing.IsActive = model.IsActive;

            if (!await _userRepository.UpdateBranchAsync(existing))
                throw new InvalidOperationException("Branch was not found.");
        }, nameof(EditBranchDetail));

        if (!saved)
            return View(model);

        TempData["SuccessMessage"] = "Branch updated successfully.";

        return RedirectToAction(
            nameof(PartnerBranchesDetail),
            new { partnerId = existing.PartnerId });
    }

    [HttpGet]
    public async Task<IActionResult> BranchUsers(
        int partnerId,
        int branchId,
        int page = 1)
    {
        var partner = await _userRepository.GetPartnerByIdAsync(partnerId);
        var branch = await _userRepository.GetBranchByIdAsync(branchId);

        if (partner is null ||
            branch is null ||
            branch.PartnerId != partner.PartnerId)
        {
            return NotFound();
        }

        var result = await _userRepository.GetPageAsync(
            partner.PartnerId,
            branch.BranchId,
            Math.Max(1, page),
            10);

        return View(new BranchUsersViewModel
        {
            PartnerId = partner.PartnerId,
            PartnerName = partner.PartnerName,
            BranchId = branch.BranchId,
            BranchName = branch.BranchName,
            Users = result.Items,
            TotalUsers = result.TotalUsers,
            ActiveUsers = result.ActiveUsers,
            InactiveUsers = result.InactiveUsers,
            Page = result.PageNumber,
            PageSize = result.PageSize,
            TotalPages = Math.Max(1, result.TotalPages)
        });
    }

    [HttpGet]
    public async Task<IActionResult> CreateUser(
        int partnerId,
        int branchId)
    {
        var model = new CreateUserViewModel
        {
            PartnerId = partnerId,
            BranchId = branchId
        };

        await LoadUserOptionsAsync(model);

        if (!HasActiveParents(model))
        {
            return BadRequest(
                "Select an active branch belonging to an active partner.");
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(
        CreateUserViewModel model)
    {
        model.Username = model.Username?.Trim() ?? string.Empty;
        model.Address = Clean(model.Address);
        model.Email = Clean(model.Email);
        model.PhoneNumber = Clean(model.PhoneNumber);
        model.SelectedRoleIds = (model.SelectedRoleIds ?? [])
            .Distinct()
            .ToList();

        await LoadUserOptionsAsync(model);

        if (!HasActiveParents(model))
        {
            ModelState.AddModelError(
                string.Empty,
                "Select an active branch belonging to an active partner.");
        }

        ValidateRoles(model.SelectedRoleIds, model.Roles);

        if (!ModelState.IsValid)
            return View(model);

        if (await _userRepository.UsernameExistsAsync(model.Username))
        {
            ModelState.AddModelError(
                nameof(model.Username),
                "This username already exists.");

            return View(model);
        }

        var saved = await TrySaveAsync(async () =>
        {
            await _userRepository.CreateAsync(
                new AppUser
                {
                    PartnerId = model.PartnerId,
                    BranchId = model.BranchId,
                    Username = model.Username,
                    PasswordHash = _passwordService.Hash(model.Password),
                    Address = model.Address,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    IsActive = model.IsActive
                },
                model.SelectedRoleIds);
        }, nameof(CreateUser));

        if (!saved)
            return View(model);

        TempData["SuccessMessage"] = "User created successfully.";

        return RedirectToAction(
            nameof(BranchUsers),
            new
            {
                partnerId = model.PartnerId,
                branchId = model.BranchId
            });
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(long id)
    {
        var user = await _userRepository.GetUserByIdAsync(id);
        var access = await _userRepository.GetAccessAsync(id);

        if (user is null || access is null)
            return NotFound();

        return View(new EditUserViewModel
        {
            UserId = user.UserId,
            PartnerId = access.PartnerId,
            BranchId = access.BranchId,
            Username = access.Username,
            PartnerName = access.PartnerName,
            BranchName = access.BranchName,
            Address = user.Address,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(EditUserViewModel model)
    {
        var access = await _userRepository.GetAccessAsync(model.UserId);

        if (access is null)
            return NotFound();

        // Load ownership/display values from the database, not the form.
        model.PartnerId = access.PartnerId;
        model.BranchId = access.BranchId;
        model.Username = access.Username;
        model.PartnerName = access.PartnerName;
        model.BranchName = access.BranchName;

        model.Address = Clean(model.Address);
        model.Email = Clean(model.Email);
        model.PhoneNumber = Clean(model.PhoneNumber);

        if (model.UserId == CurrentUserId() && !model.IsActive)
        {
            ModelState.AddModelError(
                nameof(model.IsActive),
                "You cannot deactivate your own account.");
        }

        if (!ModelState.IsValid)
            return View(model);

        var saved = await TrySaveAsync(async () =>
        {
            var updated = await _userRepository.UpdateUserProfileAsync(
                model.UserId,
                model.Address,
                model.Email,
                model.PhoneNumber,
                model.IsActive);

            if (!updated)
                throw new InvalidOperationException("User was not found.");
        }, nameof(EditUser));

        if (!saved)
            return View(model);

        TempData["SuccessMessage"] = "User updated successfully.";

        return RedirectToAction(
            nameof(BranchUsers),
            new
            {
                partnerId = access.PartnerId,
                branchId = access.BranchId
            });
    }

    [HttpGet]
    public async Task<IActionResult> Access(long id)
    {
        var user = await _userRepository.GetAccessAsync(id);

        if (user is null)
            return NotFound();

        return View(new UserAccessViewModel
        {
            UserId = user.UserId,
            PartnerId = user.PartnerId,
            BranchId = user.BranchId,
            Username = user.Username,
            PartnerName = user.PartnerName,
            BranchName = user.BranchName,
            IsActive = user.IsActive,
            SelectedRoleIds = (
                await _userRepository.GetUserRoleIdsAsync(id)).ToList(),
            Roles = await _userRepository.GetActiveRolesAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Access(UserAccessViewModel model)
    {
        var user = await _userRepository.GetAccessAsync(model.UserId);

        if (user is null)
            return NotFound();

        model.PartnerId = user.PartnerId;
        model.BranchId = user.BranchId;
        model.Username = user.Username;
        model.PartnerName = user.PartnerName;
        model.BranchName = user.BranchName;

        model.Roles = await _userRepository.GetActiveRolesAsync();
        model.SelectedRoleIds = (model.SelectedRoleIds ?? [])
            .Distinct()
            .ToList();

        ValidateRoles(model.SelectedRoleIds, model.Roles);

        if (model.UserId == CurrentUserId())
        {
            var currentRoles = (
                await _userRepository.GetUserRoleIdsAsync(model.UserId))
                .ToHashSet();

            if (model.IsActive != user.IsActive ||
                !currentRoles.SetEquals(model.SelectedRoleIds))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Another administrator must change your roles or account status.");
            }
        }

        if (!ModelState.IsValid)
            return View(model);

        var saved = await TrySaveAsync(
            () => _userRepository.UpdateAccessAsync(
                model.UserId,
                model.IsActive,
                model.SelectedRoleIds),
            nameof(Access));

        if (!saved)
            return View(model);

        TempData["SuccessMessage"] =
            "Access saved. If access changed, the user must sign in again.";

        return RedirectToAction(
            nameof(BranchUsers),
            new
            {
                partnerId = user.PartnerId,
                branchId = user.BranchId
            });
    }

    [HttpGet]
    public async Task<IActionResult> GetBranches(int partnerId)
    {
        var partner = await _userRepository.GetPartnerByIdAsync(partnerId);

        if (partner is null || !partner.IsActive)
            return Json(Array.Empty<BranchOption>());

        var branches = await _userRepository.GetActiveBranchesAsync(
            partner.PartnerId);

        return Json(branches);
    }

    private async Task LoadUserOptionsAsync(CreateUserViewModel model)
    {
        model.Partners = await _userRepository.GetActivePartnersAsync();
        model.Roles = await _userRepository.GetActiveRolesAsync();

        model.Branches = model.PartnerId > 0
            ? await _userRepository.GetActiveBranchesAsync(model.PartnerId)
            : Array.Empty<BranchOption>();
    }

    private static bool HasActiveParents(CreateUserViewModel model)
    {
        return model.Partners.Any(
                   partner => partner.PartnerId == model.PartnerId)
               && model.Branches.Any(
                   branch => branch.BranchId == model.BranchId
                             && branch.PartnerId == model.PartnerId);
    }

    private void ValidateRoles(
        IReadOnlyCollection<int> selectedRoleIds,
        IReadOnlyList<RoleOption> roles)
    {
        var allowedIds = roles.Select(role => role.RoleId).ToHashSet();

        if (selectedRoleIds.Count == 0 ||
            selectedRoleIds.Any(id => !allowedIds.Contains(id)))
        {
            ModelState.AddModelError(
                "SelectedRoleIds",
                "Select at least one valid active role.");
        }
    }

    private async Task<bool> TrySaveAsync(
        Func<Task> save,
        string action)
    {
        try
        {
            await save();
            return true;
        }
        catch (Exception exception)
        {
            var errorId = await _errorLogService.LogAsync(
                exception,
                nameof(UsersController),
                action,
                User.FindFirstValue(AppClaimTypes.PartnerCode));

            ModelState.AddModelError(
                string.Empty,
                errorId.HasValue
                    ? $"Unable to save. Error reference: {errorId.Value}."
                    : "Unable to save. Please try again.");

            return false;
        }
    }

    private long CurrentUserId()
    {
        return long.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id)
            ? id
            : 0;
    }

    private int CurrentIntClaim(string claimType)
    {
        return int.TryParse(
            User.FindFirstValue(claimType),
            out var id)
            ? id
            : 0;
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}