using Loupe.Application.Images;
using MediatR;

namespace Loupe.Application.Photographs;

public sealed record UploadPhotographCommand(ImageUpload Image, string? Title, CritiqueBriefInput Brief, string? OperationKey, int FileCount) : IRequest<PhotographResult>;
