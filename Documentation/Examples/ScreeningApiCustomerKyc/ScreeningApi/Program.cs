using System.Net;
using Microsoft.AspNetCore.DataProtection;
using ScreeningApi.Application.Interface.Repositories;
using ScreeningApi.Application.Interface.Services;
using ScreeningApi.Application.Services;
using ScreeningApi.Infrastructure.Persistence;
using ScreeningApi.Infrastructure.Repositories;
using ScreeningApi.Services;

var builder = WebApplication.CreateBuilder(args);

// This temporary version has no authentication. Never run it as a hosted partner API.
if (!builder.Environment.IsDevelopment())
    throw new InvalidOperationException("This API requires authentication before deployment.");

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddDataProtection().SetApplicationName("ScreeningApi.CustomerKyc");
builder.Services.AddScoped<ICurrentClientContext, LocalClientContext>();
builder.Services.AddScoped<ICustomerKycService, CustomerKycService>();
builder.Services.AddScoped<ICustomerKycRepository, CustomerKycRepository>();
builder.Services.AddScoped<IDBConnectionFactory, DBConnectionFactory>();

var app = builder.Build();

// Reject remote callers even if someone binds this local test app to a public interface.
app.Use(async (context, next) =>
{
    var address = context.Connection.RemoteIpAddress;
    if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();
    if (address == null || !IPAddress.IsLoopback(address))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next(context);
});

app.UseHttpsRedirection();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();
