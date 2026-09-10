using System.Net;
using CustomerKyc.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Authentication-free KYC is allowed only in local Development.");
var clientId = builder.Configuration.GetValue<int>("LocalTesting:ClientId");
if (clientId <= 0 || string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("AMLDatabase")))
    throw new InvalidOperationException("Configure LocalTesting:ClientId and ConnectionStrings:AMLDatabase.");

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddDataProtection().SetApplicationName("CustomerKycLocal");
builder.Services.AddScoped<KycRepository>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("local-kyc", limiter =>
    {
        limiter.PermitLimit=60; limiter.Window=TimeSpan.FromMinutes(1); limiter.QueueLimit=0;
    });
});
var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context,next) =>
{
    var address=context.Connection.RemoteIpAddress;
    if (address?.IsIPv4MappedToIPv6 == true) address=address.MapToIPv4();
    if (address==null || !IPAddress.IsLoopback(address))
    {
        context.Response.StatusCode=403;
        return;
    }
    await next();
});
app.UseHttpsRedirection();
app.UseRateLimiter();
app.MapControllers();
app.Run();
