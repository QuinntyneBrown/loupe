using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text;

namespace Loupe.Api.Tests.Persistence;

public sealed class CleanupProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task<string> output;
    private readonly Task<string> error;
    private readonly ConcurrentQueue<string> lines = new();
    private Task<string>? stopping;
    public string[] Lines => lines.ToArray();

    public CleanupProcess(string connectionString, string mediaRoot)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Loupe.Worker.dll"));
        start.Environment["ConnectionStrings__Library"] = connectionString;
        start.Environment["Media__Root"] = mediaRoot;
        start.Environment["Cleanup__PollInterval"] = "00:00:00.100";
        start.Environment["Logging__LogLevel__Default"] = "Warning";
        process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the cleanup worker.");
        output = ReadAsync(process.StandardOutput);
        error = ReadAsync(process.StandardError);
    }

    public async Task EnsureRunningAsync()
    {
        if (process.HasExited)
            throw new InvalidOperationException($"Cleanup worker exited ({process.ExitCode}): {await output} {await error}");
    }

    public Task<string> StopAndReadLogsAsync() => stopping ??= StopCoreAsync();
    public async ValueTask DisposeAsync() => await StopAndReadLogsAsync();

    private async Task<string> StopCoreAsync()
    {
        if (!process.HasExited) process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
        await Task.WhenAll(output, error);
        process.Dispose();
        return await output + Environment.NewLine + await error;
    }

    private async Task<string> ReadAsync(StreamReader reader)
    {
        var captured = new StringBuilder();
        while (await reader.ReadLineAsync() is { } line)
        {
            lines.Enqueue(line);
            captured.AppendLine(line);
        }
        return captured.ToString();
    }
}
