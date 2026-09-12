using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Security;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.RolesManage)]
[Authorize(Policy = AppPermissions.AmlRecordsViewAll)]
public sealed class RolesController : Controller
{
    private readonly IRoleRepository _roleRepository;
    private readonly ILogger<RolesController> _logger;

    public RolesController(
        IRoleRepository roleRepository,
        ILogger<RolesController> logger)
    {
        _roleRepository = roleRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            return View(new RolesPageViewModel
            {
                Roles = await _roleRepository.GetAllAsync()
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load roles.");
            return View(new RolesPageViewModel
            {
                ErrorMessage = "Roles could not be loaded. Please try again later."
            });
        }
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateRoleViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRoleViewModel model)
    {
        model.RoleName = model.RoleName?.Trim() ?? string.Empty;
        try
        {
            if (!string.IsNullOrWhiteSpace(model.RoleName) &&
                await _roleRepository.RoleNameExistsAsync(model.RoleName))
            {
                ModelState.AddModelError(nameof(model.RoleName), "This role already exists.");
            }

            if (!ModelState.IsValid) return View(model);

            var roleId = await _roleRepository.CreateAsync(model.RoleName, model.IsActive);
            TempData["SuccessMessage"] = $"Role {model.RoleName} created. Now assign its permissions.";
            return RedirectToAction(nameof(Permissions), new { id = roleId });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to create role {RoleName}.", model.RoleName);
            ModelState.AddModelError(string.Empty, "The role could not be created. Please try again.");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Permissions(int id)
    {
        try
        {
            var role = await _roleRepository.GetByIdAsync(id);
            if (role is null) return NotFound();

            var permissions = await _roleRepository.GetPermissionsAsync(id);
            return View(new RolePermissionsViewModel
            {
                RoleId = role.RoleId,
                RoleName = role.RoleName,
                IsAdminRole = role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase),
                Permissions = permissions,
                SelectedPermissionIds = permissions.Where(x => x.IsSelected).Select(x => x.PermissionId).ToList()
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load permissions for role {RoleId}.", id);
            TempData["ErrorMessage"] = "Role permissions could not be loaded.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(RolePermissionsViewModel model)
    {
        try
        {
            var role = await _roleRepository.GetByIdAsync(model.RoleId);
            if (role is null) return NotFound();

            var permissions = await _roleRepository.GetPermissionsAsync(model.RoleId);
            var validIds = permissions.Select(x => x.PermissionId).ToHashSet();
            var selectedIds = model.SelectedPermissionIds.Where(validIds.Contains).Distinct().ToList();

            if (role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                selectedIds = validIds.ToList();

            await _roleRepository.UpdatePermissionsAsync(role.RoleId, selectedIds);
            TempData["SuccessMessage"] = $"Permissions for {role.RoleName} updated. Assigned users must sign in again.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to update permissions for role {RoleId}.", model.RoleId);
            TempData["ErrorMessage"] = "Role permissions could not be updated. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }
}
