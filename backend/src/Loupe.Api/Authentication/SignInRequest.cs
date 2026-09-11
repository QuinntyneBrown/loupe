namespace Loupe.Api.Authentication;

public sealed record SignInRequest(string? Email, string? Password);
