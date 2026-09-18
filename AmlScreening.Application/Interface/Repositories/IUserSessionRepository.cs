using AmlScreening.Application.Models;

namespace AmlScreening.Application.Interface.Repositories;

public  interface IUserSessionRepository
{
    Task<int> StartAsync(
        Guid sessionId,
        long userId,
        int partnerId,
        int branchId,
        string ipAddress,
        string? userAgent);

    Task<bool> ValidateAsync(
        Guid sessionId,
        long userId,
        int partnerId,
        int branchId,
        string ipAddress);

    Task EndAsync(
        Guid sessionId,
        long userId);
}

