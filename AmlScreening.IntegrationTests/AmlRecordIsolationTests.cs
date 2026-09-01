using AmlScreening.Application.Interface;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Data;
using AmlScreening.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AmlScreening.IntegrationTests;

public sealed class AmlRecordIsolationTests
{
    [Fact]
    public async Task Regular_user_sees_only_records_owned_by_same_user()
    {
        var currentUser = new FakeCurrentUserContext(10, 1);
        await using var dbContext = CreateContext(currentUser);
        await SeedAsync(dbContext);
        var repository = new AmlRecordRepository(dbContext, currentUser);

        var result = await repository.GetPageAsync(null, null, null, 1, 20);

        var record = Assert.Single(result.Items);
        Assert.Equal(101, record.Id);
    }

    [Fact]
    public async Task Client_view_permission_never_returns_another_clients_records()
    {
        var currentUser = new FakeCurrentUserContext(
            10,
            1,
            AppPermissions.AmlRecordsViewClient);
        await using var dbContext = CreateContext(currentUser);
        await SeedAsync(dbContext);
        var repository = new AmlRecordRepository(dbContext, currentUser);

        var result = await repository.GetPageAsync(null, null, null, 1, 20);

        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, record => Assert.Equal(1, record.ClientId));
        Assert.DoesNotContain(result.Items, record => record.Id == 201);
    }

    [Fact]
    public async Task Cross_client_record_cannot_be_loaded_by_id()
    {
        var currentUser = new FakeCurrentUserContext(
            10,
            1,
            AppPermissions.AmlRecordsViewClient);
        await using var dbContext = CreateContext(currentUser);
        await SeedAsync(dbContext);
        var repository = new AmlRecordRepository(dbContext, currentUser);

        var record = await repository.GetByIdAsync(201);

        Assert.Null(record);
    }

    [Fact]
    public async Task Platform_view_all_permission_can_cross_client_boundary()
    {
        var currentUser = new FakeCurrentUserContext(
            1,
            1,
            AppPermissions.AmlRecordsViewAll);
        await using var dbContext = CreateContext(currentUser);
        await SeedAsync(dbContext);
        var repository = new AmlRecordRepository(dbContext, currentUser);

        var result = await repository.GetPageAsync(null, null, null, 1, 20);

        Assert.Equal(3, result.Items.Count);
    }

    private static AmlScreeningDbContext CreateContext(ICurrentUserContext currentUser)
    {
        var options = new DbContextOptionsBuilder<AmlScreeningDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AmlScreeningDbContext(options, currentUser);
    }

    private static async Task SeedAsync(AmlScreeningDbContext dbContext)
    {
        dbContext.AmlRecords.AddRange(
            CreateRecord(101, 1, 10, "C1-OWN"),
            CreateRecord(102, 1, 11, "C1-OTHER"),
            CreateRecord(201, 2, 20, "C2-OTHER"));
        await dbContext.SaveChangesAsync();
    }

    private static AmlRecord CreateRecord(
        int id,
        int clientId,
        long ownerUserId,
        string referenceNo) => new()
    {
        Id = id,
        ClientId = clientId,
        OwnerUserId = ownerUserId,
        RecordType = "Individual",
        FullName = referenceNo,
        Country = "Nepal",
        Nationality = "Nepali",
        ReferenceNo = referenceNo,
        SourceName = "Test",
        DeleteFg = "N",
        CreatedDate = DateTime.UtcNow
    };

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        private readonly HashSet<string> _permissions;

        public FakeCurrentUserContext(
            long userId,
            int clientId,
            params string[] permissions)
        {
            UserId = userId;
            ClientId = clientId;
            _permissions = permissions.ToHashSet(StringComparer.Ordinal);
        }

        public bool IsAuthenticated => true;
        public long UserId { get; }
        public int ClientId { get; }
        public string Username => $"user-{UserId}";
        public bool HasPermission(string permissionCode) =>
            _permissions.Contains(permissionCode);
    }
}
