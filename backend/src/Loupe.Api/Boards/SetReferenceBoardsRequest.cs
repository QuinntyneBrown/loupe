namespace Loupe.Api.Boards;
public sealed record SetReferenceBoardsRequest(long Revision, Guid[]? BoardIds);
