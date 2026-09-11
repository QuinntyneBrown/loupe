using Loupe.Application.Images;

namespace Loupe.Application.ReferenceImports;

public sealed record ReferenceSourceResult(string? Title, string? Attribution, string FetchedUrl, ProcessedImage? Image, string? FailureCode = null);
