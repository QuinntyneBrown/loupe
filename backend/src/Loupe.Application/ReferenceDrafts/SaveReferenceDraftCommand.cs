using Loupe.Application.References;
using MediatR;
namespace Loupe.Application.ReferenceDrafts;

public sealed record SaveReferenceDraftCommand(Guid Id, long Revision, string? Title, string? SourceUrl, string? Attribution,
    string? Notes, Guid[]? BoardIds, string? OperationKey) : IRequest<SaveReferenceUrlResult>;
