namespace Loupe.Application.Common;

public sealed class RevisionConflictException() : Exception("This photograph changed. Reload the latest version before saving your edit.");
