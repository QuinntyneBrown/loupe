using Loupe.Application.Common;
using Loupe.Application.Videos;
using Loupe.Domain.Videos;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Loupe.Infrastructure.Persistence;

public sealed class VideoStore(LibraryDbContext database) : IVideoStore
{
    public Task<Video?> FindOwnedAsync(string ownerId, Guid id, CancellationToken cancellationToken) =>
        database.Videos.AsNoTracking().Include(item => item.Tags).SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken);

    public async Task<Video> SaveAsync(Video video, CancellationToken cancellationToken)
    {
        if (await database.Videos.AnyAsync(item => item.OwnerId == video.OwnerId && item.VideoId == video.VideoId, cancellationToken)) throw new VideoConflictException();
        database.Videos.Add(video);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { throw new VideoConflictException(); }
        catch (DbUpdateException exception) when (exception.InnerException is NpgsqlException { IsTransient: true }) { throw new ServiceUnavailableException(); }
        return video;
    }

    public async Task<Video> UpdateAsync(string ownerId, Guid id, long revision, VideoMetadata metadata, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var video = await database.Videos.FromSqlInterpolated($"SELECT * FROM videos WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .Include(item => item.Tags).SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (video.Revision != revision) throw new RevisionConflictException();
        if (await database.Videos.AnyAsync(item => item.OwnerId == ownerId && item.Id != id && item.VideoId == metadata.VideoId, cancellationToken)) throw new VideoConflictException();
        video.Title = metadata.Title; video.VideoId = metadata.VideoId; video.Topic = metadata.Topic;
        video.Channel = metadata.Channel; video.Summary = metadata.Summary; video.Notes = metadata.Notes;
        var names = metadata.Tags.Select(tag => tag.Name!.ToUpperInvariant()).ToHashSet();
        foreach (var tag in video.Tags.Where(tag => !names.Contains(tag.NormalizedName)).ToArray()) video.Tags.Remove(tag);
        foreach (var input in metadata.Tags)
        {
            var name = input.Name!.ToUpperInvariant();
            var tag = video.Tags.SingleOrDefault(tag => tag.NormalizedName == name);
            if (tag is null) video.Tags.Add(new VideoTag { VideoId = id, OwnerId = ownerId, NormalizedName = name, Name = input.Name, Category = input.Category, Provenance = "manual" });
            else if (tag.Name != input.Name || tag.Category != input.Category) { tag.Name = input.Name; tag.Category = input.Category; tag.Provenance = "manual"; }
        }
        video.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { throw new VideoConflictException(); }
        await transaction.CommitAsync(cancellationToken); return video;
    }

    public async Task DeleteAsync(string ownerId, Guid id, long revision, CancellationToken cancellationToken)
    {
        var video = await database.Videos.SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken) ?? throw new ResourceNotFoundException();
        if (video.Revision != revision) throw new RevisionConflictException();
        database.Videos.Remove(video);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
    }

    public Task<int> CountAsync(string ownerId, VideoFilter filter, CancellationToken cancellationToken) => Matching(ownerId, filter).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Video>> ListAsync(string ownerId, VideoFilter filter, int count, CreatedCursor? cursor, CancellationToken cancellationToken)
    {
        var query = Matching(ownerId, filter);
        if (cursor is not null) query = query.Where(item => item.CreatedAt < cursor.CreatedAt || item.CreatedAt == cursor.CreatedAt && item.Id.CompareTo(cursor.Id) > 0);
        return await query.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id).Take(count).Include(item => item.Tags).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VideoTagCount>> ListTagsAsync(string ownerId, CancellationToken cancellationToken) =>
        await database.Database.SqlQuery<VideoTagCount>($"""
            SELECT min("Name" COLLATE "C") AS "Name", count(*)::integer AS "Count"
            FROM video_tags WHERE "OwnerId" = {ownerId}
            GROUP BY "NormalizedName" ORDER BY count(*) DESC, "NormalizedName" COLLATE "C"
            """).ToListAsync(cancellationToken);

    public async Task<VideoSearchResult> SearchAsync(string ownerId, string model, float[] query, VideoFilter filter, double threshold, int count, CancellationToken cancellationToken)
    {
        var literal = VectorLiteral.Format(query);
        var scores = await database.Database.SqlQuery<VideoScore>($"""
            SELECT v."Id", 1 - (v."Embedding" <=> {literal}::vector) AS "Score" FROM videos v
            WHERE v."OwnerId" = {ownerId} AND v."Embedding" IS NOT NULL AND v."EmbeddedRevision" = v."Revision" AND v."EmbeddingModel" = {model}
                AND ({filter.Topic}::text IS NULL OR v."Topic" = {filter.Topic}::text)
                AND ARRAY(SELECT t."NormalizedName" FROM video_tags t WHERE t."VideoId" = v."Id" AND t."OwnerId" = {ownerId}) @> {filter.Tags}
                AND 1 - (v."Embedding" <=> {literal}::vector) >= {threshold}
            ORDER BY 1 - (v."Embedding" <=> {literal}::vector) DESC, v."Id"
            """).ToListAsync(cancellationToken);
        var page = scores.Take(count).ToArray();
        var ids = page.Select(score => score.Id).ToArray();
        var found = await database.Videos.AsNoTracking().Include(item => item.Tags).Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        return new(page.Where(score => found.ContainsKey(score.Id)).Select(score => new VideoMatch(found[score.Id], score.Score)).ToArray(), scores.Count);
    }

    private IQueryable<Video> Matching(string ownerId, VideoFilter filter)
    {
        var items = database.Videos.AsNoTracking().Where(item => item.OwnerId == ownerId);
        if (filter.Topic is not null) items = items.Where(item => item.Topic == filter.Topic);
        foreach (var tag in filter.Tags) items = items.Where(item => item.Tags.Any(existing => existing.NormalizedName == tag));
        if (filter.Query.Length == 0) return items;
        var pattern = "%" + filter.Query.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
        return items.Where(item => EF.Functions.ILike(item.Title, pattern, "\\") || EF.Functions.ILike(item.Channel ?? "", pattern, "\\")
            || EF.Functions.ILike(item.Summary ?? "", pattern, "\\") || EF.Functions.ILike(item.Notes ?? "", pattern, "\\")
            || item.Tags.Any(tag => EF.Functions.ILike(tag.Name, pattern, "\\")));
    }
}
