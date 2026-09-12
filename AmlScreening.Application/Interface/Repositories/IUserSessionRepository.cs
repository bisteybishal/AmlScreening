using AmlScreening.Application.Models;

namespace AmlScreening.Application.Interface.Repositories;

public  interface IUserSessionRepository
{
    Task<UserSessionResult> StartAsync(long UserId, int clientId, Guid sessionId);
    Task<UserSessionResult> ValidateAsync(long UserId, int clientId, Guid sessionId);
    Task<UserSessionResult> EndAsync(long UserId, int clientId, Guid sessionId);
}

