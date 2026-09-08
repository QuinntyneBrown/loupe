using MediatR;

namespace Loupe.Application.References;

public sealed record SaveReferenceUrlCommand(string? SourceUrl, string? Title, string? Attribution, string? Notes, string? OperationKey) : IRequest<SaveReferenceUrlResult>;
