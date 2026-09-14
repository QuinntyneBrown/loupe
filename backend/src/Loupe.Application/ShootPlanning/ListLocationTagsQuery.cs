using Loupe.Application.Search;
using MediatR;

namespace Loupe.Application.ShootPlanning;

public sealed record ListLocationTagsQuery : IRequest<IReadOnlyList<SearchTagCount>>;
