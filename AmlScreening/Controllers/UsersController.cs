using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Domain.Entities;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AmlScreening.Application.Security;
using System.Security.Claims;
using AmlScreening.Application.Models;
using AmlScreening.Application.Interface.Services;

namespace AmlScreening.Portal.Controllers;

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
    public async Task<IActionResult> Index(int? partnerId, int? branchId, int page = 1)
    {
        try
        {
            const int pageSize = 10;
            var result = await _userRepository.GetPageAsync(partnerId, branchId, page, pageSize);
            var partners = await _userRepository.GetActivePartnersAsync();
            var branches =
                partnerId.HasValue

                    ? await _userRepository
                        .GetActiveBranchesAsync(
                            partnerId.Value)

                    : Array.Empty<BranchOption>();

            var model = new UsersPageViewModel
            {
                Users = result.Items,
                Partners = partners,
                Branches = branches,
                PartnerId = partnerId,
                BranchId = branchId,
                TotalUsers = result.TotalUsers,
                ActiveUsers = result.ActiveUsers,
                InactiveUsers = result.InactiveUsers,
                Page = result.PageNumber,
                PageSize = result.PageSize,
                TotalPages = Math.Max(1, result.TotalPages)
            };
            return View(model);
        }

        catch (Exception ex)
        {
            var errorId =
               await LogErrorAsync(
                   ex,
                   nameof(Index));


            return View(
                new UsersPageViewModel
                {
                    ErrorMessage = $"Users could not be loaded.Error reference: {errorId}"
                });

        }
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.Users.Manage)]
    public async Task<IActionResult> CreatePartner(CreateUserViewModel model)
    {
        return View(
             new CreatePartnerViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult>
         CreatePartner(
             CreatePartnerViewModel model)
    {
        try
        {
            model.PartnerCode =
                model.PartnerCode?
                    .Trim()
                    .ToUpperInvariant()
                ?? string.Empty;


            model.PartnerName =
                model.PartnerName?
                    .Trim()
                ?? string.Empty;


            model.Address =
                NullIfWhiteSpace(
                    model.Address);

            model.Email =
                NullIfWhiteSpace(
                    model.Email);

            model.PhoneNumber =
                NullIfWhiteSpace(
                    model.PhoneNumber);


            if (!string.IsNullOrWhiteSpace(
                    model.PartnerCode))
            {
                var exists =
                    await _userRepository
                        .PartnerCodeExistsAsync(
                            model.PartnerCode);


                if (exists)
                {
                    ModelState.AddModelError(
                        nameof(
                            model.PartnerCode),
                        "Partner code already exists.");
                }
            }


            if (!ModelState.IsValid)
            {
                return View(model);
            }


            var partner =
                new Partner
                {
                    PartnerCode =
                        model.PartnerCode,

                    PartnerName =
                        model.PartnerName,

                    Address =
                        model.Address,

                    Email =
                        model.Email,

                    PhoneNumber =
                        model.PhoneNumber,

                    IsActive =
                        model.IsActive
                };


            var partnerId =
                await _userRepository
                    .CreatePartnerAsync(
                        partner);


            TempData["SuccessMessage"] =
                "Partner created successfully.";


            /*
             * After Partner creation,
             * go directly to Branch creation.
             */

            return RedirectToAction(
                nameof(CreateBranch),
                new
                {
                    partnerId
                });
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(CreatePartner));


            ModelState.AddModelError(
                string.Empty,
                errorId.HasValue
                    ? $"Unable to create partner. Error reference: {errorId}"
                    : "Unable to create partner.");


            return View(model);
        }
    }
    // CREATE BRANCH - GET

    [HttpGet]
    public async Task<IActionResult>
        CreateBranch(
            int? partnerId)
    {
        try
        {
            var model =
                new CreateBranchViewModel
                {
                    PartnerId =
                        partnerId
                        ?? 0
                };


            await LoadPartnerOptionsAsync(
                model);


            return View(model);
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(CreateBranch));


            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Unable to load branch form. Error reference: {errorId}"
                    : "Unable to load branch form.";


            return RedirectToAction(
                nameof(Index));
        }
    }
    // CREATE BRANCH - POST

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult>
        CreateBranch(
            CreateBranchViewModel model)
    {
        try
        {
            model.BranchName =
                model.BranchName?
                    .Trim()
                ?? string.Empty;


            model.Address =
                NullIfWhiteSpace(
                    model.Address);

            model.Email =
                NullIfWhiteSpace(
                    model.Email);

            model.PhoneNumber =
                NullIfWhiteSpace(
                    model.PhoneNumber);


            await LoadPartnerOptionsAsync(
                model);


            if (model.PartnerId > 0
                &&
                !string.IsNullOrWhiteSpace(
                    model.BranchName))
            {
                var exists =
                    await _userRepository
                        .BranchNameExistsAsync(
                            model.PartnerId,
                            model.BranchName);


                if (exists)
                {
                    ModelState.AddModelError(
                        nameof(
                            model.BranchName),
                        "This branch already exists for the selected partner.");
                }
            }


            if (!ModelState.IsValid)
            {
                return View(model);
            }


            var branch =
                new PartnerBranch
                {
                    PartnerId =
                        model.PartnerId,

                    BranchName =
                        model.BranchName,

                    Address =
                        model.Address,

                    Email =
                        model.Email,

                    PhoneNumber =
                        model.PhoneNumber,

                    IsActive =
                        model.IsActive
                };


            var branchId =
                await _userRepository
                    .CreateBranchAsync(
                        branch);


            TempData["SuccessMessage"] =
                "Branch created successfully.";


            /*
             * After Branch creation,
             * go directly to User creation.
             */

            return RedirectToAction(
                nameof(Create),
                new
                {
                    partnerId =
                        model.PartnerId,

                    branchId
                });
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(CreateBranch));


            ModelState.AddModelError(
                string.Empty,
                errorId.HasValue
                    ? $"Unable to create branch. Error reference: {errorId}"
                    : "Unable to create branch.");


            await LoadPartnerOptionsAsync(
                model);


            return View(model);
        }
    }
    // CREATE USER - GET

    [HttpGet]
    public async Task<IActionResult> Create(
        int? partnerId,
        int? branchId)
    {
        try
        {
            var model =
                new CreateUserViewModel
                {
                    PartnerId =
                        partnerId
                        ?? 0,

                    BranchId =
                        branchId
                        ?? 0
                };


            await LoadUserOptionsAsync(
                model);


            return View(model);
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Create));


            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Unable to load user form. Error reference: {errorId}"
                    : "Unable to load user form.";


            return RedirectToAction(
                nameof(Index));
        }
    }
    // CREATE USER - POST

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateUserViewModel model)
    {
        try
        {
            model.Username =
                model.Username?
                    .Trim()
                ?? string.Empty;


            model.Email =
                NullIfWhiteSpace(
                    model.Email);

            model.Address =
                NullIfWhiteSpace(
                    model.Address);

            model.PhoneNumber =
                NullIfWhiteSpace(
                    model.PhoneNumber);


            model.SelectedRoleIds ??=
                [];


            await LoadUserOptionsAsync(
                model);

            // Username

            if (!string.IsNullOrWhiteSpace(
                    model.Username))
            {
                var usernameExists =
                    await _userRepository
                        .UsernameExistsAsync(
                            model.Username);


                if (usernameExists)
                {
                    ModelState.AddModelError(
                        nameof(
                            model.Username),
                        "Username already exists.");
                }
            }
            // Role

            if (model.SelectedRoleIds.Count == 0)
            {
                ModelState.AddModelError(
                    nameof(
                        model.SelectedRoleIds),
                    "Select at least one role.");
            }
            else
            {
                var validRoleIds =
                    model.Roles
                        .Select(
                            x =>
                                x.RoleId)
                        .ToHashSet();


                var invalidRole =
                    model.SelectedRoleIds
                        .Distinct()
                        .Any(
                            x =>
                                !validRoleIds
                                    .Contains(x));


                if (invalidRole)
                {
                    ModelState.AddModelError(
                        nameof(
                            model.SelectedRoleIds),
                        "One or more selected roles are invalid.");
                }
            }

            // Branch belongs to selected Partner

            if (model.PartnerId > 0
                &&
                model.BranchId > 0)
            {
                var validBranch =
                    model.Branches.Any(
                        x =>
                            x.BranchId ==
                                model.BranchId

                            &&

                            x.PartnerId ==
                                model.PartnerId);


                if (!validBranch)
                {
                    ModelState.AddModelError(
                        nameof(
                            model.BranchId),
                        "Select a valid branch for the selected partner.");
                }
            }


            if (!ModelState.IsValid)
            {
                return View(model);
            }


            var user =
                new User
                {
                    PartnerId =
                        model.PartnerId,

                    BranchId =
                        model.BranchId,

                    Username =
                        model.Username,

                    PasswordHash =
                        _passwordService
                            .Hash(
                                model.Password),

                    Address =
                        model.Address,

                    Email =
                        model.Email,

                    PhoneNumber =
                        model.PhoneNumber,

                    IsActive =
                        model.IsActive
                };


            await _userRepository
                .CreateAsync(
                    user,
                    model.SelectedRoleIds);


            TempData["SuccessMessage"] =
                $"User {model.Username} created successfully.";


            return RedirectToAction(
                nameof(Index),
                new
                {
                    partnerId =
                        model.PartnerId,

                    branchId =
                        model.BranchId
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
                    ? $"Unable to create user. Error reference: {errorId}"
                    : "Unable to create user.");


            await LoadUserOptionsAsync(
                model);


            return View(model);
        }
    }
    // AJAX - BRANCHES BY PARTNER

    [HttpGet]
    public async Task<IActionResult>
        GetBranches(
            int partnerId)
    {
        if (partnerId <= 0)
        {
            return Json(
                Array.Empty<object>());
        }


        try
        {
            var branches =
                await _userRepository
                    .GetActiveBranchesAsync(
                        partnerId);


            return Json(
                branches.Select(
                    x =>
                        new
                        {
                            x.BranchId,
                            x.BranchName
                        }));
        }
        catch (Exception ex)
        {
            await LogErrorAsync(
                ex,
                nameof(GetBranches));


            return Json(
                Array.Empty<object>());
        }
    }
    // LOAD PARTNER OPTIONS

    private async Task
        LoadPartnerOptionsAsync(
            CreateBranchViewModel model)
    {
        model.Partners =
            await _userRepository
                .GetActivePartnersAsync();
    }
    // LOAD USER OPTIONS

    private async Task
        LoadUserOptionsAsync(
            CreateUserViewModel model)
    {
        model.Partners =
            await _userRepository
                .GetActivePartnersAsync();


        model.Roles =
            await _userRepository
                .GetActiveRolesAsync();


        if (model.PartnerId > 0)
        {
            model.Branches =
                await _userRepository
                    .GetActiveBranchesAsync(
                        model.PartnerId);
        }
        else
        {
            model.Branches =
                Array.Empty<
                    AmlScreening.Application.Models
                        .BranchOption>();
        }
    }
    // EMPTY STRING -> NULL

    private static string?
        NullIfWhiteSpace(
            string? value)
    {
        return string.IsNullOrWhiteSpace(
                value)
            ? null
            : value.Trim();
    }
    // ERROR LOGGING
    private Task<long?>
        LogErrorAsync(
            Exception exception,
            string action)
    {
        return _errorLogService
            .LogAsync(
                exception,
                nameof(UsersController),
                action,
                User.FindFirstValue(
                    AppClaimTypes
                        .PartnerCode));
    }
}