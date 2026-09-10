using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace CustomerKyc.Infrastructure;

public static class PartnerRegistration
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration config)
    {
        var oauthId = config["Registration:OAuthClientId"] ?? throw new InvalidOperationException("Set Registration:OAuthClientId.");
        var secret = config["Registration:Secret"] ?? throw new InvalidOperationException("Set Registration:Secret securely.");
        var clientId = config.GetValue<int>("Registration:ClientId");
        if (string.IsNullOrWhiteSpace(oauthId) || oauthId.Length > 100 || secret.Length < 32 || clientId <= 0)
            throw new InvalidOperationException("Use a valid client ID and a random secret of at least 32 characters.");
        var db = services.GetRequiredService<AuthDbContext>();
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var connection = db.Database.GetDbConnection();
        var sqlTransaction = transaction.GetDbTransaction();
        if (await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.Clients WHERE Id=@clientId AND IsActive=1;", new {clientId}, sqlTransaction) != 1)
            throw new InvalidOperationException("Internal client is not active.");
        if (await manager.FindByClientIdAsync(oauthId) != null)
            throw new InvalidOperationException("OAuth client already exists; this command does not overwrite secrets or mappings.");
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = oauthId, ClientSecret = secret, ClientType = ClientTypes.Confidential,
            DisplayName = oauthId,
            Permissions = { Permissions.Endpoints.Token, Permissions.GrantTypes.ClientCredentials,
                Permissions.Prefixes.Scope + "customer.screening" }
        });
        await connection.ExecuteAsync("""
            INSERT kyc.PartnerAccess(OAuthClientId,ClientId,IsActive,AllowClearDecisions)
            VALUES(@oauthId,@clientId,1,0);
            """, new {oauthId,clientId},sqlTransaction);
        await transaction.CommitAsync();
        Console.WriteLine("Partner registered. Secret is hashed by OpenIddict and is not printed. New access is REVIEW-only.");
    }
}
