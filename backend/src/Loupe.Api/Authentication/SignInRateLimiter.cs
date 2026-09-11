using Microsoft.Extensions.Caching.Memory;
namespace Loupe.Api.Authentication;

public sealed class SignInRateLimiter(TimeProvider clock) : IDisposable
{
    private readonly MemoryCache windows = new(new MemoryCacheOptions());
    private readonly object gate = new();
    public int Acquire(string address)
    {
        lock (gate)
        {
            var now = clock.GetUtcNow();
            var window = windows.Get<SignInAttemptWindow>(address);
            if (window is null || now >= window.StartedAt.AddMinutes(1)) window = new SignInAttemptWindow(now, 0);
            if (window.Attempts >= 10) return Math.Max(1, (int)Math.Ceiling((window.StartedAt.AddMinutes(1) - now).TotalSeconds));
            windows.Set(address, window with { Attempts = window.Attempts + 1 }, TimeSpan.FromMinutes(1));
            return 0;
        }
    }
    public void Dispose() => windows.Dispose();
}
