using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace AmlScreening.Portal.Services
{
    public sealed class ErrorLogService
     : IErrorLogService
    {
        private readonly IErrorLogRepository
            _repository;

        private readonly IHttpContextAccessor
            _httpContextAccessor;

        public ErrorLogService(
            IErrorLogRepository repository,
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _httpContextAccessor =
                httpContextAccessor;
        }

        public async Task<long?> LogAsync(
            Exception exception,
            string controller,
            string action,
            string? partnerCode = null)
        {
            try
            {
                var context =
                    _httpContextAccessor.HttpContext;

                long? userId = null;

                var userIdValue =
                    context?.User.FindFirstValue(
                        ClaimTypes.NameIdentifier);

                if (long.TryParse(
                        userIdValue,
                        out var parsedUserId))
                {
                    userId = parsedUserId;
                }

                var username =
                    context?.User.Identity?.Name;

                var ipAddress =
                    context?
                        .Connection
                        .RemoteIpAddress?
                        .ToString();

                var requestPath =
       context == null
           ? null
           : $"{context.Request.Path}" +
             $"{context.Request.QueryString}";

                string errorNumber;

                if (exception is SqlException sqlException)
                {
                    errorNumber =
                        sqlException.Number.ToString();
                }
                else
                {
                    errorNumber =
                        exception.HResult.ToString();
                }

                var errorDescription =
                    exception.Message;

                if (errorDescription.Length > 2000)
                {
                    errorDescription =
                        errorDescription[..2000];
                }

                var errorAction =
                    action;

                if (!string.IsNullOrWhiteSpace(
                        exception.StackTrace))
                {
                    var stack =
                        exception.StackTrace;

                    if (stack.Length > 350)
                        stack = stack[..350];

                    errorAction =
                        $"{action} | {stack}";
                }

                if (errorAction.Length > 500)
                {
                    errorAction =
                        errorAction[..500];
                }

                var request =
                    new ErrorLogRequest
                    {
                        ErrorDesc =
                            errorDescription,

                        PartnerCode =
                            partnerCode,

                        ErrorNumber =
                            errorNumber,

                        Script =
                            controller,

                        ErrorScript =
                            errorAction,

                        QueryString =
                            requestPath,

                        IpAddress =
                            ipAddress,

                        UserId =
                            userId,

                        Username =
                            username
                    };

                return await _repository
                    .LogAsync(request);
            }
            catch
            {

                return null;
            }
        }
    }
}
