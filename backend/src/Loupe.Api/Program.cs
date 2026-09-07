using Loupe.Application.Security;
using Loupe.Application.Sessions;
using Loupe.Infrastructure.Security;
using Loupe.Api.Authentication;
using Loupe.Api.Errors;
using Loupe.Infrastructure.Persistence;

namespace Loupe.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers();
        builder.Services.AddLoupePersistence();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddProblemDetails();
        builder.Services.AddMediatR(options => options.RegisterServicesFromAssemblyContaining<GetSessionQuery>());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentOwner, CurrentOwner>();
        builder.Services.AddLoupeAuthentication(builder.Configuration);
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
        app.MapControllers();
        app.Run();
    }
}
