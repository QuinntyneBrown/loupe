namespace Loupe.Infrastructure.Ai;

public sealed class AiOptions
{
    public string? Mode { get; set; }
    public string Model { get; set; } = "gpt-5.4-mini-2026-03-17";
    public string? Endpoint { get; set; }
    public string? Deployment { get; set; }
    public string? ApiKey { get; set; }
    public int MaxConcurrentCalls { get; set; } = 4;
    public bool IsConfigured => Mode == "Live" && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(Deployment);

    public bool HasValidEndpoint => string.IsNullOrWhiteSpace(Endpoint)
        || (Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            && uri.UserInfo.Length == 0 && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0);
}
