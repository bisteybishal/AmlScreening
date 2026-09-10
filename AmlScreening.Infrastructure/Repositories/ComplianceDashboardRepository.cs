using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using System.Data;

namespace AmlScreening.Infrastructure.Repositories;

public class ComplianceDashboardRepository : IComplianceDashboardRepository
{
    private readonly IDBConnectionFactory _dBConnection;

    public ComplianceDashboardRepository(IDBConnectionFactory dBConnection)
    {
        _dBConnection = dBConnection;
    }

    public async Task<ComplianceDashboardSummary> GetSummaryAsync(int clientid, bool canViewTransactions, bool canViewAlerts, bool canViewCases, CancellationToken cancellationToken = default)
    {
        await using var connection = _dBConnection.CreateConnection();
        await using var command = new SqlCommand("dbo.usp_ComplianceDashboard_GetSummary", connection);
        command.CommandType = CommandType.StoredProcedure;
        command.Parameters.Add("@clientid", SqlDbType.Int).Value = clientid;
        command.Parameters.Add("@CanViewTransactions", SqlDbType.Bit).Value = canViewTransactions;
        command.Parameters.Add("@CanViewAlerts", SqlDbType.Bit).Value = canViewAlerts;
        command.Parameters.Add("@CanViewCases", SqlDbType.Bit).Value = canViewCases;

        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new ComplianceDashboardSummary();
        }
        return new ComplianceDashboardSummary
        {
            TransactionCount = reader.GetInt32(reader.GetOrdinal("TransactionCount")),
            ReviewCount = reader.GetInt32(reader.GetOrdinal("ReviewCount")),
            BlockCount = reader.GetInt32(reader.GetOrdinal("BlockCount")),
            OpenAlertCount = reader.GetInt32(reader.GetOrdinal("OpenAlertCount")),
            OpenCaseCount = reader.GetInt32(reader.GetOrdinal("OpenCaseCount"))
        };
    }
}