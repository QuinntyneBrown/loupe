using Loupe.Application.Images;
using MediatR;

namespace Loupe.Application.Locations;

public sealed record AddLocationImageCommand(Guid Id, ImageUpload Image, string? OperationKey, int FileCount) : IRequest<LocationResult>;
