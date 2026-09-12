namespace Loupe.Api.Boards;
public sealed record RenameBoardRequest(long Revision, string? Name);
