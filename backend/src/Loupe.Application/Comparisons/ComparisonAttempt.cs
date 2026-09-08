using Loupe.Application.Photographs;
using Loupe.Domain.Critiques;

namespace Loupe.Application.Comparisons;

public sealed record ComparisonAttempt(PhotographResult Photograph, SavedCritique Critique);
