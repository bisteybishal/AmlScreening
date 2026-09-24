using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AmlScreening.Portal.Controllers;

[Authorize(Policy = AppPermissions.RiskScoreSetup.View)]
public sealed class RiskScoreSetupController : Controller
{
    private const string ReviewView =
        "~/Views/Shared/RiskApprovalReview.cshtml";

    private readonly IRiskScoringService _riskScoring;
    private readonly IErrorLogService _errorLogService;

    public RiskScoreSetupController(
        IRiskScoringService riskScoring,
        IErrorLogService errorLogService)
    {
        _riskScoring = riskScoring;
        _errorLogService = errorLogService;
    }

    // =====================================================
    // SETUP PAGE
    // =====================================================

    [HttpGet("/RiskScoreSetup/Index", Order = 0)]
    [HttpGet("/RiskScoring/Setup", Order = 1)]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await LoadSetupAsync(cancellationToken);

            return View(
                "Index",
                RiskScoringPageViewModel.From(result));
        }
        catch (RiskConfigurationException ex)
        {
            ViewBag.ErrorMessage = ex.Message;

            return View(
                "Index",
                new RiskScoringPageViewModel
                {
                    ErrorMessage = ex.Message
                });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var message = await GetErrorMessageAsync(
                ex,
                nameof(Index),
                "Unable to load risk score setup.");

            // The existing workflow partial reads this property.
            ViewBag.ErrorMessage = message;

            return View(
                "Index",
                new RiskScoringPageViewModel
                {
                    ErrorMessage = message
                });
        }
    }

    // =====================================================
    // RISK RANGES — GET
    // =====================================================

    [HttpGet]
    [Authorize(Policy = AppPermissions.RiskScoreSetup.Manage)]
    public async Task<IActionResult> Edit(
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await LoadSetupAsync(cancellationToken);

            if (!result.CanEditBands)
            {
                TempData["ErrorMessage"] =
                    "You cannot edit these ranges. If a pending set exists, " +
                    "only its maker can change it.";

                return RedirectToAction(nameof(Index));
            }

            if (result.Bands.Count == 0)
            {
                // Proposed starting values only.
                // They remain inactive until saved and approved.
                return View(
                    "Edit",
                    new RiskScoreSetupEditViewModel
                    {
                        Bands =
                        [
                            new RiskBandEditViewModel
                            {
                                RiskLevel = "LOW",
                                MinimumScore = 0,
                                MaximumScore = 29,
                                Decision = "ALLOW"
                            },
                            new RiskBandEditViewModel
                            {
                                RiskLevel = "MEDIUM",
                                MinimumScore = 30,
                                MaximumScore = 59,
                                Decision = "REVIEW"
                            },
                            new RiskBandEditViewModel
                            {
                                RiskLevel = "HIGH",
                                MinimumScore = 60,
                                MaximumScore = 79,
                                Decision = "REVIEW"
                            },
                            new RiskBandEditViewModel
                            {
                                RiskLevel = "CRITICAL",
                                MinimumScore = 80,
                                MaximumScore = 100,
                                Decision = "BLOCK"
                            }
                        ]
                    });
            }

            var model = new RiskScoreSetupEditViewModel
            {
                Bands = result.Bands
                    .OrderBy(band => band.MinimumScore)
                    .Select(band => new RiskBandEditViewModel
                    {
                        Id = band.Id,
                        RiskLevel = band.RiskLevel,
                        MinimumScore = band.MinimumScore,
                        MaximumScore = band.MaximumScore,
                        Decision = band.Decision,
                        Remarks = band.Remarks,
                        RowVersion = band.RowVersion
                    })
                    .ToList()
            };

            return View("Edit", model);
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["ErrorMessage"] = await GetErrorMessageAsync(
                ex,
                nameof(Edit),
                "Unable to open the risk range form.");

            return RedirectToAction(nameof(Index));
        }
    }

    // =====================================================
    // RISK RANGES — POST
    // =====================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.RiskScoreSetup.Manage)]
    public async Task<IActionResult> Edit(
        RiskScoreSetupEditViewModel model,
        CancellationToken cancellationToken)
    {
        model.Bands ??= [];

        ValidateRanges(model);

        if (!ModelState.IsValid)
            return View("Edit", model);

        try
        {
            var ranges = model.Bands
                .Select(band => new RiskBandInput
                {
                    Id = band.Id,
                    RiskLevel = band.RiskLevel.Trim().ToUpperInvariant(),
                    MinimumScore = band.MinimumScore,
                    MaximumScore = band.MaximumScore,
                    Decision = band.Decision.Trim().ToUpperInvariant(),
                    Remarks = Clean(band.Remarks),
                    RowVersion = band.RowVersion
                })
                .ToList();

            await _riskScoring.SaveBandRangesAsync(
                ranges,
                cancellationToken);

            TempData["SuccessMessage"] =
                "Risk ranges saved as Pending. A different authorized " +
                "checker must review and approve them.";

            return RedirectToAction(nameof(Index));
        }
        catch (RiskConfigurationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);

            return View("Edit", model);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ModelState.AddModelError(
                string.Empty,
                await GetErrorMessageAsync(
                    ex,
                    nameof(Edit),
                    "Unable to save risk ranges."));

            return View("Edit", model);
        }
    }

    // =====================================================
    // REVIEW RISK RANGES
    // =====================================================

    [HttpGet]
    public Task<IActionResult> Review(
        CancellationToken cancellationToken)
    {
        return OpenReviewAsync(
            "Bands",
            nameof(Review),
            cancellationToken);
    }

    // =====================================================
    // APPROVE RISK RANGES
    // =====================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.RiskScoreSetup.Approve)]
    public async Task<IActionResult> Apply(
        string? reviewToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reviewToken))
        {
            TempData["ErrorMessage"] =
                "Open the range review again before approving.";

            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _riskScoring.ActivateBandsAsync(
                reviewToken,
                cancellationToken);

            TempData["SuccessMessage"] =
                "Risk ranges approved. New evaluations use the approved ranges.";
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["ErrorMessage"] = await GetErrorMessageAsync(
                ex,
                nameof(Apply),
                "Unable to approve risk ranges.");
        }

        return RedirectToAction(nameof(Index));
    }

    // =====================================================
    // MODEL WEIGHTS — GET
    // =====================================================

    [HttpGet]
    [Authorize(Policy = AppPermissions.RiskScoreSetup.Manage)]
    public async Task<IActionResult> EditModel(
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await LoadSetupAsync(cancellationToken);

            if (!result.CanEditModel)
            {
                TempData["ErrorMessage"] =
                    "You cannot edit this model. If a pending model exists, " +
                    "only its maker can change it.";

                return RedirectToAction(nameof(Index));
            }

            var source = result.PendingModel ?? result.ActiveModel;

            if (source is null)
            {
                // Proposed values, not approved production settings.
                return View(
                    "Model",
                    new RiskModelEditViewModel
                    {
                        CustomerWeightPercent = 40m,
                        TransactionWeightPercent = 40m,
                        ScreeningWeightPercent = 20m
                    });
            }

            return View(
                "Model",
                new RiskModelEditViewModel
                {
                    Id = source.Id,
                    CustomerWeightPercent = source.CustomerWeightPercent,
                    TransactionWeightPercent = source.TransactionWeightPercent,
                    ScreeningWeightPercent = source.ScreeningWeightPercent,
                    RowVersion = source.RowVersion
                });
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["ErrorMessage"] = await GetErrorMessageAsync(
                ex,
                nameof(EditModel),
                "Unable to open the risk model form.");

            return RedirectToAction(nameof(Index));
        }
    }

    // =====================================================
    // MODEL WEIGHTS — POST
    // =====================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.RiskScoreSetup.Manage)]
    public async Task<IActionResult> EditModel(
        RiskModelEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.Id < 0)
        {
            ModelState.AddModelError(
                nameof(model.Id),
                "Invalid risk model.");
        }

        if (model.Id > 0 && model.RowVersion is not { Length: 8 })
        {
            ModelState.AddModelError(
                string.Empty,
                "Reload the model form before saving.");
        }

        // Property annotations validate each weight's 0–100 range.
        // Avoid arithmetic on malformed/out-of-range posted values.
        if (ModelState.IsValid)
        {
            var total =
                model.CustomerWeightPercent +
                model.TransactionWeightPercent +
                model.ScreeningWeightPercent;

            if (total != 100m)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Customer, Transaction and Screening weights " +
                    "must total exactly 100%.");
            }
        }

        if (!ModelState.IsValid)
            return View("Model", model);

        try
        {
            await _riskScoring.SaveModelAsync(
                new RiskModelInput
                {
                    Id = model.Id,
                    CustomerWeightPercent = model.CustomerWeightPercent,
                    TransactionWeightPercent = model.TransactionWeightPercent,
                    ScreeningWeightPercent = model.ScreeningWeightPercent,
                    RowVersion = model.RowVersion
                },
                cancellationToken);

            TempData["SuccessMessage"] =
                "Risk model saved as Pending. A different authorized " +
                "checker must review and approve it.";

            return RedirectToAction(nameof(Index));
        }
        catch (RiskConfigurationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);

            return View("Model", model);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ModelState.AddModelError(
                string.Empty,
                await GetErrorMessageAsync(
                    ex,
                    nameof(EditModel),
                    "Unable to save the risk model."));

            return View("Model", model);
        }
    }

    // =====================================================
    // REVIEW MODEL
    // =====================================================

    [HttpGet]
    public Task<IActionResult> ReviewModel(
        CancellationToken cancellationToken)
    {
        return OpenReviewAsync(
            "Model",
            nameof(ReviewModel),
            cancellationToken);
    }

    // =====================================================
    // APPROVE MODEL
    // =====================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.RiskScoreSetup.Approve)]
    public async Task<IActionResult> ApplyModel(
        string? reviewToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reviewToken))
        {
            TempData["ErrorMessage"] =
                "Open the model review again before approving.";

            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _riskScoring.ActivateModelAsync(
                reviewToken,
                cancellationToken);

            TempData["SuccessMessage"] =
                "Risk model approved. New evaluations use the approved model.";
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["ErrorMessage"] = await GetErrorMessageAsync(
                ex,
                nameof(ApplyModel),
                "Unable to approve the risk model.");
        }

        return RedirectToAction(nameof(Index));
    }

    // =====================================================
    // SHARED HELPERS
    // =====================================================

    private Task<RiskScoringSetupResult> LoadSetupAsync(
        CancellationToken cancellationToken)
    {
        return _riskScoring.GetSetupAsync(
            null,
            null,
            1,
            10,
            cancellationToken);
    }

    private async Task<IActionResult> OpenReviewAsync(
        string kind,
        string action,
        CancellationToken cancellationToken)
    {
        try
        {
            var review = await _riskScoring.GetApprovalReviewAsync(
                kind,
                null,
                cancellationToken);

            return View(ReviewView, review);
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["ErrorMessage"] = await GetErrorMessageAsync(
                ex,
                action,
                "Unable to open the approval review.");
        }

        return RedirectToAction(nameof(Index));
    }

    private void ValidateRanges(RiskScoreSetupEditViewModel model)
    {
        if (model.Bands.Count == 0)
        {
            ModelState.AddModelError(
                nameof(model.Bands),
                "At least one risk range is required.");

            return;
        }

        for (var index = 0; index < model.Bands.Count; index++)
        {
            var band = model.Bands[index];

            if (band.Id < 0)
            {
                ModelState.AddModelError(
                    $"Bands[{index}].Id",
                    "Invalid risk range.");
            }

            if (band.Id > 0 && band.RowVersion is not { Length: 8 })
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Reload the range form before saving.");

                break;
            }
        }

        if (!ModelState.IsValid)
            return;

        var levels = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var nextMinimum = 0;

        foreach (var band in model.Bands.OrderBy(x => x.MinimumScore))
        {
            var level = band.RiskLevel.Trim();
            var decision = band.Decision.Trim().ToUpperInvariant();
            var maximum = band.MaximumScore ?? 100;

            if (!levels.Add(level))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Each risk level must have a unique name.");
            }

            if (decision is not ("ALLOW" or "REVIEW" or "BLOCK"))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Risk decisions must be ALLOW, REVIEW or BLOCK.");
            }

            if (band.MinimumScore != nextMinimum ||
                maximum < band.MinimumScore ||
                maximum > 100)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Risk ranges must cover 0–100 without gaps or overlaps.");

                return;
            }

            nextMinimum = maximum + 1;
        }

        if (nextMinimum != 101)
        {
            ModelState.AddModelError(
                string.Empty,
                "The final risk range must end at 100.");
        }
    }

    private async Task<string> GetErrorMessageAsync(
        Exception exception,
        string action,
        string message)
    {
        var errorId = await _errorLogService.LogAsync(
            exception,
            nameof(RiskScoreSetupController),
            action,
            User.FindFirstValue(AppClaimTypes.PartnerCode));

        return errorId.HasValue
            ? $"{message} Error reference: {errorId.Value}."
            : message;
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}