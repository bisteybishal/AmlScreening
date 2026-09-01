using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Domain.Entities;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AmlScreening.Application.Security;
using System.Security.Claims;

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
            return View(await LoadRolesAsync(new CreateUserViewModel()));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load roles for the create-user page.");
            TempData["ErrorMessage"] = "The create-user page could not be loaded. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.UsersCreate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        model.Username = model.Username?.Trim() ?? string.Empty;
        model.FullName = model.FullName?.Trim() ?? string.Empty;

        try
        {
            if (!string.IsNullOrWhiteSpace(model.Username) &&
                await _userRepository.UsernameExistsAsync(model.Username))
            {
                ModelState.AddModelError(nameof(model.Username), "This username already exists.");
            }

            var roles = await _userRepository.GetActiveRolesAsync();
            if (model.RoleId > 0 && roles.All(x => x.RoleId != model.RoleId))
                ModelState.AddModelError(nameof(model.RoleId), "Select a valid active role.");

            if (!ModelState.IsValid)
            {
                model.Roles = roles;
                return View(model);
            }

            var user = new User
            {
                ClientId = _currentUser.ClientId,
                Username = model.Username,
                FullName = model.FullName,
                PasswordHash = _passwordService.Hash(model.Password),
                IsActive = model.IsActive,
                CreatedDate = DateTime.UtcNow
            };

            await _userRepository.CreateAsync(user, model.RoleId);
            TempData["SuccessMessage"] = $"User {model.Username} created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to create user {Username}.", model.Username);
            ModelState.AddModelError(string.Empty, "The user could not be created. Please try again.");
            model.Roles = await TryLoadRolesAsync();
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

            if (user.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                ModelState.AddModelError(string.Empty, "Administrator permissions are protected to prevent system lockout.");

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildAccessModelAsync(user);
                invalidModel.SelectedPermissionIds = model.SelectedPermissionIds;
                return View(invalidModel);
            }

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

    private async Task<CreateUserViewModel> LoadRolesAsync(CreateUserViewModel model)
    {
        model.Roles = await _userRepository.GetActiveRolesAsync();
        return model;
    }

    private async Task<IReadOnlyList<AmlScreening.Application.Models.RoleOption>> TryLoadRolesAsync()
    {
        try
        {
            return await _userRepository.GetActiveRolesAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to reload roles after a create-user error.");
            return Array.Empty<AmlScreening.Application.Models.RoleOption>();
        }
    }

    private async Task<UserAccessViewModel> BuildAccessModelAsync(AmlScreening.Application.Models.UserListItem user)
    {
        var permissions = await _userRepository.GetPermissionOptionsAsync(user.UserId);

        return new UserAccessViewModel
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            RoleName = user.RoleName,
            Permissions = permissions,
            SelectedPermissionIds = permissions.Where(x => x.IsSelected).Select(x => x.PermissionId).ToList()
        };
    }
}
