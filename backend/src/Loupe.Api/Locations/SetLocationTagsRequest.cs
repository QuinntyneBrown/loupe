using Loupe.Application.Locations;

namespace Loupe.Api.Locations;

public sealed record SetLocationTagsRequest(long Revision, LocationTagInput[]? Tags);
