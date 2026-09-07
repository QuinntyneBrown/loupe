namespace Loupe.Api.Photographs;

public sealed record UpdateNotesRequest(long Revision, string? Notes);
