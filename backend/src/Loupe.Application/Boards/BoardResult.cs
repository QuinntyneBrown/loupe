namespace Loupe.Application.Boards;

public sealed record BoardResult(Guid Id, string Name, long Revision, int ReferenceCount);
