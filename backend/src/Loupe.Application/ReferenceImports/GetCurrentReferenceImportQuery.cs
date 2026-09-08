using Loupe.Application.Operations;
using MediatR;

namespace Loupe.Application.ReferenceImports;

public sealed record GetCurrentReferenceImportQuery(Guid ReferenceId) : IRequest<OperationResult?>;
