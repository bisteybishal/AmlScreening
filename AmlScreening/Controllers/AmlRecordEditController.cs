using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using AmlScreening.Application.Security;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.AmlRecordsEdit)]
public sealed class AmlRecordEditController : Controller
{
    private readonly IAmlRecordRepository _repository;

    public AmlRecordEditController(IAmlRecordRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int id)
    {
        var record = await _repository.GetByIdAsync(id);
        if (record is null) return NotFound();

        return View(new AmlRecordInputModel
        {
            Id = record.Id,
            RecordType = record.RecordType,
            FullName = record.FullName,
            DateOfBirth = record.DateOfBirth,
            Country = record.Country,
            Nationality = record.Nationality,
            ReferenceNo = record.ReferenceNo,
            SourceName = record.SourceName,
            Remarks = record.Remarks,
            IsActive = record.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AmlRecordInputModel model)
    {
        var existing = await _repository.GetByIdAsync(model.Id);
        if (existing is null) return NotFound();

        if (model.DateOfBirth > DateTime.Today)
            ModelState.AddModelError(nameof(model.DateOfBirth), "The date cannot be in the future.");

        var reference = model.ReferenceNo?.Trim();
        if (!string.IsNullOrWhiteSpace(reference) &&
            (await _repository.GetAllAsync()).Any(x => x.Id != model.Id && string.Equals(x.ReferenceNo, reference, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(nameof(model.ReferenceNo), "This reference number already exists.");
        }

        if (!ModelState.IsValid) return View(model);

        string userId = User.FindFirstValue(ClaimTypes.Name)??User.Identity?.Name ?? "Unknown";
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
        

        await _repository.UpdateAsync(existing, userId);
        TempData["SuccessMessage"] = "AML record updated successfully.";
        return RedirectToAction("Index", "AmlRecords");
    }
}
