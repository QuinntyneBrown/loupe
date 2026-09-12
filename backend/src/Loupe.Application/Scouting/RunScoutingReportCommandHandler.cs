using System.Text.Json;
using Loupe.Domain.Scouting;
using Loupe.Domain.Operations;
using Loupe.Application.Operations;
using MediatR;

namespace Loupe.Application.Scouting;

public sealed class RunScoutingReportCommandHandler(IScoutingWorkStore work, IScoutingProvider provider, IScoutingConfiguration configuration, IAnalysisFailureStore failures, TimeProvider clock) : IRequestHandler<RunScoutingReportCommand, bool>
{
    public async Task<bool> Handle(RunScoutingReportCommand request, CancellationToken cancellationToken)
    {
        var operation = await work.ClaimAsync(configuration.GetIdentity().Mode, cancellationToken);
        if (operation is null) return false;
        var input = JsonSerializer.Deserialize<ScoutingInput>(operation.InputJson!)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120), clock);
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var renewal = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ownsLease = true;
        var renewing = RenewAsync();
        ScoutingReport? result = null;
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
        if (providerFailure is not null) await failures.RejectProviderAsync(operation, providerFailure, cancellationToken);
        else if (timedOut) await failures.RejectTimeoutAsync(operation, cancellationToken);
        else if (result is not null && ScoutingReportValidator.IsValid(result, input)) await work.PublishAsync(operation, result, cancellationToken);
        else await failures.RejectInvalidAsync(operation, cancellationToken);
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
