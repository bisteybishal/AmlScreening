using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Domain.Entities;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AmlScreening.Application.Security;
using System.Security.Claims;
using AmlScreening.Application.Models;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.UsersView)]
public sealed class UsersController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserRepository userRepository,
        IPasswordService passwordService,
        ICurrentUserContext currentUser,
        ILogger<UsersController> logger)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        try
        {
            const int pageSize = 10;
            var requestedPage = Math.Max(1, page);
            var result = await _userRepository.GetPageAsync(requestedPage, pageSize);
            var totalPages = Math.Max(1, result.TotalPages);
            var currentPage = Math.Clamp(requestedPage, 1, totalPages);
            if (currentPage != requestedPage)
                result = await _userRepository.GetPageAsync(currentPage, pageSize);

            return View(new UsersPageViewModel
            {
                Users = result.Items,
                TotalUsers = result.TotalUsers,
                ActiveUsers = result.ActiveUsers,
                InactiveUsers = result.InactiveUsers,
                Administrators = result.Administrators,
                Page = currentPage,
                PageSize = pageSize,
                TotalPages = totalPages
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load the users page.");
            return View(new UsersPageViewModel
            {
                ErrorMessage = "Users could not be loaded. Please refresh the page or try again later."
            });
        }
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.UsersCreate)]
    public async Task<IActionResult> Create()
    {
        try
        {
            var model = new CreateUserViewModel();

            await LoadCreateOptionsAsync(model);

            return View(model);

            // return View(await LoadRolesAsync(new CreateUserViewModel()));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load roles for the create-user page.");
            TempData["ErrorMessage"] = "The create-user page could not be loaded. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    //[Authorize(Roles = "Admin")]
    [Authorize(Policy = AppPermissions.UsersCreate)]
    //[Authorize(Policy = AppPermissions.RolesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        model.Username =
            model.Username?.Trim() ?? string.Empty;

        model.FullName =
            model.FullName?.Trim() ?? string.Empty;

        model.CompanyName =
            model.CompanyName?.Trim() ?? string.Empty;

        model.Email =
            model.Email?.Trim() ?? string.Empty;

        model.Address =
            model.Address?.Trim() ?? string.Empty;

        model.PhoneNumber =
            model.PhoneNumber?.Trim() ?? string.Empty;

        // Never trust ClientId from browser.
        model.ClientId = _currentUser.ClientId;

        try
        {
            await LoadCreateOptionsAsync(model);

            if (model.SelectedRoleIds.Count == 0)
            {
                ModelState.AddModelError(nameof(model.SelectedRoleIds), "Select at least one role.");
            }

            var validRoleIds = model.Roles
                    .Select(x => x.RoleId)
                    .ToHashSet();

            var invalidRoleSelected =
                model.SelectedRoleIds
                    .Distinct()
                    .Any(roleId =>
                        !validRoleIds.Contains(roleId));

            if (invalidRoleSelected)
            {
                ModelState.AddModelError(
                    nameof(model.SelectedRoleIds),
                    "One or more selected roles are invalid.");
            }

            if (!string.IsNullOrWhiteSpace(model.Username) &&
                await _userRepository.UsernameExistsAsync(
                    model.Username))
            {
                ModelState.AddModelError(
                    nameof(model.Username),
                    "This username already exists.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = new User
            {
                ClientId = _currentUser.ClientId,

                Username = model.Username,

                FullName = model.FullName,

                CompanyName = model.CompanyName,

                Email = model.Email,

                Address = model.Address,

                PhoneNumber = model.PhoneNumber,

                PasswordHash = _passwordService.Hash(model.Password),

                IsActive = model.IsActive,

                CreatedDate = DateTime.UtcNow
            };

            await _userRepository.CreateAsync(
                user,
                model.SelectedRoleIds);

            TempData["SuccessMessage"] =
                $"User {model.Username} created successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to create user {Username} for client {ClientId}.",
                model.Username,
                model.ClientId);

            ModelState.AddModelError(
                string.Empty,
                "The user could not be created. Please try again.");

            await LoadCreateOptionsAsync(model);

            return View(model);
        }
    }


    [HttpGet]
    [Authorize(Policy = AppPermissions.RolesManage)]
    public async Task<IActionResult> Access(long id)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user is null) return NotFound();

            return View(await BuildAccessModelAsync(user));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load access settings for user {UserId}.", id);
            TempData["ErrorMessage"] = "User permissions could not be loaded. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.RolesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Access(UserAccessViewModel model)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(model.UserId);
            if (user is null) return NotFound();

            if (long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var loggedInUserId) &&
                loggedInUserId == model.UserId)
            {
                ModelState.AddModelError(string.Empty, "You cannot change your own permissions while signed in.");
            }
            var existingRoles = await _userRepository.GetUserRolesAsync(model.UserId);
            var isplatformAdmin = existingRoles.Any(x => x.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase));

            if (isplatformAdmin)
            {
                ModelState.AddModelError(string.Empty, "PlatForm Administrator access is protected.");
            }

            if (model.SelectedRoleIds.Count == 0)
            {
                ModelState.AddModelError(nameof(model.SelectedRoleIds), "Select at least one role.");
            }


            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildAccessModelAsync(user);
                invalidModel.SelectedRoleIds = model.SelectedRoleIds;
                invalidModel.SelectedPermissionIds = model.SelectedPermissionIds;
                return View(invalidModel);
            }

            if (!await _userRepository.UpdateRolesAsync(model.UserId, model.SelectedRoleIds))
                return NotFound();

            if (!await _userRepository.UpdatePermissionsAsync(model.UserId, model.SelectedPermissionIds))
                return NotFound();

            TempData["SuccessMessage"] = $"Permissions for {user.Username} updated. The user must sign in again.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to update permissions for user {UserId}.", model.UserId);
            TempData["ErrorMessage"] = "User permissions could not be updated. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    private async Task LoadCreateOptionsAsync(CreateUserViewModel model)
    {
        //model.ClientId = await _userRepository.GetActiveClientsAsync();
        model.ClientId = _currentUser.ClientId;
        model.Roles = await _userRepository.GetActiveRolesAsync();
    }

    private async Task<UserAccessViewModel> BuildAccessModelAsync(UserListItem user)
    {
        var permissions = await _userRepository.GetPermissionOptionsAsync(user.UserId);
        var availableRoles = await _userRepository.GetActiveRolesAsync();
        var userRoles = await _userRepository.GetUserRolesAsync(user.UserId);

        var selectedRolesIds = userRoles.Select(x => x.RoleId).ToHashSet();
        var roleAccess = new List<RoleAccessViewModel>();
        foreach (var role in availableRoles)
        {
            var rolePermissions = await _userRepository.GetRolePermissionAsync(role.RoleId);
            roleAccess.Add(new RoleAccessViewModel
            {
                RoleId = role.RoleId,
                RoleName = role.RoleName,
                IsSelected = selectedRolesIds.Contains(role.RoleId),
                Permissions = rolePermissions
            });
        }

        return new UserAccessViewModel
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Roles = availableRoles,
            SelectedRoleIds = selectedRolesIds.ToList(),
            RoleNames = string.Join(",", userRoles.Select(x => x.RoleName)),
            Permissions = permissions,
            SelectedPermissionIds = permissions.Where(x => x.IsSelected).Select(x => x.PermissionId).ToList(),
            RoleAccess=roleAccess
        };
    }
}
