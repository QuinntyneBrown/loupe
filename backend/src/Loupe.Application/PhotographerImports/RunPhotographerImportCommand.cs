using MediatR;

namespace Loupe.Application.PhotographerImports;

public sealed record RunPhotographerImportCommand : IRequest<bool>;
