using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface;
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
            RoleName = User.FindFirst(ClaimTypes.Role)?.Value ?? "User"
        };

        try
        {
            model.Summary = await _amlRecordRepository.GetSummaryAsync();
            var recentPage = await _amlRecordRepository.GetPageAsync(
                search: null,
                recordType: null,
                isActive: null,
                pageNumber: 1,
                pageSize: 5);
            model.RecentRecords = recentPage.Items;
            var transactions = await _workflowService.GetTransactionsAsync();
            var alerts = await _workflowService.GetAlertsAsync();
            var cases = await _workflowService.GetCasesAsync();
            model.TransactionCount = transactions.Count;
            model.ReviewCount = transactions.Count(x => x.Decision == "REVIEW");
            model.BlockCount = transactions.Count(x => x.Decision == "BLOCK");
            model.OpenAlertCount = alerts.Count(x => x.Status is not ("RESOLVED" or "CLOSED"));
            model.OpenCaseCount = cases.Count(x => x.Status != "CLOSED");
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
