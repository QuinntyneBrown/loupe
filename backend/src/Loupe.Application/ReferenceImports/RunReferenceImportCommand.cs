using MediatR;

namespace Loupe.Application.ReferenceImports;

public sealed record RunReferenceImportCommand : IRequest<bool>;
