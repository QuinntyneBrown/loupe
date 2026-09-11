using System.Text.Json;
using Loupe.Application.Images;
using Loupe.Domain.References;
using MediatR;

namespace Loupe.Application.ReferenceImports;

public sealed class RunReferenceImportCommandHandler(IReferenceImportWorkStore work, IReferenceSourceReader reader, TimeProvider clock) : IRequestHandler<RunReferenceImportCommand, bool>
{
    public async Task<bool> Handle(RunReferenceImportCommand request, CancellationToken cancellationToken)
    {
        var operation = await work.ClaimAsync(cancellationToken);
        if (operation is null) return false;
        var input = JsonSerializer.Deserialize<ReferenceImportInput>(operation.InputJson!)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120), clock);
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var renewal = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ownsLease = true;
        var renewing = RenewAsync();
        ReferenceSourceResult result;
        try { result = await reader.ReadAsync(input.SourceUrl, attempt.Token).WaitAsync(attempt.Token); }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        { result = new(null, null, input.SourceUrl, null, "source_timeout"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt.IsCancellationRequested) { return true; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { result = new(null, null, input.SourceUrl, null, "source_timeout"); }
        catch (SourceImportException failure) { result = new(null, null, input.SourceUrl, null, failure.Code); }
        catch (SourceFetchException) { result = new(null, null, input.SourceUrl, null, "source_restricted"); }
        catch (Exception failure) when (failure is HttpRequestException or IOException or InvalidDataException or ImageValidationException)
        { result = new(null, null, input.SourceUrl, null, "source_unavailable"); }
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
                    ownsLease = false;
                    await attempt.CancelAsync();
                    return;
                }
            }
            catch (OperationCanceledException) when (renewal.IsCancellationRequested) { }
            catch { ownsLease = false; await attempt.CancelAsync(); throw; }
        }
    }
}
