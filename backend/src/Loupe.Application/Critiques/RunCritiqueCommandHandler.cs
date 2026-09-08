using System.Text.Json;
using Loupe.Domain.Critiques;
using Loupe.Domain.Operations;
using Loupe.Application.Operations;
using MediatR;

namespace Loupe.Application.Critiques;

public sealed class RunCritiqueCommandHandler(ICritiqueWorkStore work, ICritiqueProvider provider, TimeProvider clock) : IRequestHandler<RunCritiqueCommand, bool>
{
    public async Task<bool> Handle(RunCritiqueCommand request, CancellationToken cancellationToken)
    {
        var operation = await work.ClaimAsync(ExecutionMode.Demo, cancellationToken);
        if (operation is null) return false;
        var input = JsonSerializer.Deserialize<CritiqueInput>(operation.InputJson!)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120), clock);
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var renewal = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ownsLease = true;
        var renewing = RenewAsync();
        CritiqueResult? result = null;
        var timedOut = false;
        ProviderFailureException? providerFailure = null;
        try
        {
            result = await provider.GenerateAsync(input, new AnalysisIdentity(operation.Mode, operation.Model, operation.PromptVersion), attempt.Token)
                .WaitAsync(attempt.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { timedOut = true; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt.IsCancellationRequested) { return true; }
        catch (ProviderFailureException failure) { providerFailure = failure; }
        finally
        {
            await renewal.CancelAsync();
            // Finish renewal before publishing through the same scoped store.
            await renewing;
        }
        if (!ownsLease) return true;
        if (providerFailure is not null) await work.RejectProviderAsync(operation, providerFailure, cancellationToken);
        else if (timedOut) await work.RejectTimeoutAsync(operation, cancellationToken);
        else if (result is not null && CritiqueResultValidator.IsValid(result, input.Exif)) await work.PublishAsync(operation, result, cancellationToken);
        else await work.RejectInvalidAsync(operation, cancellationToken);
        return true;

        async Task RenewAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20), clock);
            try
            {
                while (await timer.WaitForNextTickAsync(renewal.Token))
                {
                    if (await work.RenewAsync(operation, renewal.Token)) continue;
                    ownsLease = false;
                    await attempt.CancelAsync();
                    return;
                }
            }
            catch (OperationCanceledException) when (renewal.IsCancellationRequested) { }
            catch
            {
                ownsLease = false;
                await attempt.CancelAsync();
                throw;
            }
        }
    }
}
