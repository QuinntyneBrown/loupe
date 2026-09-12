using Loupe.Application.References;

namespace Loupe.Api.References;

public sealed record SetReferenceTagsRequest(long Revision, ReferenceTagInput[]? Tags);
