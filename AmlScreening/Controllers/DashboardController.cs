using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Security;
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

    public DashboardController(
        IAmlRecordRepository amlRecordRepository,
        IAmlWorkflowService workflowService,
        ILogger<DashboardController> logger)
    {
        _amlRecordRepository = amlRecordRepository;
        _workflowService = workflowService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new DashboardViewModel
        {
            FullName = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "User",
            Username = User.FindFirst("Username")?.Value ?? User.Identity?.Name ?? "User",
            RoleName=string.Join(",",User.FindAll(ClaimTypes.Role).Select(x=>x.Value).Distinct(StringComparer.OrdinalIgnoreCase)),
            CanViewAmlRecords = User.HasClaim(AppPermissions.ClaimType, AppPermissions.AmlRecordsView),
            CanViewTransactions = User.HasClaim(AppPermissions.ClaimType, AppPermissions.TransactionsView),
            CanViewAlerts = User.HasClaim(AppPermissions.ClaimType, AppPermissions.AlertsView),
            CanViewCases = User.HasClaim(AppPermissions.ClaimType, AppPermissions.CasesView)
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
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to load dashboard data for user {Username}.",
                model.Username);
            model.ErrorMessage = "Dashboard data could not be loaded. Please refresh the page or try again later.";
        }

        return View(model);
    }
}
