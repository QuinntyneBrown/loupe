using Loupe.Application.Images;
using MediatR;

namespace Loupe.Application.References;

public sealed record UploadReferenceCommand(ImageUpload Image, string? Title, string? SourceUrl,
    string? Attribution, string? Notes, string? OperationKey, int FileCount) : IRequest<ReferenceResult>;
