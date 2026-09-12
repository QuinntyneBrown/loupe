using System.Text.Json;
using Loupe.Application.ReferenceImports;
using MediatR;

namespace Loupe.Application.PhotographerImports;

public sealed class RunPhotographerImportCommandHandler(IPhotographerImportWorkStore work, IPortfolioSourceReader reader, TimeProvider clock) : IRequestHandler<RunPhotographerImportCommand, bool>
{
    public async Task<bool> Handle(RunPhotographerImportCommand request, CancellationToken cancellationToken)
    {
        var operation = await work.ClaimAsync(cancellationToken);
        if (operation is null) return false;
        var source = JsonSerializer.Deserialize<string>(operation.InputJson!)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120), clock);
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var renewal = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ownsLease = true;
        var renewing = RenewAsync();
        PortfolioSourceResult result;
        try { result = await reader.ReadAsync(source, attempt.Token).WaitAsync(attempt.Token); }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { result = new(null, "source_timeout"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt.IsCancellationRequested) { return true; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { result = new(null, "source_timeout"); }
        catch (SourceImportException failure) { result = new(null, failure.Code); }
        catch (SourceFetchException) { result = new(null, "source_restricted"); }
        catch (Exception failure) when (failure is HttpRequestException or IOException or InvalidDataException) { result = new(null, "source_unavailable"); }
        finally { await renewal.CancelAsync(); await renewing; }
        if (ownsLease) await work.PublishAsync(operation, result, cancellationToken);
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
