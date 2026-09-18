using AmlScreening.Application.Models;

namespace AmlScreening.Application.Interface.Repositories;

public interface IErrorLogRepository
{
    Task<long> LogAsync(
         ErrorLogRequest request);
}
