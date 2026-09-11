using Loupe.Application.Security;
using Loupe.Application.Sessions;
using Loupe.Infrastructure.Security;
using Loupe.Api.Authentication;
using Loupe.Api.Errors;
using Loupe.Infrastructure.Persistence;
using Loupe.Api.Uploads;
using System.Text.Json.Serialization;

namespace Loupe.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
        builder.Services.AddLoupePersistence();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddProblemDetails();
        builder.Services.AddMediatR(options => options.RegisterServicesFromAssemblyContaining<GetSessionQuery>());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentOwner, CurrentOwner>();
        builder.Services.AddLoupeAuthentication(builder.Configuration);
        builder.Services.AddOptions<BrowserOptions>().BindConfiguration("Browser")
            .Validate(options => options.AllowedOrigins.Length > 0 && options.AllowedOrigins.All(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme == "https"
                && uri.GetLeftPart(UriPartial.Authority) == origin && string.IsNullOrEmpty(uri.UserInfo)),
                "Browser:AllowedOrigins must explicitly list HTTPS origins without paths or credentials.")
            .ValidateOnStart();
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-Token";
            options.Cookie.Name = "__Host-loupe-csrf";
            options.Cookie.Path = "/";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
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
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<UploadBodyLimitMiddleware>();
        app.UseMiddleware<SessionCsrfMiddleware>();
        app.UseMiddleware<SignInRateLimitMiddleware>();
        app.MapControllers();
        app.Run();
    }
}
