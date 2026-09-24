using Loupe.Application.Users;
using MediatR;
using Microsoft.Extensions.Options;
namespace Loupe.Api.Accounts;

public sealed class SeedAccountService(IServiceScopeFactory scopes, IOptions<SeedOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var seed = options.Value;
        if (!seed.IsConfigured) return;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new EnsureUserCommand(seed.Email, seed.Name, seed.Password), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Seed: the seed account could not be ensured because the database is unreachable or not migrated. Apply migrations before starting the API.", exception);
        }
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
