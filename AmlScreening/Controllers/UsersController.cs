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
    private readonly IRoleRepository _roleRepository;

    public UsersController(
        IUserRepository userRepository,
        IPasswordService passwordService,
        IRoleRepository roleRepository)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _roleRepository = roleRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(new UsersPageViewModel
        {
            Users = await _userRepository.GetAllAsync()
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.UsersCreate)]
    public async Task<IActionResult> Create()
    {
        return View(await LoadRolesAsync(new CreateUserViewModel()));
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.UsersCreate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        model.Username = model.Username?.Trim() ?? string.Empty;
        model.FullName = model.FullName?.Trim() ?? string.Empty;

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

    [HttpGet]
    [Authorize(Policy = AppPermissions.RolesManage)]
    public async Task<IActionResult> Access(long id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user is null) return NotFound();

        return View(await BuildAccessModelAsync(user));
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.RolesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Access(UserAccessViewModel model)
    {
        var user = await _userRepository.GetByIdAsync(model.UserId);
        if (user is null) return NotFound();

        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var loggedInUserId) &&
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

    private async Task<CreateUserViewModel> LoadRolesAsync(CreateUserViewModel model)
    {
        model.Roles = await _userRepository.GetActiveRolesAsync();
        return model;
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
