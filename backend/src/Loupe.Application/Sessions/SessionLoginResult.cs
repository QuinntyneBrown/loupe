namespace Loupe.Application.Sessions;

public sealed record SessionLoginResult(string Token, SessionResult Session);
