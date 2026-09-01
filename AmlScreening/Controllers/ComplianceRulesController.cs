using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities.Compliance;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Models;

namespace AmlScreening.Portal.Controllers
{
    [Authorize(Policy = AppPermissions.ComplianceRulesView)]
    public class ComplianceRulesController : Controller
    {
        private readonly IComplianceRuleRepository _complianceRuleRepository;

        public ComplianceRulesController(
            IComplianceRuleRepository complianceRuleRepository)
        {
            _complianceRuleRepository = complianceRuleRepository;
        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var rules = await _complianceRuleRepository.GetAllAsync();
            var model = rules.Select(rule => new ComplianceRuleViewModel
            {
                Id = rule.Id,
                RuleCode = rule.RuleCode,
                RuleName = rule.RuleName,
                RuleType = rule.RuleType,
                ThresholdAmount = rule.ThresholdAmount,
                Currency = rule.Currency,
                LookbackMinutes = rule.LookbackMinutes,
                TransactionCount = rule.TransactionCount,
                RiskScore = rule.RiskScore,
                Action = rule.Action,
                RuleVersion = rule.RuleVersion,
                Priority = rule.Priority,
                ApprovalStatus = rule.ApprovalStatus,
                EffectiveFrom = rule.EffectiveFrom,
                EffectiveTo = rule.EffectiveTo,
                IsActive = rule.IsActive
            }).ToList();

            return View(model);
        }

        [HttpGet]
        [Authorize(Policy = AppPermissions.ComplianceRulesCreate)]
        public IActionResult Add()
        {
            return View(new ComplianceRuleViewModel
            {
                RuleType = "AmountThreshold",
                Currency = "NPR",
                RiskScore = 30,
                Action = "Review",
                RuleVersion = 1,
                Priority = 100,
                ApprovalStatus = "Active",
                EffectiveFrom = DateTime.UtcNow,
                IsActive = true
            });
        }

        [HttpPost]
        [Authorize(Policy = AppPermissions.ComplianceRulesCreate)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(ComplianceRuleViewModel complianceModel)
        {
            ValidateRule(complianceModel);
            if (!ModelState.IsValid)
                return View(complianceModel);

            var username = User.FindFirstValue(ClaimTypes.Name)
                ?? User.Identity?.Name
                ?? "Unknown";

            var rule = new ComplianceRule
            {
                RuleCode = complianceModel.RuleCode.Trim(),
                RuleName = complianceModel.RuleName.Trim(),
                RuleType = complianceModel.RuleType.Trim(),
                ThresholdAmount = complianceModel.ThresholdAmount,
                Currency = complianceModel.Currency?.Trim().ToUpperInvariant(),
                LookbackMinutes = complianceModel.LookbackMinutes,
                TransactionCount = complianceModel.TransactionCount,
                RiskScore = complianceModel.RiskScore,
                Action = complianceModel.Action.Trim(),
                RuleVersion = complianceModel.RuleVersion,
                Priority = complianceModel.Priority,
                ApprovalStatus = complianceModel.ApprovalStatus.Trim(),
                EffectiveFrom = complianceModel.EffectiveFrom,
                EffectiveTo = complianceModel.EffectiveTo,
                IsActive = complianceModel.IsActive,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = username
            };

            await _complianceRuleRepository.CreateAsync(rule);
            TempData["SuccessMessage"] = "Compliance rule added successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Policy = AppPermissions.ComplianceRulesEdit)]
        public async Task<IActionResult> Edit(int id)
        {
            var rule = await _complianceRuleRepository.GetByIdAsync(id);
            if (rule is null)
                return NotFound();

            return View(new ComplianceRuleViewModel
            {
                Id = rule.Id,
                RowVersion = rule.RowVersion,
                RuleCode = rule.RuleCode,
                RuleName = rule.RuleName,
                RuleType = rule.RuleType,
                ThresholdAmount = rule.ThresholdAmount,
                Currency = rule.Currency,
                LookbackMinutes = rule.LookbackMinutes,
                TransactionCount = rule.TransactionCount,
                RiskScore = rule.RiskScore,
                Action = rule.Action,
                RuleVersion = rule.RuleVersion,
                Priority = rule.Priority,
                ApprovalStatus = rule.ApprovalStatus,
                EffectiveFrom = rule.EffectiveFrom,
                EffectiveTo = rule.EffectiveTo,
                IsActive = rule.IsActive
            });
        }

        [HttpPost]
        [Authorize(Policy = AppPermissions.ComplianceRulesEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ComplianceRuleViewModel complianceModel)
        {
            ValidateRule(complianceModel);
            if (!ModelState.IsValid)
                return View(complianceModel);

            var rule = await _complianceRuleRepository.GetByIdAsync(complianceModel.Id);
            if (rule is null)
                return NotFound();

            rule.RuleCode = (complianceModel.RuleCode ?? string.Empty).Trim();
            rule.RuleName = (complianceModel.RuleName ?? string.Empty).Trim();
            rule.RuleType = complianceModel.RuleType.Trim();
            rule.ThresholdAmount = complianceModel.ThresholdAmount;
            rule.Currency = complianceModel.Currency?.Trim().ToUpperInvariant();
            rule.LookbackMinutes = complianceModel.LookbackMinutes;
            rule.TransactionCount = complianceModel.TransactionCount;
            rule.RiskScore = complianceModel.RiskScore;
            rule.Action = complianceModel.Action.Trim();
            rule.RuleVersion = complianceModel.RuleVersion;
            rule.Priority = complianceModel.Priority;
            rule.ApprovalStatus = complianceModel.ApprovalStatus.Trim();
            rule.EffectiveFrom = complianceModel.EffectiveFrom;
            rule.EffectiveTo = complianceModel.EffectiveTo;
            rule.IsActive = complianceModel.IsActive;
            rule.RowVersion = complianceModel.RowVersion;

            var username = User.FindFirstValue(ClaimTypes.Name)
                ?? User.Identity?.Name
                ?? "Unknown";

            if (!await _complianceRuleRepository.UpdateAsync(rule, username))
            {
                ModelState.AddModelError(string.Empty,
                    "This rule was changed by another user. Reload it and try again.");
                return View(complianceModel);
            }

            TempData["SuccessMessage"] = "Compliance rule updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        private void ValidateRule(ComplianceRuleViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.RuleCode))
                ModelState.AddModelError(nameof(model.RuleCode), "Rule code is required.");
            if (string.IsNullOrWhiteSpace(model.RuleName))
                ModelState.AddModelError(nameof(model.RuleName), "Rule name is required.");
            if (string.IsNullOrWhiteSpace(model.RuleType))
                ModelState.AddModelError(nameof(model.RuleType), "Rule type is required.");
            if (string.IsNullOrWhiteSpace(model.Action))
                ModelState.AddModelError(nameof(model.Action), "Action is required.");
            if (model.RiskScore is < 0 or > 100)
                ModelState.AddModelError(nameof(model.RiskScore), "Risk score must be between 0 and 100.");
            if (model.RuleVersion <= 0)
                ModelState.AddModelError(nameof(model.RuleVersion), "Rule version must be greater than zero.");
            if (model.Priority <= 0)
                ModelState.AddModelError(nameof(model.Priority), "Priority must be greater than zero.");
            if (model.ThresholdAmount < 0)
                ModelState.AddModelError(nameof(model.ThresholdAmount), "Threshold amount cannot be negative.");
            if (model.LookbackMinutes <= 0 && model.LookbackMinutes.HasValue)
                ModelState.AddModelError(nameof(model.LookbackMinutes), "Lookback minutes must be greater than zero.");
            if (model.TransactionCount <= 0 && model.TransactionCount.HasValue)
                ModelState.AddModelError(nameof(model.TransactionCount), "Transaction count must be greater than zero.");
            if (model.EffectiveFrom == default)
                ModelState.AddModelError(nameof(model.EffectiveFrom), "Effective-from date is required.");
            if (model.EffectiveTo.HasValue && model.EffectiveTo < model.EffectiveFrom)
                ModelState.AddModelError(nameof(model.EffectiveTo), "Effective-to date must be after effective-from date.");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.ComplianceRulesDelete)]
        public async Task<IActionResult> Delete(int id)
        {
            var deletedBy = User.FindFirstValue(ClaimTypes.Name)
                ?? User.Identity?.Name
                ?? "Unknown";

            var deleted = await _complianceRuleRepository.DeleteAsync(id, deletedBy);
            if (!deleted)
                return NotFound();

            TempData["SuccessMessage"] = "Compliance rule deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
