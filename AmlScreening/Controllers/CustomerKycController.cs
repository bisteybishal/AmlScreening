using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Security;
using AmlScreening.Portal.Services;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace AmlScreening.Portal.Controllers
{
    [Authorize(Policy = AppPermissions.CustomerKyc.View)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class CustomerKycController : Controller
    {
        private readonly CustomerKycService _service;
        private readonly IErrorLogService _errorLogService;

        public CustomerKycController(CustomerKycService service, IErrorLogService errorLogService)
        {
            _service = service;
            _errorLogService = errorLogService;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            {
                var model = new CustomerKycViewModel();

                try
                {
                    await _service.LoadDropdownsAsync(
                        model, cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    return new EmptyResult();
                }
                catch (Exception ex)
                {
                    await _errorLogService.LogAsync(
                        ex,
                        nameof(CustomerKycController),
                        nameof(Index),
                        User.FindFirstValue(AppClaimTypes.PartnerCode));

                    ModelState.AddModelError(
                        string.Empty,
                        "KYC options could not be loaded. Please try again.");
                }

                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Screen()
        {
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Screen(CustomerKycViewModel model, CancellationToken cancellationToken)
        {

            // Load options first.
            await _service.LoadDropdownsAsync(
                model,
                cancellationToken);


            // Validate occupation only when supplied.
            if (!string.IsNullOrWhiteSpace(
                    model.Occupation)
                &&
                !model.OccupationOptions.Any(
                    x => string.Equals(
                        x.Value,
                        model.Occupation,
                        StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(
                    nameof(model.Occupation),
                    "Select a valid occupation.");
            }


            // Validate source only when supplied.
            if (!string.IsNullOrWhiteSpace(
                    model.SourceOfFunds)
                &&
                !model.SourceOfFundsOptions.Any(
                    x => string.Equals(
                        x.Value,
                        model.SourceOfFunds,
                        StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(
                    nameof(model.SourceOfFunds),
                    "Select a valid source of funds.");
            }

            if (!ModelState.IsValid)
            {
                return View(
                    "Index",
                    model);
            }
            try
            {
               
                model.Result =
                    await _service.CheckAsync(
                        model,
                        cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] =
                    ex.Message;
            }


            catch (Exception ex)
            {
                model.Result =
                    null;


                await _errorLogService.LogAsync(
                    ex.GetBaseException(),
                    nameof(CustomerKycController),
                    nameof(Screen),
                    User.FindFirstValue(
                        AppClaimTypes.PartnerCode));


                ModelState.AddModelError(
                    string.Empty,
                    "KYC checking could not be completed.");
            }


            return View(
                "Index",
                model);
        }
    }
}