using AmlScreening.Domain.Common;

namespace AmlScreening.Domain.Entities;

public sealed class Client : BaseEntity
{
    public string ClientCode { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
}
