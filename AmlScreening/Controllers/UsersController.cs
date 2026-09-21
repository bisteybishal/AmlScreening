using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using AmlScreening.Portal.ViewModels;
using AspNetCoreGeneratedDocument;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using static AppPermissions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace AmlScreening.Portal.Controllers;

[Authorize(
    Policy = AppPermissions.Users.Manage)]
public sealed class UsersController : Controller
{
    private readonly IUserRepository
        _userRepository;

    private readonly IPasswordService
        _passwordService;

    private readonly IErrorLogService
        _errorLogService;


    public UsersController(
        IUserRepository userRepository,
        IPasswordService passwordService,
        IErrorLogService errorLogService)
    {
        _userRepository =
            userRepository;

        _passwordService =
            passwordService;

        _errorLogService =
            errorLogService;
    }


    // =========================================================
    // USERS LIST
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Index(
        int? partnerId,
        int? branchId,
        int page = 1)
    {
        try
        {
            const int pageSize = 10;


            page =
                Math.Max(
                    1,
                    page);


            var result =
                await _userRepository
                    .GetPartnersAsync(
                        page,
                        pageSize);
                       
            var model =
                new UsersPageViewModel
                {
                    Partners = result.Items,

                    TotalPartners =
                    result.TotalCount,

                    ActivePartners =
                    result.Items.Count(x => x.IsActive),

                    IActivePartners =
                    result.Items.Count(x => !x.IsActive),

                    TotalUsers =
                    result.Items.Sum(x => x.UserCount),

                    Page =
                    result.PageNumber,

                    PageSize =
                    result.PageSize,

                    TotalPages =
                    result.TotalPages
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
                    ErrorMessage =
                        errorId.HasValue

                            ? $"Partners could not be loaded. Error reference: {errorId.Value}"
                        : "Partners could not be loaded."
                });
        }
    }


    // =========================================================
    // CREATE PARTNER - GET
    // =========================================================

    [HttpGet]
    [Authorize(Policy = AppPermissions.Users.Manage)]
    public IActionResult CreatePartner()
    {
        return View(
            new CreatePartnerViewModel
            {
                IsActive =
                    true
            });
    }


    // =========================================================
    // CREATE PARTNER - POST


    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.Users.Manage)]
    public async Task<IActionResult> CreatePartner(
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


            // ---------------------------------------------
            // Partner Code Duplicate Check
            // ---------------------------------------------

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
             * Flow:
             *
             * Partner
             *   ↓
             * Branch
             *   ↓
             * User
             */

            return RedirectToAction(
                nameof(CreatePartner),
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

                    ? $"Unable to create partner. Error reference: {errorId.Value}"

                    : "Unable to create partner.");


            return View(model);
        }
    }

    // edit partner 
    [HttpGet]
    public async Task<IActionResult> EditPartner(int partnerid)
    {
        try
        {
            if (partnerid <= 0)
            {
                return NotFound();
            }
            var partner = await _userRepository.GetPartnerByIdAsync(partnerid);
            if (partner == null)
            {
                return NotFound();
            }
            var model = new EditPartnerViewModel
            {
                PartnerId = partner.PartnerId,
                PartnerName = partner.PartnerName,
                PartnerCode = partner.PartnerCode,
                Address = partner.Address,
                Email = partner.Email,
                PhoneNumber = partner.PhoneNumber,
                IsActive = partner.IsActive
            };
            return View(model);
        }
        catch (Exception ex)
        {
            var errorId =
           await LogErrorAsync(
               ex,
               nameof(EditPartner));

            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Partner could not be loaded. Error reference: {errorId.Value}"
                    : "Partner could not be loaded.";

            return RedirectToAction(
                nameof(Index));
        }
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPartner(EditPartnerViewModel model)
    {
        try
        {
            model.PartnerCode = model.PartnerCode?.Trim().ToUpperInvariant() ?? string.Empty;
            model.PartnerName = model.PartnerName?.Trim() ?? string.Empty;
            model.Address =
            NullIfWhiteSpace(
                model.Address);

            model.Email =
                NullIfWhiteSpace(
                    model.Email);

            model.PhoneNumber =
                NullIfWhiteSpace(
                    model.PhoneNumber);
            if (model.PartnerId <= 0)
            {
                return NotFound();
            }
            var updatedPartner = await _userRepository.GetPartnerByIdAsync(model.PartnerId);
            if (updatedPartner == null)
            {
                return NotFound();
            }
            if (!string.IsNullOrWhiteSpace(model.PartnerCode))
            {
                var duplicate = await _userRepository.PartnerCodeExistsAsync(model.PartnerCode, model.PartnerId);
                if (duplicate)
                {
                    ModelState.AddModelError(nameof(model.PartnerCode), "Partner code already exists.");

                }
            }
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var partner = new Partner
            {
                PartnerId =
                    model.PartnerId,

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
            var updated = await _userRepository.UpdatePartnerAsync(partner);
            if (!updated)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Partner could not be updated.");

                return View(model);
            }
            TempData["SuccessMessage"] =
            "Partner updated successfully.";


            return RedirectToAction(
                nameof(Index));

        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(EditPartner));

            ModelState.AddModelError(
                string.Empty,
                errorId.HasValue
                    ? $"Partner could not be updated. Error reference: {errorId.Value}"
                    : "Partner could not be updated.");

            return View(model);
        }

    }



    [HttpGet]
    public async Task<IActionResult> PartnerBranchesDetail(int partnerId)
    {
        try
        {

            if (partnerId <= 0)
            {
                return NotFound();
            }
            var partner = await _userRepository.GetPartnerByIdAsync(partnerId);
            if (partner == null)
            {
                return NotFound();
            }

            var branches = await _userRepository.GetBranchesByPartnerAsync(partnerId);
            var branchModel = new PartnerBranchesViewModel
            {
                PartnerId = partnerId,
                PartnerCode = partner.PartnerCode,
                PartnerName = partner.PartnerName,
                IsActive =
                        partner.IsActive,

                Branches =
                        branches
            };
            return View(branchModel);
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(PartnerBranchesDetail));


            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Branches could not be loaded. Error reference: {errorId.Value}"
                    : "Branches could not be loaded.";


            return RedirectToAction(
                nameof(Index));
        }

    }

    // =========================================================
    // CREATE BRANCH - GET
    // =========================================================

    [HttpGet]
    [Authorize(Policy = AppPermissions.Users.Manage)]
    public async Task<IActionResult> CreateBranch(
        int? partnerId)
    {
        try
        {
            var model =
                new CreateBranchViewModel
                {
                    PartnerId =
                        partnerId
                        ?? 0,

                    IsActive =
                        true
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

                    ? $"Unable to load branch form. Error reference: {errorId.Value}"

                    : "Unable to load branch form.";


            return RedirectToAction(
                nameof(Index));
        }
    }


    // =========================================================
    // CREATE BRANCH - POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy =
            AppPermissions.Users.Manage)]
    public async Task<IActionResult> CreateBranch(
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


            // ---------------------------------------------
            // Partner validation
            // ---------------------------------------------

            if (model.PartnerId <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.PartnerId),
                    "Select a partner.");
            }
            else
            {
                var partnerExists =
                    model.Partners.Any(
                        x =>
                            x.PartnerId ==
                                model.PartnerId);


                if (!partnerExists)
                {
                    ModelState.AddModelError(
                        nameof(model.PartnerId),
                        "Selected partner is invalid.");
                }
            }


            // ---------------------------------------------
            // Duplicate Branch
            // ---------------------------------------------

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

                    ? $"Unable to create branch. Error reference: {errorId.Value}"

                    : "Unable to create branch.");


            await LoadPartnerOptionsAsync(
                model);


            return View(model);
        }
    }

    //Edit Branch Detail
    [HttpGet]
    public async Task<IActionResult> EditBranchDetail(int branchid)
    {
        try
        {
            if (branchid <= 0)
            {
                return NotFound();
            }
            var branch = await _userRepository.GetBranchByIdAsync(branchid);
            if (branch == null)
            {
                return NotFound();
            }
            var branchmodel = new EditBranchViewModel
            {
                BranchId = branch.BranchId,
                PartnerId = branch.PartnerId,
                BranchName = branch.BranchName,
                Email = branch.Email,
                PhoneNumber = branch.PhoneNumber,
                IsActive = branch.IsActive

            };
            return View(branchmodel);
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(EditBranchDetail));

            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Branch could not be loaded. Error reference: {errorId.Value}"
                    : "Branch could not be loaded.";

            return RedirectToAction(
                nameof(Index));
        }
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBranchDetail(EditBranchViewModel model)
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

            if (model.BranchId <= 0 ||
          model.PartnerId <= 0)
            {
                return NotFound();
            }
            var existing =
           await _userRepository
               .GetBranchByIdAsync(
                   model.BranchId);

            if (existing == null ||
                existing.PartnerId != model.PartnerId)
            {
                return NotFound();
            }
            var duplicate =
           await _userRepository
               .BranchNameExistsAsync(
                   model.PartnerId,
                   model.BranchName,
                   model.BranchId);


            if (duplicate)
            {
                ModelState.AddModelError(
                    nameof(model.BranchName),
                    "This branch already exists for the selected partner.");
            }
            if (!ModelState.IsValid)
            {
                return View(model);
            }


            var branch = new PartnerBranch
            {
                BranchId = model.BranchId,
                PartnerId = model.PartnerId,
                BranchName = model.BranchName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                Address = model.Address,
                IsActive = model.IsActive
            };
            var updated = await _userRepository.UpdateBranchAsync(branch);
            if (!updated)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Branch could not be updated.");

                return View(model);
            }


            TempData["SuccessMessage"] =
                "Branch updated successfully.";


            return RedirectToAction(
                nameof(PartnerBranchesDetail),
                new
                {
                    partnerId =
                        model.PartnerId
                });
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(EditBranchDetail));

            ModelState.AddModelError(
                string.Empty,
                errorId.HasValue
                    ? $"Branch could not be updated. Error reference: {errorId.Value}"
                    : "Branch could not be updated.");

            return View(model);
        }
    }

    // =========================================================
    // CREATE USER - GET
    // =========================================================

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
                        ?? 0,

                    IsActive =
                        true
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

                    ? $"Unable to load user form. Error reference: {errorId.Value}"

                    : "Unable to load user form.";


            return RedirectToAction(
                nameof(Index));
        }
    }


    // =========================================================
    // CREATE USER - POST
    // =========================================================

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


            // ---------------------------------------------
            // USERNAME
            // ---------------------------------------------

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


            // ---------------------------------------------
            // PARTNER
            // ---------------------------------------------

            if (model.PartnerId <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.PartnerId),
                    "Select a partner.");
            }
            else
            {
                var partnerValid =
                    model.Partners.Any(
                        x =>
                            x.PartnerId ==
                                model.PartnerId);


                if (!partnerValid)
                {
                    ModelState.AddModelError(
                        nameof(model.PartnerId),
                        "Selected partner is invalid.");
                }
            }


            // ---------------------------------------------
            // BRANCH
            // ---------------------------------------------

            if (model.BranchId <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.BranchId),
                    "Select a branch.");
            }
            else if (model.PartnerId > 0)
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


            // ---------------------------------------------
            // ROLES
            // ---------------------------------------------

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
                            roleId =>
                                !validRoleIds
                                    .Contains(
                                        roleId));


                if (invalidRole)
                {
                    ModelState.AddModelError(
                        nameof(
                            model.SelectedRoleIds),
                        "One or more selected roles are invalid.");
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

                    ? $"Unable to create user. Error reference: {errorId.Value}"

                    : "Unable to create user.");


            await LoadUserOptionsAsync(
                model);


            return View(model);
        }
    }


    // =========================================================
    // USER ACCESS - GET
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Access(
        long id)
    {
        try
        {
            if (id <= 0)
            {
                return NotFound();
            }


            var user =
                await _userRepository
                    .GetAccessAsync(
                        id);


            if (user == null)
            {
                return NotFound();
            }


            var roles =
                await _userRepository
                    .GetActiveRolesAsync();


            var selectedRoleIds =
                await _userRepository
                    .GetUserRoleIdsAsync(
                        id);


            var model =
                new UserAccessViewModel
                {
                    UserId =
                        user.UserId,

                    Username =
                        user.Username,

                    PartnerName =
                        user.PartnerName,

                    BranchName =
                        user.BranchName,

                    IsActive =
                        user.IsActive,

                    SelectedRoleIds =
                        selectedRoleIds
                            .ToList(),

                    Roles =
                        roles
                };


            return View(model);
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Access));


            TempData["ErrorMessage"] =
                errorId.HasValue

                    ? $"User access could not be loaded. Error reference: {errorId.Value}"

                    : "User access could not be loaded.";


            return RedirectToAction(
                nameof(Index));
        }
    }


    // =========================================================
    // USER ACCESS - POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Access(
        UserAccessViewModel model)
    {
        try
        {
            model.SelectedRoleIds ??=
                [];


            // ---------------------------------------------
            // Load actual user from DB.
            //
            // Never trust Username / Partner / Branch
            // posted from the browser.
            // ---------------------------------------------

            var user =
                await _userRepository
                    .GetAccessAsync(
                        model.UserId);


            if (user == null)
            {
                return NotFound();
            }


            var roles =
                await _userRepository
                    .GetActiveRolesAsync();


            model.Username =
                user.Username;

            model.PartnerName =
                user.PartnerName;

            model.BranchName =
                user.BranchName;

            model.Roles =
                roles;


            // ---------------------------------------------
            // ROLE REQUIRED
            // ---------------------------------------------

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
                    roles
                        .Select(
                            x =>
                                x.RoleId)
                        .ToHashSet();


                var invalidRole =
                    model.SelectedRoleIds
                        .Distinct()
                        .Any(
                            roleId =>
                                !validRoleIds
                                    .Contains(
                                        roleId));


                if (invalidRole)
                {
                    ModelState.AddModelError(
                        nameof(
                            model.SelectedRoleIds),
                        "One or more selected roles are invalid.");
                }
            }


            if (!ModelState.IsValid)
            {
                /*
                 * Keep actual DB status if the
                 * validation fails.
                 */

                model.IsActive =
                    user.IsActive;


                return View(model);
            }


            // ---------------------------------------------
            // UPDATE
            // ---------------------------------------------

            await _userRepository
                .UpdateAccessAsync(
                    model.UserId,
                    model.IsActive,
                    model.SelectedRoleIds);


            TempData["SuccessMessage"] =
                $"Access for {user.Username} updated successfully. "
                +
                "The user should sign in again to receive the current permissions.";


            return RedirectToAction(
                nameof(Access),
                new
                {
                    id =
                        model.UserId
                });
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Access));


            ModelState.AddModelError(
                string.Empty,
                errorId.HasValue

                    ? $"User access could not be updated. Error reference: {errorId.Value}"

                    : "User access could not be updated.");


            // ---------------------------------------------
            // Reload non-posted display/options data
            // ---------------------------------------------

            try
            {
                var user =
                    await _userRepository
                        .GetAccessAsync(
                            model.UserId);


                if (user != null)
                {
                    model.Username =
                        user.Username;

                    model.PartnerName =
                        user.PartnerName;

                    model.BranchName =
                        user.BranchName;

                    model.IsActive =
                        user.IsActive;
                }


                model.Roles =
                    await _userRepository
                        .GetActiveRolesAsync();
            }
            catch
            {
                model.Roles =
                    Array.Empty<RoleOption>();
            }


            return View(model);
        }
    }


    [HttpGet]
    public async Task<IActionResult> BranchUsers(
    int partnerId,
    int branchId,
    int page = 1)
    {
        try
        {
            if (partnerId <= 0 ||
                branchId <= 0)
            {
                return NotFound();
            }


            var partner =
                await _userRepository
                    .GetPartnerByIdAsync(
                        partnerId);


            var branch =
                await _userRepository
                    .GetBranchByIdAsync(
                        branchId);


            if (partner == null ||
                branch == null ||
                branch.PartnerId != partnerId)
            {
                return NotFound();
            }


            const int pageSize = 10;


            var result =
                await _userRepository
                    .GetPageAsync(
                        partnerId,
                        branchId,
                        Math.Max(1, page),
                        pageSize);


            var model =
                new BranchUsersViewModel
                {
                    PartnerId =
                        partnerId,

                    PartnerName =
                        partner.PartnerName,

                    BranchId =
                        branchId,

                    BranchName =
                        branch.BranchName,

                    Users =
                        result.Items,

                    TotalUsers =
                        result.TotalUsers,

                    ActiveUsers =
                        result.ActiveUsers,

                    InactiveUsers =
                        result.InactiveUsers,

                    Page =
                        result.PageNumber,

                    PageSize =
                        result.PageSize,

                    TotalPages =
                        Math.Max(
                            1,
                            result.TotalPages)
        
                    
                };


            return View(model);
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(BranchUsers));

            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Users could not be loaded. Error reference: {errorId.Value}"
                    : "Users could not be loaded.";

            return RedirectToAction(
                nameof(PartnerBranchesDetail),
                new
                {
                    partnerId
                });
        }
    }



    // =========================================================
    // AJAX - BRANCHES BY PARTNER
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> GetBranches(
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


    // =========================================================
    // LOAD PARTNER OPTIONS
    // =========================================================

    private async Task LoadPartnerOptionsAsync(
        CreateBranchViewModel model)
    {
        model.Partners =
            await _userRepository
                .GetActivePartnersAsync();
    }


    // =========================================================
    // LOAD USER OPTIONS
    // =========================================================

    private async Task LoadUserOptionsAsync(
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
                Array.Empty<BranchOption>();
        }
    }


    // =========================================================
    // EMPTY STRING -> NULL
    // =========================================================

    private static string? NullIfWhiteSpace(
        string? value)
    {
        return string.IsNullOrWhiteSpace(
                value)

            ? null

            : value.Trim();
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
                nameof(UsersController),
                action,
                User.FindFirstValue(
                    AppClaimTypes.PartnerCode));
    }


}