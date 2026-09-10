using AmlScreening.Application.Models;
using AmlScreening.Domain.Entities.Compliance;

namespace AmlScreening.Application.Interface;

public interface IAmlWorkflowService
{
    Task<ComplianceDashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken=default);

    Task<IReadOnlyList<Customer>> GetCustomersAsync();
    Task<Customer?> GetCustomerAsync(int id);
    Task<int> CreateCustomerAsync(Customer customer);
    Task<bool> UpdateCustomerAsync(Customer customer);
    Task<bool> DeleteCustomerAsync(int id);

    Task<IReadOnlyList<AmlTransaction>> GetTransactionsAsync();
    Task<AmlTransactionDetails?> GetTransactionDetailsAsync(int id);
    Task<int> CreateTransactionAsync(AmlTransaction transaction);
    Task<AmlEvaluationResult?> EvaluateAsync(int transactionId);

    Task<IReadOnlyList<ComplianceAlert>> GetAlertsAsync();
    Task<ComplianceAlert?> GetAlertAsync(int id);
    Task<bool> UpdateAlertAsync(int id, string status, string? resolution);

    Task<IReadOnlyList<ComplianceCase>> GetCasesAsync();
    Task<ComplianceCase?> GetCaseAsync(int id);
    Task<bool> UpdateCaseAsync(int id, string status, string? disposition, string? notes);
}
