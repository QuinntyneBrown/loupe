using System.Text.Json;
using Loupe.Application.Operations;
using Loupe.Application.PhotographerImports;
using Loupe.Application.ReferenceImports;
using Loupe.Domain.Operations;
using Loupe.Domain.Photographers;
using MediatR;

namespace Loupe.Application.PhotographerSummaries;

public sealed class RunPhotographerSummaryCommandHandler(IPhotographerSummaryWorkStore work, IPortfolioSourceReader reader,
    IPhotographerSummaryProvider provider, IPhotographerSummaryConfiguration configuration, IAnalysisFailureStore failures,
    TimeProvider clock) : IRequestHandler<RunPhotographerSummaryCommand, bool>
{
    public async Task<bool> Handle(RunPhotographerSummaryCommand request, CancellationToken cancellationToken)
    {
        var operation = await work.ClaimAsync(configuration.GetIdentity().Mode, cancellationToken);
        if (operation is null) return false;
        var input = JsonSerializer.Deserialize<PhotographerSummaryInput>(operation.InputJson!)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120), clock);
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var renewal = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ownsLease = true;
        var renewing = RenewAsync();
        var source = input.Source;
        string? sourceFailure = null;
        PhotographerSummaryResult? result = null;
        ProviderFailureException? providerFailure = null;
        var timedOut = false;
        try
        {
            if (source is null)
            {
                var read = await reader.ReadAsync(input.PortfolioUrl, attempt.Token).WaitAsync(attempt.Token);
                source = read.Source; sourceFailure = read.FailureCode;
            }
            if (source is not null)
                result = await provider.GenerateAsync(source, new AnalysisIdentity(operation.Mode, operation.Model, operation.PromptVersion), attempt.Token).WaitAsync(attempt.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { timedOut = true; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt.IsCancellationRequested) { return true; }
        catch (ProviderFailureException failure) { providerFailure = failure; }
        catch (SourceImportException failure) { sourceFailure = failure.Code; }
        catch (SourceFetchException) { sourceFailure = "source_restricted"; }
        catch (Exception failure) when (failure is HttpRequestException or IOException or InvalidDataException) { sourceFailure = "source_unavailable"; }
        finally { await renewal.CancelAsync(); await renewing; }
        if (!ownsLease) return true;
        if (providerFailure is not null) await failures.RejectProviderAsync(operation, providerFailure, cancellationToken);
        else if (timedOut) await failures.RejectTimeoutAsync(operation, cancellationToken);
        else if (source is null) await work.PublishAsync(operation, null, null, sourceFailure ?? "source_empty", cancellationToken);
        else if (PhotographerSummaryResultValidator.IsValid(result, source)) await work.PublishAsync(operation, source, result, null, cancellationToken);
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
                    ownsLease = false; await attempt.CancelAsync(); return;
                }
            }
            catch (OperationCanceledException) when (renewal.IsCancellationRequested) { }
            catch { ownsLease = false; await attempt.CancelAsync(); throw; }
        }
    }
}
