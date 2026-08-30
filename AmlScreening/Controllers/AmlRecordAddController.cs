using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Domain.Entities;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using AmlScreening.Application.Security;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.AmlRecordsCreate)]
public sealed class AmlRecordAddController : Controller
{
    private readonly IAmlRecordRepository _repository;

    public AmlRecordAddController(IAmlRecordRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new AmlRecordInputModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AmlRecordInputModel model)
    {
        try
        {
            await ValidateAsync(model);
            if (!ModelState.IsValid) return View(model);

            string userId = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "UnKnown";
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
        catch
        {
            return RedirectToAction("Index", "Home");
        }
    }

    private async Task ValidateAsync(AmlRecordInputModel model)
    {
        if (model.DateOfBirth > DateTime.Today)
            ModelState.AddModelError(nameof(model.DateOfBirth), "The date cannot be in the future.");

        var reference = model.ReferenceNo?.Trim();
        if (!string.IsNullOrWhiteSpace(reference) &&
            (await _repository.GetAllAsync()).Any(x => string.Equals(x.ReferenceNo, reference, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(nameof(model.ReferenceNo), "This reference number already exists.");
        }
    }
}
