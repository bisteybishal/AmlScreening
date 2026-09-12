namespace AmlScreening.Domain.Common;

public interface IClientOwnedEntity
{
    int ClientId { get; set; }
}

public interface IUserOwnedEntity : IClientOwnedEntity
{
    long? OwnerUserId { get; set; }
}
