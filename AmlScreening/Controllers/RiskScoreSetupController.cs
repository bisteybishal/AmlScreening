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
    private static readonly string[] Levels =
  {
        "LOW",
        "MEDIUM",
        "HIGH",
        "CRITICAL"
    };

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
            var setup = await LoadSetupAsync(cancellationToken);

            if (!setup.CanEditBands)
            {
                TempData["ErrorMessage"] =
                    "You cannot edit these ranges. " +
                    "Only the maker can edit an existing pending change.";

                return RedirectToAction(nameof(Index));
            }

            var model = new RiskScoreSetupEditViewModel();

            if (setup.Bands.Count == 0)
            {
                // Users enter their own boundaries.
                model.Bands = Levels.Select(level =>
                    new RiskBandEditViewModel
                    {
                        RiskLevel = level,
                        MaximumScore = null,
                        Decision = level switch
                        {
                            "LOW" => "ALLOW",
                            "CRITICAL" => "BLOCK",
                            _ => "REVIEW"
                        }
                    }).ToList();
            }
            else
            {
                model.Bands = setup.Bands
                    .OrderBy(band => band.MinimumScore)
                    .Select(band => new RiskBandEditViewModel
                    {
                        Id = band.Id,
                        RiskLevel = band.RiskLevel,
                        MinimumScore = band.MinimumScore,
                        MaximumScore = band.MaximumScore ,
                        Decision = band.Decision,
                        Remarks = band.Remarks,
                        RowVersion = band.RowVersion
                    })
                    .ToList();
            }

            return View("Edit", model);
        }
        catch (RiskConfigurationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["ErrorMessage"] = await GetErrorMessageAsync(
                ex,
                nameof(Edit),
                "Unable to open risk ranges.");
        }

        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.RiskScoreSetup.Manage)]
    public async Task<IActionResult> Edit(
    RiskScoreSetupEditViewModel model,
    CancellationToken cancellationToken)
    {
        ValidateRanges(model);

        if (!ModelState.IsValid)
            return View("Edit", model);

        try
        {
            var ranges = model.Bands.Select(band => new RiskBandInput
            {
                Id = band.Id,
                RiskLevel = band.RiskLevel,
                MinimumScore = band.MinimumScore,
                MaximumScore = band.MaximumScore,
                Decision = band.Decision,
                Remarks = Clean(band.Remarks),
                RowVersion = band.RowVersion
            }).ToList();

            await _riskScoring.SaveBandRangesAsync(
                ranges,
                cancellationToken);

            TempData["SuccessMessage"] =
                "Ranges saved as Pending. " +
                "A different authorised user must approve them.";

            return RedirectToAction(nameof(Index));
        }
        catch (RiskConfigurationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var message = await GetErrorMessageAsync(
                ex,
                nameof(Edit),
                "Unable to save risk ranges.");

            ModelState.AddModelError(string.Empty, message);
        }

        return View("Edit", model);
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
        if (!ModelState.IsValid)
        {
            return;
        }

        if (model.Bands == null ||
            model.Bands.Count != 4)
        {
            ModelState.AddModelError(
                string.Empty,
                "LOW, MEDIUM, HIGH and CRITICAL ranges are required.");

            return;
        }

        var orderedBands =
            model.Bands
                .OrderBy(x => x.MinimumScore)
                .ToList();

        var expectedMinimum = 0;


        foreach (var band in orderedBands)
        {
            if (!band.MaximumScore.HasValue)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Enter the maximum score for every range.");

                return;
            }


            // First range starts 0.
            // Next range must start previous max + 1.
            if (band.MinimumScore != expectedMinimum)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Risk ranges must be continuous without gaps or overlaps.");

                return;
            }


            if (band.MaximumScore.Value <
                band.MinimumScore)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Maximum score cannot be less than minimum score.");

                return;
            }


            expectedMinimum =
                band.MaximumScore.Value + 1;
        }


        if (orderedBands.First().MinimumScore != 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "The first risk range must start at 0.");
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