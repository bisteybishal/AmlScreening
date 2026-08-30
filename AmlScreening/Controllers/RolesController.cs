using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Security;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.RolesManage)]
public sealed class RolesController : Controller
{
    private readonly IRoleRepository _roleRepository;

    public RolesController(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(new RolesPageViewModel
        {
            Roles = await _roleRepository.GetAllAsync()
        });
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

    [HttpGet]
    public async Task<IActionResult> Permissions(int id)
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(RolePermissionsViewModel model)
    {
        var role = await _roleRepository.GetByIdAsync(model.RoleId);
        if (role is null) return NotFound();

        var permissions = await _roleRepository.GetPermissionsAsync(model.RoleId);
        var validIds = permissions.Select(x => x.PermissionId).ToHashSet();
        var selectedIds = model.SelectedPermissionIds.Where(validIds.Contains).Distinct().ToList();

        // Prevent accidental administrator lockout.
        if (role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            selectedIds = validIds.ToList();

        await _roleRepository.UpdatePermissionsAsync(role.RoleId, selectedIds);
        TempData["SuccessMessage"] = $"Permissions for {role.RoleName} updated successfully.";
        return RedirectToAction(nameof(Index));
    }
}
