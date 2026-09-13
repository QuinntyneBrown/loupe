using Loupe.Application.Videos;
using Loupe.Domain.Videos;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class VideoIndexStore(LibraryDbContext database) : IVideoIndexStore
{
    public async Task<bool> IndexNextAsync(string model, Func<Video, CancellationToken, Task<float[]>> embed, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var video = await database.Videos.FromSqlInterpolated($"""
            SELECT * FROM videos WHERE "EmbeddedRevision" IS DISTINCT FROM "Revision" OR "EmbeddingModel" IS DISTINCT FROM {model}
            ORDER BY "CreatedAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).AsNoTracking().Include(item => item.Tags).SingleOrDefaultAsync(cancellationToken);
        if (video is null) return false;
        var vector = await embed(video, cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE videos SET "Embedding" = {VectorLiteral.Format(vector)}::vector, "EmbeddedRevision" = "Revision", "EmbeddingModel" = {model} WHERE "Id" = {video.Id}
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
