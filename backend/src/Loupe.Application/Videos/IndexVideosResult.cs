using Loupe.Application.Operations;

namespace Loupe.Application.Videos;

public sealed record IndexVideosResult(int Indexed, ProviderFailureKind? Failure);
