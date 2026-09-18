namespace AmlScreening.Application.Interface.Services
{
    public interface IErrorLogService
    {
        Task<long?> LogAsync(
            Exception exception,
            string controller,
            string action,
            string? partnerCode = null);
    }
}
