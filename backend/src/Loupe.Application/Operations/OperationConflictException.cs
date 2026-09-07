namespace Loupe.Application.Operations;

public sealed class OperationConflictException() : Exception("This operation key was already used with different content.");
