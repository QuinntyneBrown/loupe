using System.Diagnostics;

namespace Loupe.Api.Tests.Persistence;

public sealed class CleanupProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task<string> output;
    private readonly Task<string> error;

    public CleanupProcess(string connectionString, string mediaRoot)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Loupe.Worker.dll"));
        start.Environment["ConnectionStrings__Library"] = connectionString;
        start.Environment["Media__Root"] = mediaRoot;
        start.Environment["Cleanup__PollInterval"] = "00:00:00.100";
        start.Environment["Logging__LogLevel__Default"] = "Warning";
        process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the cleanup worker.");
        output = process.StandardOutput.ReadToEndAsync();
        error = process.StandardError.ReadToEndAsync();
    }

    public async Task EnsureRunningAsync()
    {
        if (process.HasExited)
            throw new InvalidOperationException($"Cleanup worker exited ({process.ExitCode}): {await output} {await error}");
    }

    public async ValueTask DisposeAsync()
    {
        if (!process.HasExited) process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
        await Task.WhenAll(output, error);
        process.Dispose();
    }
}
