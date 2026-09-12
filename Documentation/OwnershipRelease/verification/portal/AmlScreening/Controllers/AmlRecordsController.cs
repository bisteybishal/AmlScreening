using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using AmlScreening.Portal.Security;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.AmlRecordsView)]
public sealed class AmlRecordsController : Controller
{
    private readonly IAmlRecordRepository _repository;
    private readonly IAuthorizationService _authorizationService;

    public AmlRecordsController(
        IAmlRecordRepository repository,
        IAuthorizationService authorizationService)
    {
        _repository = repository;
        _authorizationService = authorizationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? recordType,
        string? status,
        int page = 1)
    {
        return View(await BuildPageAsync(
            new AmlRecordInputModel(), search, recordType, status, page));
    }



    private async Task<AmlRecordsPageViewModel> BuildPageAsync(
        AmlRecordInputModel input,
        string? search,
        string? recordType,
        string? status,
        int page = 1)
    {
        const int pageSize = 5;
        bool? isActive = status?.ToLowerInvariant() switch
        {
            "active" => true,
            "inactive" => false,
            _ => null
        };

        var summary = await _repository.GetSummaryAsync();
        var requestedPage = Math.Max(1, page);
        var pagedResult = await _repository.GetPageAsync(
            search, recordType, isActive, requestedPage, pageSize);
        var totalPages = Math.Max(1, pagedResult.TotalPages);
        page = Math.Clamp(page, 1, totalPages);

        if (page != requestedPage)
        {
            pagedResult = await _repository.GetPageAsync(
                search, recordType, isActive, page, pageSize);
        }

        return new AmlRecordsPageViewModel
        {
            Input = input,
            Records = pagedResult.Items,
            Search = search?.Trim() ?? string.Empty,
            RecordType = recordType ?? string.Empty,
            Status = status ?? string.Empty,
            TotalRecords = summary.TotalRecords,
            ActiveRecords = summary.ActiveRecords,
            IndividualRecords = summary.IndividualRecords,
            EntityRecords = summary.EntityRecords,
            CountriesCovered = summary.CountriesCovered,
            Page = page,
            PageSize = pageSize,
            FilteredRecords = pagedResult.TotalCount,
            TotalPages = totalPages,
            RecordTypes = AmlRecordOptions.RecordTypes
        };
    }


    [HttpGet]
    [Authorize(Policy = AppPermissions.AmlRecordsCreate)]
    public async Task<IActionResult> Add()
    {
        return View(PopulateOptions(new AmlRecordInputModel()));
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.AmlRecordsCreate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AmlRecordInputModel model)
    {
        await ValidateAsync(model);
        if (!ModelState.IsValid) return View(PopulateOptions(model));

        string userId = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Unknown";
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
            CreatedDate = DateTime.UtcNow,
            CreatedBy = userId
        };

        await _repository.CreateAsync(record);
        TempData["SuccessMessage"] = "AML record added successfully.";
        return RedirectToAction("Index", "AmlRecords");
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.AmlRecordsEdit)]
    public async Task<IActionResult> Edit(int id)
    {
        var record = await _repository.GetByIdAsync(id);
        if (record is null) return NotFound();
        if (!(await _authorizationService.AuthorizeAsync(User, record, AmlRecordOperations.Update)).Succeeded)
            return Forbid();

        return View(PopulateOptions(new AmlRecordInputModel
        {
            Id = record.Id,
            RecordType = record.RecordType,
            FullName = record.FullName,
            DateOfBirth = record.DateOfBirth,
            Country = record.Country ?? string.Empty,
            Nationality = record.Nationality ?? string.Empty,
            ReferenceNo = record.ReferenceNo,
            SourceName = record.SourceName,
            Remarks = record.Remarks ?? string.Empty,
            IsActive = record.IsActive,
            RowVersion = record.RowVersion
        }));
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.AmlRecordsEdit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AmlRecordInputModel model)
    {
        var existing = await _repository.GetByIdAsync(model.Id);
        if (existing is null) return NotFound();
        if (!(await _authorizationService.AuthorizeAsync(User, existing, AmlRecordOperations.Update)).Succeeded)
            return Forbid();

        if (model.DateOfBirth > DateTime.Today)
            ModelState.AddModelError(nameof(model.DateOfBirth), "The date cannot be in the future.");

        var reference = model.ReferenceNo?.Trim();
        if (!string.IsNullOrWhiteSpace(reference) &&
            !string.IsNullOrWhiteSpace(model.SourceName) &&
            await _repository.ReferenceExistsAsync(reference, model.SourceName, model.Id))
        {
            ModelState.AddModelError(nameof(model.ReferenceNo), "This reference number already exists.");
        }

        if (!ModelState.IsValid) return View(PopulateOptions(model));

        string userId = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Unknown";
        existing.RecordType = model.RecordType.Trim();
        existing.FullName = model.FullName.Trim();
        existing.DateOfBirth = model.DateOfBirth;
        existing.Country = model.Country.Trim();
        existing.Nationality = model.Nationality.Trim();
        existing.ReferenceNo = reference!;
        existing.SourceName = model.SourceName.Trim();
        existing.Remarks = model.Remarks.Trim();
        existing.IsActive = model.IsActive;
        existing.UpdatedDate = DateTime.UtcNow;
        existing.RowVersion = model.RowVersion;


        if (!await _repository.UpdateAsync(existing, userId))
        {
            ModelState.AddModelError(string.Empty,
                "This record was changed by another user. Reload the record and try again.");
            return View(PopulateOptions(model));
        }
        TempData["SuccessMessage"] = "AML record updated successfully.";
        return RedirectToAction("Index", "AmlRecords");
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.AmlRecordsDelete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await _repository.GetByIdAsync(id);
        if (record is null) return NotFound();
        if (!(await _authorizationService.AuthorizeAsync(User, record, AmlRecordOperations.Delete)).Succeeded)
            return Forbid();

        var deletedBy = User.FindFirstValue(ClaimTypes.Name)
            ?? User.Identity?.Name
            ?? "Unknown";

        if (id <= 0 || !await _repository.DeleteAsync(id, deletedBy)) return NotFound();
        TempData["SuccessMessage"] = "AML record deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateAsync(AmlRecordInputModel model)
    {
        if (model.DateOfBirth > DateTime.Today)
            ModelState.AddModelError(nameof(model.DateOfBirth), "The date cannot be in the future.");

        var reference = model.ReferenceNo?.Trim();
        if (!string.IsNullOrWhiteSpace(reference) &&
            !string.IsNullOrWhiteSpace(model.SourceName) &&
            await _repository.ReferenceExistsAsync(reference, model.SourceName))
        {
            ModelState.AddModelError(nameof(model.ReferenceNo), "This reference number already exists.");
        }
    }

    private static AmlRecordInputModel PopulateOptions(AmlRecordInputModel model)
    {
        model.RecordTypes = IncludeCurrentValue(AmlRecordOptions.RecordTypes, model.RecordType);
        model.Countries = IncludeCurrentValue(AmlRecordOptions.Countries, model.Country);
        model.Nationalities = IncludeCurrentValue(AmlRecordOptions.Nationalities, model.Nationality);
        model.Sources = IncludeCurrentValue(AmlRecordOptions.Sources, model.SourceName);
        return model;
    }

    private static IReadOnlyList<string> IncludeCurrentValue(
        IReadOnlyList<string> values,
        string? currentValue)
    {
        if (string.IsNullOrWhiteSpace(currentValue) ||
            values.Any(item => string.Equals(
                item, currentValue, StringComparison.OrdinalIgnoreCase)))
        {
            return values;
        }

        return values.Append(currentValue).ToList();
    }
}
