using CustomerKyc.Application;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CustomerKyc.Infrastructure;

public sealed class PartnerRegistry(IConfiguration configuration)
{
    public async Task<Partner?> FindAsync(string oauthClientId, CancellationToken ct)
    {
        using var db = new SqlConnection(configuration.GetConnectionString("AMLDatabase"));
        return await db.QuerySingleOrDefaultAsync<Partner>(new CommandDefinition(
            "kyc.GetPartner", new { OAuthClientId = oauthClientId },
            commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));
    }
}
