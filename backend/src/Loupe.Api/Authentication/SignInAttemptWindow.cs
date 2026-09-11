namespace Loupe.Api.Authentication;

public sealed record SignInAttemptWindow(DateTimeOffset StartedAt, int Attempts);
