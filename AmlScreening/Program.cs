using AmlScreening.Application;
using AmlScreening.Infrastructure;
using AmlScreening.Application.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AMLScreening.Auth";
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in AppPermissions.All)
    {
        options.AddPolicy(permission, policy =>
            policy.RequireClaim(AppPermissions.ClaimType, permission));
    }
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Too many login attempts for this username and IP address. Please wait one minute and try again.",
            cancellationToken);
    };

    options.AddPolicy("login", httpContext =>
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        var username = httpContext.Items["LoginUsername"]?.ToString() ?? "unknown-user";
        var partitionKey = $"{ipAddress}|{username}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Read the submitted username before the rate limiter creates its partition.
// The form remains available to MVC after ReadFormAsync has been called here.
app.Use(async (context, next) =>
{
    var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
    var isLoginPost = HttpMethods.IsPost(context.Request.Method)
        && action?.ControllerName.Equals("Account", StringComparison.OrdinalIgnoreCase) == true
        && action.ActionName.Equals("Login", StringComparison.OrdinalIgnoreCase)
        && context.Request.HasFormContentType;

    if (isLoginPost)
    {
        var form = await context.Request.ReadFormAsync();
        context.Items["LoginUsername"] = form["UserName"].ToString().Trim().ToUpperInvariant();
    }

    await next();
});

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();
