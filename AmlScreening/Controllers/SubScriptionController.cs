using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmlScreening.Portal.Controllers
{

    [Authorize(Policy = AppPermissions.SubscriptionCreate)]
    public sealed class SubscriptionController : Controller
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        public SubscriptionController(ISubscriptionRepository subscriptionRepository)
        {
            _subscriptionRepository = subscriptionRepository;
        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var subscriptionPlans = await _subscriptionRepository.GetAllAsync();
            return View(subscriptionPlans);
        }
        [HttpGet]
        public IActionResult Add()
        {
            return View(new SubscriptionAddModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(SubscriptionAddModel model)
        {
            if (!ModelState.IsValid)
                return View(model);
            var plan = new SubscriptionPlan
            {
                SourceCode = model.SourceCode,
                SourceName = model.SourceName,
                Rate = model.Rate,
                DurationMonths = model.DurationMonths,
                IsActive = model.IsActive
            };
            await _subscriptionRepository.CreateSubscriptionAsync(plan);
            TempData["SuccessMessage"] = "Subscription plan added Successfully.";
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _subscriptionRepository.DeleteAsync(id);
            if (result)
            {
                TempData["ErrorMessage"] = "Subscription plan not found.";
            }
            else
            {
                TempData["SuccessMessage"] = "Subscription plan deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var res = await _subscriptionRepository.GetByIdAsync(id);
            if (res == null)
                return NotFound();
            return View(new SubscriptionAddModel
            {
                SourceCode=res.SourceCode,
                SourceName=res.SourceName,
                Rate=res.Rate,
                DurationMonths=res.DurationMonths,
                IsActive=res.IsActive   

            });

        }
    }
}
