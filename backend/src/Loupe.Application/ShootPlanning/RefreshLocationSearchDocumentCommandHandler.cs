using Loupe.Application.Operations;
using Loupe.Application.Search;
using MediatR;

namespace Loupe.Application.ShootPlanning;

public sealed class RefreshLocationSearchDocumentCommandHandler(ILocationIndexWorkStore work, IEmbeddingProvider embeddings, IEmbeddingConfiguration configuration,
    IAnalysisFailureStore failures, TimeProvider clock) : IRequestHandler<RefreshLocationSearchDocumentCommand, bool>
{
    public async Task<bool> Handle(RefreshLocationSearchDocumentCommand request, CancellationToken cancellationToken)
    {
        if (!configuration.IsConfigured) return false;
        var operation = await work.ClaimAsync(cancellationToken);
        if (operation is null) return false;
        var source = await work.ReadAsync(operation, cancellationToken);
        if (source is null)
        {
            // The location was deleted after the intent was recorded; nothing to embed.
            await work.PublishAsync(operation, new LocationIndexSource(-1, ""), [], cancellationToken);
            return true;
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120), clock);
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var renewal = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ownsLease = true;
        var renewing = RenewAsync();
        float[]? vector = null;
        var timedOut = false;
        ProviderFailureException? providerFailure = null;
        try
        {
            vector = await embeddings.EmbedAsync(source.Document, attempt.Token).WaitAsync(attempt.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { timedOut = true; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt.IsCancellationRequested) { return true; }
        catch (ProviderFailureException failure) { providerFailure = failure; }
        finally
        {
            await renewal.CancelAsync();
            await renewing;
        }
        if (!ownsLease) return true;
        if (providerFailure is not null) await failures.RejectProviderAsync(operation, providerFailure, cancellationToken);
        else if (timedOut) await failures.RejectTimeoutAsync(operation, cancellationToken);
        else await work.PublishAsync(operation, source, vector!, cancellationToken);
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
