using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface.Services;
using AmlScreening.Portal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AmlScreening.Portal.Controllers;

[Authorize]
public sealed class DashboardController : Controller
{
    private readonly IAmlRecordRepository _amlRecordRepository;
    private readonly IAmlWorkflowService _workflowService;
    private readonly ILogger<DashboardController> _logger;
    private readonly IErrorLogService _errorLogService;

    public DashboardController(IAmlRecordRepository amlRecordRepository,
      IAmlWorkflowService workflowService,
      ILogger<DashboardController> logger,
      IErrorLogService errorLogService)
    {
        _amlRecordRepository = amlRecordRepository;
        _workflowService = workflowService;
        _logger = logger;
        _errorLogService = errorLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new DashboardViewModel
        {
            FullName = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "User",
            Username = User.FindFirst("Username")?.Value ?? User.Identity?.Name ?? "User",
            RoleName = string.Join(",", User.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct(StringComparer.OrdinalIgnoreCase)),
            CanViewAmlRecords = User.HasClaim(AppPermissions.ClaimType, AppPermissions.Aml.View),
            CanViewTransactions = User.HasClaim(AppPermissions.ClaimType, AppPermissions.Transactions.View),
            CanViewAlerts = User.HasClaim(AppPermissions.ClaimType, AppPermissions.Alerts.View),
            CanViewCases = User.HasClaim(AppPermissions.ClaimType, AppPermissions.Cases.View)
        };

        try
        {
            if (model.CanViewAmlRecords)
            {
                model.Summary = await _amlRecordRepository.GetSummaryAsync();
                model.RecentRecords = await _amlRecordRepository.GetRecentAsync(5);
            }

            var complianceSummary = await _workflowService.GetDashboardSummaryAsync();
            model.TransactionCount = complianceSummary.TransactionCount;
            model.ReviewCount = complianceSummary.ReviewCount;
            model.BlockCount = complianceSummary.BlockCount;
            model.OpenAlertCount = complianceSummary.OpenAlertCount;
            model.OpenCaseCount = complianceSummary.OpenCaseCount;
            return View(model);
        }
        

        catch (Exception ex)
        {
            var errorId =
                await _errorLogService.LogAsync(
                    ex,
                    nameof(DashboardController),
                    nameof(Index),
                    null);

            ModelState.AddModelError(
                string.Empty,
                errorId.HasValue
                    ? $"Unable to load dashboard data for user : {errorId}"
                    : "Dashboard data could not be loaded. Please refresh the page or try again later.");

            return View(model);
        }
        
    }
}
