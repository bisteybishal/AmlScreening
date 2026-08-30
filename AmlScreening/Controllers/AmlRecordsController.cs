using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Domain.Entities;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using AmlScreening.Application.Security;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.AmlRecordsView)]
public sealed class AmlRecordsController : Controller
{
    private readonly IAmlRecordRepository _repository;

    public AmlRecordsController(IAmlRecordRepository repository)
    {
        _repository = repository;
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

    [HttpPost]
    [Authorize(Policy = AppPermissions.AmlRecordsDelete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var deletedBy = User.FindFirstValue(ClaimTypes.Name)
            ?? User.Identity?.Name
            ?? "Unknown";

        if (id <= 0 || !await _repository.DeleteAsync(id, deletedBy)) return NotFound();
        TempData["SuccessMessage"] = "AML record deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AmlRecordsPageViewModel> BuildPageAsync(
        AmlRecordInputModel input,
        string? search,
        string? recordType,
        string? status,
        int page = 1,
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

        const int pageSize = 5;

        var filteredRecords = filtered.ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(filteredRecords.Count / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var pagedRecords = filteredRecords
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new AmlRecordsPageViewModel
        {
            Input = input,
            Records = pagedRecords,
            Search = search?.Trim() ?? string.Empty,
            RecordType = recordType ?? string.Empty,
            Status = status ?? string.Empty,
            TotalRecords = all.Count,
            ActiveRecords = all.Count(x => x.IsActive),
            IndividualRecords = all.Count(x => x.RecordType.Equals("Individual", StringComparison.OrdinalIgnoreCase)),
            EntityRecords = all.Count(x => x.RecordType.Equals("Entity", StringComparison.OrdinalIgnoreCase)),
            CountriesCovered = all.Where(x => !string.IsNullOrWhiteSpace(x.Country)).Select(x => x.Country).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Page = page,
            PageSize = pageSize,
            FilteredRecords = filteredRecords.Count,
            TotalPages = totalPages
        };
    }
}
