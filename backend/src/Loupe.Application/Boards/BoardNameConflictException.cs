namespace Loupe.Application.Boards;

public sealed class BoardNameConflictException() : Exception("A board with this name already exists. Choose another name.");
