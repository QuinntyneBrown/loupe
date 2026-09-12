using System.Text.Json;
using Loupe.Application.Critiques;
using Loupe.Application.Operations;
using Loupe.Domain.Critiques;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class CritiqueWorkStore(LibraryDbContext database, TimeProvider clock, IOperationLeaseStore leases, IAnalysisFailureStore failures) : ICritiqueWorkStore
{
    public Task<BackgroundOperation?> ClaimAsync(ExecutionMode mode, CancellationToken cancellationToken) =>
        leases.ClaimAsync([new OperationCapability(OperationType.Critique, mode)], cancellationToken);

    public Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken) =>
        leases.RenewAsync(operation, cancellationToken);

    public Task RejectInvalidAsync(BackgroundOperation operation, CancellationToken cancellationToken) => failures.RejectInvalidAsync(operation, cancellationToken);
    public Task RejectTimeoutAsync(BackgroundOperation operation, CancellationToken cancellationToken) => failures.RejectTimeoutAsync(operation, cancellationToken);
    public Task RejectProviderAsync(BackgroundOperation operation, ProviderFailureException failure, CancellationToken cancellationToken) => failures.RejectProviderAsync(operation, failure, cancellationToken);
    public async Task PublishAsync(BackgroundOperation operation, CritiqueResult result, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var photograph = await database.Photographs.FromSqlInterpolated($"SELECT * FROM photographs WHERE \"Id\" = {operation.ResourceId} AND \"OwnerId\" = {operation.OwnerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (photograph is null) return;
        var now = clock.GetUtcNow();
        var input = JsonSerializer.Deserialize<CritiqueInput>(operation.InputJson!)!;
        var outputJson = JsonSerializer.Serialize(new SavedCritique(operation.Id, now, operation.Mode,
            operation.Model, operation.PromptVersion, input.Brief, result));
        var changed = await database.BackgroundOperations.Where(item => item.Id == operation.Id && item.LeaseToken == operation.LeaseToken
            && item.Status == OperationStatus.Running && item.LeaseExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, OperationStatus.Succeeded)
                .SetProperty(item => item.CompletedAt, now).SetProperty(item => item.UpdatedAt, now)
                .SetProperty(item => item.LeaseToken, (Guid?)null).SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(item => item.OutputJson, outputJson)
                .SetProperty(item => item.Message, "Critique saved."), cancellationToken);
        if (changed == 0) return;
        photograph.CritiqueJson = outputJson;
        photograph.Revision++;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
