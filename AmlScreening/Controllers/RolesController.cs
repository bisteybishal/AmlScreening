using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Security;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AmlScreening.Portal.Controllers;


/*
 * Roles are global application security configuration.
 *
 * For your current architecture,
 * only ADMIN may manage them.
 *
 * Users.Manage is also required.
 */

[Authorize(
    Roles = AppRoles.Admin)]

[Authorize(
    Policy = AppPermissions.Roles.View)]

public sealed class RolesController
    : Controller
{
    private readonly IRoleRepository
        _roleRepository;

    private readonly IErrorLogService
        _errorLogService;


    public RolesController(
        IRoleRepository roleRepository,
        IErrorLogService errorLogService)
    {
        _roleRepository =
            roleRepository;

        _errorLogService =
            errorLogService;
    }


    // =========================================================
    // INDEX
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var roles =
                await _roleRepository
                    .GetAllAsync(
                        cancellationToken);


            return View(
                new RolesPageViewModel
                {
                    Roles =
                        roles
                });
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Index));


            return View(
                new RolesPageViewModel
                {
                    ErrorMessage =
                        errorId.HasValue
                            ? $"Roles could not be loaded. Error reference: {errorId}"
                            : "Roles could not be loaded."
                });
        }
    }


    // =========================================================
    // CREATE - GET
    // =========================================================

    [HttpGet]
    public IActionResult Create()
    {
        return View(
            new CreateRoleViewModel());
    }


    // =========================================================
    // CREATE - POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateRoleViewModel model,
        CancellationToken cancellationToken = default)
    {
        model.RoleName =
            model.RoleName?
                .Trim()
            ?? string.Empty;


        try
        {
            if (!string.IsNullOrWhiteSpace(
                    model.RoleName))
            {
                var exists =
                    await _roleRepository
                        .RoleNameExistsAsync(
                            model.RoleName,
                            cancellationToken);


                if (exists)
                {
                    ModelState.AddModelError(
                        nameof(
                            model.RoleName),
                        "This role already exists.");
                }
            }


            if (!ModelState.IsValid)
            {
                return View(model);
            }


            var roleId =
                await _roleRepository
                    .CreateAsync(
                        model.RoleName,
                        model.IsActive,
                        cancellationToken);


            TempData["SuccessMessage"] =
                $"Role {model.RoleName} created. Select its permissions.";


            return RedirectToAction(
                nameof(Permissions),
                new
                {
                    id =
                        roleId
                });
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Create));


            ModelState.AddModelError(
                string.Empty,
                errorId.HasValue
                    ? $"The role could not be created. Error reference: {errorId}"
                    : "The role could not be created.");


            return View(model);
        }
    }


    // =========================================================
    // PERMISSIONS - GET
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Permissions(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id <= 0)
            {
                return NotFound();
            }


            var role =
                await _roleRepository
                    .GetByIdAsync(
                        id,
                        cancellationToken);


            if (role == null)
            {
                return NotFound();
            }


            var permissions =
                await _roleRepository
                    .GetPermissionsAsync(
                        id,
                        cancellationToken);


            var isAdmin =
                role.RoleName.Equals(
                    AppRoles.Admin,
                    StringComparison.OrdinalIgnoreCase);


            return View(
                new RolePermissionsViewModel
                {
                    RoleId =
                        role.RoleId,

                    RoleName =
                        role.RoleName,

                    IsAdminRole =
                        isAdmin,

                    Permissions =
                        permissions,

                    SelectedPermissionIds =
                        permissions

                            .Where(
                                x =>
                                    x.IsSelected)

                            .Select(
                                x =>
                                    x.PermissionId)

                            .ToList()
                });
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Permissions));


            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Role permissions could not be loaded. Error reference: {errorId}"
                    : "Role permissions could not be loaded.";


            return RedirectToAction(
                nameof(Index));
        }
    }


    // =========================================================
    // PERMISSIONS - POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(
        RolePermissionsViewModel model,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (model.RoleId <= 0)
            {
                return NotFound();
            }


            var role =
                await _roleRepository
                    .GetByIdAsync(
                        model.RoleId,
                        cancellationToken);


            if (role == null)
            {
                return NotFound();
            }


            var permissions =
                await _roleRepository
                    .GetPermissionsAsync(
                        model.RoleId,
                        cancellationToken);


            var validPermissionIds =
                permissions
                    .Select(
                        x =>
                            x.PermissionId)
                    .ToHashSet();


            model.SelectedPermissionIds ??=
                [];


            IReadOnlyCollection<int>
                selectedIds;


            // ---------------------------------------------
            // ADMIN ROLE PROTECTION
            // ---------------------------------------------

            if (role.RoleName.Equals(
                    AppRoles.Admin,
                    StringComparison.OrdinalIgnoreCase))
            {
                selectedIds =
                    permissions

                        .Where(
                            permission =>
                                AppPermissions
                                    .AdminDefaults
                                    .Contains(
                                        permission
                                            .PermissionCode,
                                        StringComparer
                                            .OrdinalIgnoreCase))

                        .Select(
                            permission =>
                                permission
                                    .PermissionId)

                        .ToArray();
            }
            else
            {
                selectedIds =
                    model
                        .SelectedPermissionIds

                        .Where(
                            validPermissionIds
                                .Contains)

                        .Distinct()

                        .ToArray();
            }


            await _roleRepository
                .UpdatePermissionsAsync(
                    role.RoleId,
                    selectedIds,
                    cancellationToken);


            TempData["SuccessMessage"] =
                $"Permissions for {role.RoleName} were updated. Users with this role must sign in again to receive the new permissions.";


            return RedirectToAction(
                nameof(Index),
                new
                {
                    id =
                        role.RoleId
                });
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Permissions));


            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Role permissions could not be updated. Error reference: {errorId}"
                    : "Role permissions could not be updated.";


            return RedirectToAction(
                nameof(Index));
        }
    }


    // =========================================================
    // ERROR LOGGING
    // =========================================================

    private Task<long?> LogErrorAsync(
        Exception exception,
        string action)
    {
        return _errorLogService
            .LogAsync(
                exception,
                nameof(RolesController),
                action,
                User.FindFirstValue(
                    AppClaimTypes.PartnerCode));
    }
}