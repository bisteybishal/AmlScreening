using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace AmlScreening.Portal.Security;

public static class AmlRecordOperations
{
    public static OperationAuthorizationRequirement Read { get; } = new() { Name = nameof(Read) };
    public static OperationAuthorizationRequirement Update { get; } = new() { Name = nameof(Update) };
    public static OperationAuthorizationRequirement Delete { get; } = new() { Name = nameof(Delete) };
}
