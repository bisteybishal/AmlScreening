using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.Aml.View)]
public sealed class AmlRecordsController : Controller
{
    private readonly IAmlRecordRepository
        _repository;

    private readonly IErrorLogService
        _errorLogService;
    private readonly ICurrentUserContext _currentUser;

    public AmlRecordsController(IAmlRecordRepository repository, IErrorLogService errorLogService, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _errorLogService = errorLogService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? recordType,
        string? status,
        int page = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            var model = await BuildPageAsync(new AmlRecordInputModel(),
                search, recordType, status, page, cancellationToken);
            return View(model);
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Index));


            return View(
                new AmlRecordsPageViewModel
                {
                    ErrorMessage =
                        errorId.HasValue
                            ? $"AML records could not be loaded. Error reference: {errorId}"
                            : "AML records could not be loaded.",

                    Records =
                        Array.Empty<AmlRecord>(),

                    Page =
                        1,

                    PageSize =
                        5,

                    TotalPages =
                        1,

                    RecordTypes =
                        AmlRecordOptions
                            .RecordTypes
                });
        }
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.Aml.Manage)]
    public async Task<IActionResult> Add()
    {
        return View(PopulateOptions(new AmlRecordInputModel()));
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.Aml.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AmlRecordInputModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            await ValidateAsync(model, null, cancellationToken);
            if (!ModelState.IsValid) return View(PopulateOptions(model));
            var record = new AmlRecord
            {
                RecordType = model.RecordType.Trim(),
                FullName = model.FullName.Trim(),
                DateOfBirth = model.DateOfBirth,
                Country = model.Country.Trim(),
                Nationality = model.Nationality.Trim(),
                ReferenceNo = model.ReferenceNo.Trim(),
                SourceName = model.SourceName.Trim(),
                Remarks = model.Remarks.Trim(),
                IsActive = model.IsActive,
                IsGlobal = false

            };
            await _repository
               .CreateAsync(
                   record,
                   cancellationToken);


            TempData["SuccessMessage"] =
                "AML record added successfully.";


            return RedirectToAction(
                nameof(Index));
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Add));


            ModelState.AddModelError(
                string.Empty,
                errorId.HasValue
                    ? $"Unable to add AML record. Error reference: {errorId}"
                    : "Unable to add AML record.");


            return View(
                PopulateOptions(model));
        }


    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.Aml.Manage)]
    public async Task<IActionResult>
        Edit(
            int id,
            CancellationToken cancellationToken)
    {
        try
        {
            if (id <= 0)
            {
                return NotFound();
            }


            var record =
                await _repository
                    .GetByIdAsync(
                        id,
                        cancellationToken);


            /*
             * Repository already filters:
             *
             * PartnerId ==
             * logged-in PartnerId
             */

            if (record == null)
            {
                return NotFound();
            }
            if (!CanEditRecord(record))
            {
                return Forbid();
            }

            var model =
                new AmlRecordInputModel
                {
                    Id =
                        record.Id,

                    RecordType =
                        record.RecordType,

                    FullName =
                        record.FullName,

                    DateOfBirth =
                        record.DateOfBirth,

                    Country =
                        record.Country
                        ?? string.Empty,

                    Nationality =
                        record.Nationality
                        ?? string.Empty,

                    ReferenceNo =
                        record.ReferenceNo,

                    SourceName =
                        record.SourceName,

                    Remarks =
                        record.Remarks
                        ?? string.Empty,

                    IsActive =
                        record.IsActive,

                    RowVersion =
                        record.RowVersion
                };


            return View(
                PopulateOptions(model));
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Edit));


            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Unable to open AML record. Error reference: {errorId}"
                    : "Unable to open AML record.";


            return RedirectToAction(
                nameof(Index));
        }
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.Aml.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult>
        Edit(
            AmlRecordInputModel model,
            CancellationToken cancellationToken)
    {
        try
        {
            if (model.Id <= 0)
            {
                return NotFound();
            }
            /*
    * Make sure this record belongs
    * to logged-in Partner.
    */

            var existing =
                await _repository
                    .GetByIdAsync(
                        model.Id,
                        cancellationToken);


            if (existing == null)
            {
                return NotFound();
            }

            if (!CanEditRecord(existing))
            {
                return Forbid();
            }
            await ValidateAsync(
                model,
                model.Id,
                cancellationToken);


            if (!ModelState.IsValid)
            {
                return View(
                    PopulateOptions(model));
            }


            var record =
                new AmlRecord
                {
                    Id =
                        model.Id,

                    RecordType =
                        model.RecordType.Trim(),

                    FullName =
                        model.FullName.Trim(),

                    DateOfBirth =
                        model.DateOfBirth,

                    Country =
                        model.Country.Trim(),

                    Nationality = NullIfWhiteSpace(model.Nationality),

                    ReferenceNo =
                        model.ReferenceNo.Trim(),

                    SourceName =
                        model.SourceName.Trim(),

                    Remarks = NullIfWhiteSpace(model.Remarks),

                    IsActive =
                        model.IsActive,

                    RowVersion =
                        model.RowVersion
                };


            var updated =
                await _repository
                    .UpdateAsync(
                        record,
                        cancellationToken);


            if (!updated)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The record was changed by another user. Reload and try again.");


                /*
                 * Reload latest RowVersion so
                 * user can retry.
                 */

                var latest =
                    await _repository
                        .GetByIdAsync(
                            model.Id,
                            cancellationToken);


                if (latest != null)
                {
                    model.RowVersion =
                        latest.RowVersion;
                }


                return View(
                    PopulateOptions(model));
            }


            TempData["SuccessMessage"] =
                "AML record updated successfully.";


            return RedirectToAction(
                nameof(Index));
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Edit));


            ModelState.AddModelError(
                string.Empty,
                errorId.HasValue
                    ? $"Unable to update AML record. Error reference: {errorId}"
                    : "Unable to update AML record.");


            return View(
                PopulateOptions(model));
        }
    }
    [HttpPost]
    [Authorize(Policy = AppPermissions.Aml.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult>
        Delete(
            int id,
            CancellationToken cancellationToken)
    {
        try
        {
            if (id <= 0)
            {
                return NotFound();
            }

            var target = await _repository.GetByIdAsync(id, cancellationToken);

            if (target == null)
            {
                return NotFound();
            }

            if (!CanEditRecord(target))
            {
                return Forbid();
            }


            var deleted =
                await _repository
                    .DeleteAsync(
                        id,
                        cancellationToken);


            if (!deleted)
            {
                return NotFound();
            }


            TempData["SuccessMessage"] =
                "AML record deleted successfully.";


            return RedirectToAction(
                nameof(Index));
        }
        catch (Exception ex)
        {
            var errorId =
                await LogErrorAsync(
                    ex,
                    nameof(Delete));


            TempData["ErrorMessage"] =
                errorId.HasValue
                    ? $"Unable to delete AML record. Error reference: {errorId}"
                    : "Unable to delete AML record.";


            return RedirectToAction(
                nameof(Index));
        }
    }
    [HttpGet]
    public async Task<IActionResult>
        Photo(
            int id,
            CancellationToken cancellationToken)
    {
        try
        {
            var record =
                await _repository
                    .GetPhotoAsync(
                        id,
                        cancellationToken);


            if (record?.Photo == null
                ||
                record.Photo.Length == 0)
            {
                return NotFound();
            }


            var contentType =
                string.IsNullOrWhiteSpace(
                    record.PhotoContentType)

                    ? "image/jpeg"

                    : record.PhotoContentType;


            return File(
                record.Photo,
                contentType);
        }
        catch (Exception ex)
        {
            await LogErrorAsync(
                ex,
                nameof(Photo));


            return NotFound();
        }
    }
    private async Task<AmlRecordsPageViewModel> BuildPageAsync(
            AmlRecordInputModel input,
            string? search,
            string? recordType,
            string? status,
            int page,
            CancellationToken cancellationToken)
    {
        const int pageSize = 5;


        bool? isActive =
            status?
                .Trim()
                .ToLowerInvariant()
            switch
            {
                "active" =>
                    true,

                "inactive" =>
                    false,

                _ =>
                    null
            };


        var summary =
            await _repository
                .GetSummaryAsync(
                    cancellationToken);


        var requestedPage =
            Math.Max(
                1,
                page);


        var pagedResult =
            await _repository
                .GetPageAsync(
                    search,
                    recordType,
                    isActive,
                    requestedPage,
                    pageSize,
                    cancellationToken);


        var totalPages =
            Math.Max(
                1,
                pagedResult.TotalPages);


        page =
            Math.Clamp(
                requestedPage,
                1,
                totalPages);


        /*
         * If requested page was outside
         * valid range, reload correct page.
         */

        if (page !=
            pagedResult.PageNumber)
        {
            pagedResult =
                await _repository
                    .GetPageAsync(
                        search,
                        recordType,
                        isActive,
                        page,
                        pageSize,
                        cancellationToken);
        }


        return new AmlRecordsPageViewModel
        {
            Input =
                input,

            Records =
                pagedResult.Items,

            Search =
                search?.Trim()
                ?? string.Empty,

            RecordType =
                recordType?.Trim()
                ?? string.Empty,

            Status =
                status?.Trim()
                ?? string.Empty,

            TotalRecords =
                summary.TotalRecords,

            ActiveRecords =
                summary.ActiveRecords,

            IndividualRecords =
                summary.IndividualRecords,

            EntityRecords =
                summary.EntityRecords,

            CountriesCovered =
                summary.CountriesCovered,

            Page =
                pagedResult.PageNumber,

            PageSize =
                pagedResult.PageSize,

            FilteredRecords =
                pagedResult.TotalCount,

            TotalPages =
                Math.Max(
                    1,
                    pagedResult.TotalPages),

            RecordTypes =
                AmlRecordOptions
                    .RecordTypes
        };
    }

    private async Task ValidateAsync(
         AmlRecordInputModel model,
         int? excludedId,
         CancellationToken cancellationToken)
    {
        if (model.DateOfBirth.HasValue
            &&
            model.DateOfBirth.Value.Date >
            DateTime.UtcNow.Date)
        {
            ModelState.AddModelError(
                nameof(model.DateOfBirth),
                "The date cannot be in the future.");
        }


        if (string.IsNullOrWhiteSpace(
                model.ReferenceNo)

            ||

            string.IsNullOrWhiteSpace(
                model.SourceName))
        {
            return;
        }


        var exists =
            await _repository
                .ReferenceExistsAsync(
                    model.ReferenceNo,
                    model.SourceName,
                    excludedId,
                    cancellationToken);


        if (exists)
        {
            ModelState.AddModelError(
                nameof(model.ReferenceNo),
                "This reference number already exists for this source.");
        }
    }
    // DROPDOWN OPTIONS

    private static AmlRecordInputModel
        PopulateOptions(
            AmlRecordInputModel model)
    {
        model.RecordTypes =
            IncludeCurrentValue(
                AmlRecordOptions.RecordTypes,
                model.RecordType);


        model.Countries =
            IncludeCurrentValue(
                AmlRecordOptions.Countries,
                model.Country);


        model.Nationalities =
            IncludeCurrentValue(
                AmlRecordOptions.Nationalities,
                model.Nationality);


        model.Sources =
            IncludeCurrentValue(
                AmlRecordOptions.Sources,
                model.SourceName);


        return model;
    }


    private static IReadOnlyList<string>
        IncludeCurrentValue(
            IReadOnlyList<string> values,
            string? currentValue)
    {
        if (string.IsNullOrWhiteSpace(
                currentValue))
        {
            return values;
        }


        if (values.Any(x =>
                string.Equals(
                    x,
                    currentValue,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return values;
        }


        return values
            .Append(
                currentValue)
            .ToList();
    }


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
                nameof(AmlRecordsController),
                action,
                User.FindFirstValue(
                    AppClaimTypes.PartnerCode));
    }

    private bool CanEditRecord(AmlRecord record)
    {
        return _currentUser.HasPermission(AppPermissions.Aml.Manage)
            && !record.IsGlobal
            && record.PartnerId == _currentUser.PartnerId;
    }
}
