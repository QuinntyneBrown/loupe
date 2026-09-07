namespace Loupe.Application.Images;

public sealed record ImageUpload(Func<Stream> OpenRead, string Filename, string ContentType, long Length);
