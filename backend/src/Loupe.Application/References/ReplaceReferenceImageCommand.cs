using Loupe.Application.Images;
using MediatR;

namespace Loupe.Application.References;

public sealed record ReplaceReferenceImageCommand(Guid Id, long Revision, ImageUpload Image, string? OperationKey, int FileCount)
    : IRequest<ReferenceResult>;
