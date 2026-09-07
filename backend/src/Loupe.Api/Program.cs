using Loupe.Application.Security;
using Loupe.Application.Sessions;
using Loupe.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Loupe.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers();
        builder.Services.AddMediatR(options => options.RegisterServicesFromAssemblyContaining<GetSessionQuery>());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentOwner, CurrentOwner>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.Name = "__Host-loupe-session";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
        });
        builder.Services.AddAuthorization();
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store, private";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            await next(context);
        });
        app.UseStatusCodePages(async context =>
        {
            var status = context.HttpContext.Response.StatusCode;
            var code = status == 401 ? "authentication_required" : "request_failed";
            await Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.HttpContext.TraceIdentifier
            }).ExecuteAsync(context.HttpContext);
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.Run();
    }
}
