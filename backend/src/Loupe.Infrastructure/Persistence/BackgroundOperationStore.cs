using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Domain.Critiques;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class BackgroundOperationStore(LibraryDbContext database, TimeProvider clock) : IBackgroundOperationStore
{
    public Task<BackgroundOperation?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        database.BackgroundOperations.AsNoTracking().SingleOrDefaultAsync(operation => operation.Id == id && operation.OwnerId == ownerId, cancellationToken);

    public async Task<Guid> AdmitCritiqueAsync(Guid photographId, string ownerId, long revision, AnalysisIdentity identity, CancellationToken cancellationToken)
    {
        var photograph = await database.Photographs.FromSqlInterpolated($"SELECT * FROM photographs WHERE \"Id\" = {photographId} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (photograph.Revision != revision) throw new RevisionConflictException();
        var now = clock.GetUtcNow();
        var operation = new BackgroundOperation
        {
            OwnerId = ownerId,
            ResourceId = photographId,
            Type = OperationType.Critique,
            Mode = identity.Mode,
            Model = identity.Model,
            PromptVersion = identity.PromptVersion,
            InputJson = JsonSerializer.Serialize(new CritiqueInput(photograph.ImageKey, photograph.Brief, photograph.Exif)),
            CreatedAt = now,
            UpdatedAt = now
        };
        database.BackgroundOperations.Add(operation);
        await database.SaveChangesAsync(cancellationToken);
        return operation.Id;
    }
}
