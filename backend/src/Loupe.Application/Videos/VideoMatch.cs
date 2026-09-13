using Loupe.Domain.Videos;

namespace Loupe.Application.Videos;

public sealed record VideoMatch(Video Video, double Score);
