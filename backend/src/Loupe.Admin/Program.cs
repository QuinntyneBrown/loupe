using Loupe.Application.Common;
using Loupe.Application.Users;
using Loupe.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Loupe.Admin;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || (args[0] == "create-user" ? args.Length != 3 : args[0] != "reset-password" || args.Length != 2))
        {
            Console.Error.WriteLine("Usage: Loupe.Admin create-user <email> <display-name> | reset-password <email>. Supply the password through the hidden prompt or stdin.");
            return 2;
        }
        try
        {
            var password = PasswordPrompt.Read();
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddLoupeAccounts();
            builder.Services.AddMediatR(o =>
            {
                o.TypeEvaluator = type => type.Namespace == typeof(CreateUserCommand).Namespace;
                o.RegisterServicesFromAssemblyContaining<CreateUserCommand>();
            });
            using var host = builder.Build();
            using var scope = host.Services.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            if (args[0] == "create-user")
            {
                var id = await sender.Send(new CreateUserCommand(args[1], args[2], password));
                Console.WriteLine($"Created user {id}.");
            }
            else
            {
                await sender.Send(new ResetPasswordCommand(args[1], password));
                Console.WriteLine("Password reset. All previous sessions are revoked.");
            }
            return 0;
        }
        catch (RequestValidationException exception)
        {
            Console.Error.WriteLine(string.Join(" ", exception.Errors.Values.SelectMany(messages => messages)));
            return 1;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Account operation failed. Check the database configuration, connectivity, and applied migrations.");
            return 1;
        }
    }
}
