namespace Loupe.Application.Videos;

public sealed class VideoConflictException() : Exception("This video is already saved. Open its existing bookmark or enter a different URL.");
