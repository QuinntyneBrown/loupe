using MediatR;

namespace Loupe.Application.Videos;

public sealed record DeleteVideoCommand(Guid Id, long Revision) : IRequest;
