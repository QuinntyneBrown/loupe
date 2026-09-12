using Loupe.Application.Images;
using MediatR;

namespace Loupe.Application.ReferenceDrafts;

public sealed record UploadReferenceDraftCommand(ImageUpload Image, string? SourceUrl, string? OperationKey, int FileCount) : IRequest<ReferenceDraftResult>;
