using AmlScreening.Portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers;

[Authorize(
    Policy = AppPermissions.CustomerKyc.Review)]
public class CustomerKycReviewController : Controller
{
    private readonly CustomerKycReviewService
        _service;


    public CustomerKycReviewController(
        CustomerKycReviewService service)
    {
        _service =
            service;
    }


    [HttpGet]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var model =
            await _service.GetPendingAsync(
                cancellationToken);

        return View(model);
    }


    [HttpGet]
    public async Task<IActionResult> Details(
        long id,
        CancellationToken cancellationToken)
    {
        var model =
            await _service.GetAsync(
                id,
                cancellationToken);


        if (model == null)
        {
            return NotFound();
        }


        return View(model);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = AppPermissions.CustomerKyc.Approve)]
    public async Task<IActionResult> Approve(
        long id,
        byte[] rowVersion,
        string? remarks,
        CancellationToken cancellationToken)
    {
        try
        {
            await _service.ApproveAsync(
                id,
                rowVersion,
                remarks,
                cancellationToken);


            TempData["SuccessMessage"] =
                "KYC screening approved.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] =
                ex.Message;
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] =
                ex.Message;
        }


        return RedirectToAction(
            nameof(Index));
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.CustomerKyc.Approve)]
    public async Task<IActionResult> Reject(
        long id,
        byte[] rowVersion,
        string remarks,
        CancellationToken cancellationToken)
    {
        try
        {
            await _service.RejectAsync(
                id,
                rowVersion,
                remarks,
                cancellationToken);


            TempData["SuccessMessage"] =
                "KYC screening rejected.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] =
                ex.Message;
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] =
                ex.Message;
        }


        return RedirectToAction(
            nameof(Index));
    }
}