using System.Diagnostics;
using System.Text;

namespace GameTranslate.Core;

public sealed record ProcessResult(int ExitCode, string Output)
{
    public bool Success => ExitCode == 0;
}

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, string? workingDirectory, CancellationToken cancellationToken);
}

public static class Retry
{
    // Archive mounts fail intermittently when another process (AV scan, Steam, indexer) has the
    // multi-GB containers open; one spaced retry absorbs that class of transient I/O failure.
    public static async Task<T> OnceAsync<T>(Func<Task<T>> action, Func<T, bool> success, Action<string>? onRetry = null, int delayMilliseconds = 2000)
    {
        var first = await action();
        if (success(first)) return first;
        onRetry?.Invoke("first attempt failed; retrying once");
        await Task.Delay(delayMilliseconds);
        return await action();
    }
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, string? workingDirectory, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory ?? string.Empty,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) output.AppendLine(eventArgs.Data); };
        process.ErrorDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) output.AppendLine(eventArgs.Data); };
        if (!process.Start()) throw new InvalidOperationException($"無法啟動工具：{executable}");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
        return new(process.ExitCode, output.ToString());
    }
}
