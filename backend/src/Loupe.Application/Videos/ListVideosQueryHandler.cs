using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.References;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Videos;

public sealed class ListVideosQueryHandler(ICurrentOwner owner, IVideoStore videos, IVideoEmbeddingConfiguration embeddings, IVideoEmbeddingProvider provider)
    : IRequestHandler<ListVideosQuery, VideoPage>
{
    /// <summary>Baseline cosine similarity below which a video is not a meaning match.</summary>
    public const double SimilarityThreshold = 0.20;

    public async Task<VideoPage> Handle(ListVideosQuery request, CancellationToken cancellationToken)
    {
        if (request.Mode is not ("keyword" or "meaning")) throw new RequestValidationException("mode", "Choose Keyword or Meaning.");
        var filter = Validate(request.Query, request.Topic, request.Tags, request.PageSize);
        return request.Mode == "meaning" ? await SearchByMeaningAsync(request, filter, cancellationToken) : await ListAsync(request, filter, cancellationToken);
    }

    private async Task<VideoPage> ListAsync(ListVideosQuery request, VideoFilter filter, CancellationToken cancellationToken)
    {
        var scope = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { type = "videos", owner.Id, filter, request.PageSize }))) + ".";
        if (request.Cursor is not null && (request.Cursor.Length > 200 || !request.Cursor.StartsWith(scope, StringComparison.Ordinal)))
            throw new RequestValidationException("cursor", "This cursor belongs to a different view. Refresh the list.");
        var cursor = CreatedCursor.Parse(request.Cursor?[scope.Length..]);
        var found = await videos.ListAsync(owner.Id, filter, request.PageSize + 1, cursor, cancellationToken);
        var items = found.Take(request.PageSize).Select(video => VideoResult.From(video, embeddings.CurrentModel)).ToArray();
        var next = found.Count > request.PageSize ? scope + new CreatedCursor(items[^1].CreatedAt, items[^1].Id).Encode() : null;
        return new(items, next, await videos.CountAsync(owner.Id, filter, cancellationToken));
    }

    private async Task<VideoPage> SearchByMeaningAsync(ListVideosQuery request, VideoFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Query.Length == 0) throw new RequestValidationException("query", "Describe what you're looking for to search by meaning.");
        if (request.Cursor is not null) throw new RequestValidationException("cursor", "Meaning search returns its best matches in one page.");
        var model = embeddings.CurrentModel ?? throw new IntegrationNotConfiguredException();
        float[] vector;
        try { vector = await provider.EmbedAsync(filter.Query, cancellationToken); }
        catch (ProviderFailureException) { throw new ServiceUnavailableException(); }
        var found = await videos.SearchAsync(owner.Id, model, vector, filter, SimilarityThreshold, request.PageSize, cancellationToken);
        return new(found.Matches.Select(match => VideoResult.From(match.Video, model, match.Score)).ToArray(), null, found.TotalCount);
    }

    public static VideoFilter Validate(string? query, string? topic, string[]? tags, int pageSize)
    {
        if (pageSize is < 1 or > 100) throw new RequestValidationException("pageSize", "Choose between 1 and 100 items.");
        if (query?.Contains('\0') == true) throw new RequestValidationException("query", "Remove the null character from the query.");
        var normalizedQuery = TextField.Normalize(query?.Normalize(NormalizationForm.FormC), 500, "query") ?? "";
        var normalizedTopic = string.IsNullOrEmpty(topic) ? null : VideoTopic.Validate(topic);
        if (tags?.Length > 10 || tags?.Any(tag => tag?.Contains('\0') == true) == true) throw new RequestValidationException("tags", "Choose up to 10 valid tags.");
        var normalizedTags = (tags ?? []).Select(tag => TagName.Validate(tag).ToUpperInvariant()).Distinct().Order(StringComparer.Ordinal).ToArray();
        return new(normalizedQuery, normalizedTopic, normalizedTags);
    }
}
