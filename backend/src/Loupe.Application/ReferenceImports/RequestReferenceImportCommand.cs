using Loupe.Application.Operations;
using MediatR;
namespace Loupe.Application.ReferenceImports;

public sealed record RequestReferenceImportCommand(Guid ReferenceId, long Revision, string? OperationKey) : IRequest<OperationResult>;
