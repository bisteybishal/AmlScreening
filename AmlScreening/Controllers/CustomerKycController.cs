using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Security;
using AmlScreening.Portal.Services;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Screen(
    [Bind(
        "FullName,DateOfBirth,Country,Gender," +
        "ScreeningType,Occupation,SourceOfFunds")]
    CustomerKycViewModel model,
    CancellationToken cancellationToken)
        {
            try
            {
                // Always reload dropdowns after POST.
                await _service.LoadDropdownsAsync(
                    model,
                    cancellationToken);


                // ==========================================
                // VALIDATE COUNTRY
                // ==========================================

                if (!model.CountryOptions.Any(
                        x =>
                            string.Equals(
                                x.Value,
                                model.Country,
                                StringComparison.OrdinalIgnoreCase)))
                {
                    ModelState.AddModelError(
                        nameof(model.Country),
                        "Select a valid country.");
                }


                // ==========================================
                // VALIDATE GENDER
                // ==========================================

                if (!model.GenderOptions.Any(
                        x =>
                            string.Equals(
                                x.Value,
                                model.Gender,
                                StringComparison.OrdinalIgnoreCase)))
                {
                    ModelState.AddModelError(
                        nameof(model.Gender),
                        "Select a valid gender.");
                }


                // ==========================================
                // VALIDATE SCREENING TYPE
                // ==========================================

                if (!model.ScreeningTypeOptions.Any(
                        x =>
                            string.Equals(
                                x.Value,
                                model.ScreeningType,
                                StringComparison.OrdinalIgnoreCase)))
                {
                    ModelState.AddModelError(
                        nameof(model.ScreeningType),
                        "Select a valid screening type.");
                }


                // ==========================================
                // VALIDATE OCCUPATION
                // ==========================================

                if (!model.OccupationOptions.Any(
                        x =>
                            string.Equals(
                                x.Value,
                                model.Occupation,
                                StringComparison.OrdinalIgnoreCase)))
                {
                    ModelState.AddModelError(
                        nameof(model.Occupation),
                        "Select a valid occupation.");
                }


                // ==========================================
                // VALIDATE SOURCE OF FUNDS
                // ==========================================

                if (!model.SourceOfFundsOptions.Any(
                        x =>
                            string.Equals(
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


                model.Result =
                    await _service.CheckAsync(
                        model,
                        cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(
                    string.Empty,
                    ex.Message);
            }
            catch (Exception ex)
            {
                model.Result =
                    null;


                await _errorLogService.LogAsync(
                    ex,
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