using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Domain.Entities;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AmlScreening.Portal.Controllers;

[Authorize]
public sealed class AmlRecordsController : Controller
{
    private readonly IAmlRecordRepository _repository;

    public AmlRecordsController(IAmlRecordRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? recordType, string? status)
    {
        return View(await BuildPageAsync(new AmlRecordInputModel(), search, recordType, status));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AmlRecordsPageViewModel model)
    {
        if (model.Input.DateOfBirth > DateTime.Today)
        {
            ModelState.AddModelError("Input.DateOfBirth", "The date cannot be in the future.");
        }

        var allRecords = await _repository.GetAllAsync();
        if (allRecords.Any(x => x.Id != model.Input.Id &&
            x.ReferenceNo.Equals(model.Input.ReferenceNo?.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("Input.ReferenceNo", "This reference number already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageAsync(model.Input, model.Search, model.RecordType, model.Status, allRecords));
        }

        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;
        if (model.Input.Id == 0)
        {
            var record = Map(model.Input);
            record.CreatedDate = DateTime.UtcNow;
            record.CreatedBy = userId;
            await _repository.CreateAsync(record);
            TempData["SuccessMessage"] = "AML record added successfully.";
        }
        else
        {
            var existing = await _repository.GetByIdAsync(model.Input.Id);
            if (existing is null) return NotFound();

            existing.RecordType = model.Input.RecordType.Trim();
            existing.FullName = model.Input.FullName.Trim();
            existing.DateOfBirth = model.Input.DateOfBirth;
            existing.Country = model.Input.Country.Trim();
            existing.Nationality = model.Input.Nationality.Trim();
            existing.ReferenceNo = model.Input.ReferenceNo.Trim();
            existing.SourceName = model.Input.SourceName.Trim();
            existing.Remarks = model.Input.Remarks.Trim();
            existing.IsActive = model.Input.IsActive;
            existing.UpdatedDate = DateTime.UtcNow;
            existing.UpdatedBy = userId;
            await _repository.UpdateAsync(existing);
            TempData["SuccessMessage"] = "AML record updated successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (id <= 0 || !await _repository.DeleteAsync(id)) return NotFound();
        TempData["SuccessMessage"] = "AML record deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AmlRecordsPageViewModel> BuildPageAsync(
        AmlRecordInputModel input,
        string? search,
        string? recordType,
        string? status,
        IReadOnlyList<AmlRecord>? loadedRecords = null)
    {
        var all = loadedRecords ?? await _repository.GetAllAsync();
        IEnumerable<AmlRecord> filtered = all;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(x => x.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.ReferenceNo.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.SourceName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(recordType))
            filtered = filtered.Where(x => x.RecordType.Equals(recordType, StringComparison.OrdinalIgnoreCase));
        if (status == "active") filtered = filtered.Where(x => x.IsActive);
        if (status == "inactive") filtered = filtered.Where(x => !x.IsActive);

        return new AmlRecordsPageViewModel
        {
            Input = input,
            Records = filtered.ToList(),
            Search = search?.Trim() ?? string.Empty,
            RecordType = recordType ?? string.Empty,
            Status = status ?? string.Empty,
            TotalRecords = all.Count,
            ActiveRecords = all.Count(x => x.IsActive),
            IndividualRecords = all.Count(x => x.RecordType.Equals("Individual", StringComparison.OrdinalIgnoreCase)),
            EntityRecords = all.Count(x => x.RecordType.Equals("Entity", StringComparison.OrdinalIgnoreCase)),
            CountriesCovered = all.Where(x => !string.IsNullOrWhiteSpace(x.Country)).Select(x => x.Country).Distinct(StringComparer.OrdinalIgnoreCase).Count()
        };
    }

    private static AmlRecord Map(AmlRecordInputModel input) => new()
    {
        RecordType = input.RecordType.Trim(),
        FullName = input.FullName.Trim(),
        DateOfBirth = input.DateOfBirth,
        Country = input.Country.Trim(),
        Nationality = input.Nationality.Trim(),
        ReferenceNo = input.ReferenceNo.Trim(),
        SourceName = input.SourceName.Trim(),
        Remarks = input.Remarks.Trim(),
        IsActive = input.IsActive
    };
}
