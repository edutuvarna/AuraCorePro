using System.Diagnostics;
using System.Text;

namespace AuraCore.Application.Shared;

/// <summary>
/// Shared helper for invoking external processes (shell commands) from modules.
/// Captures stdout/stderr, supports cancellation and timeout.
/// </summary>
public static class ProcessRunner
{
    public sealed record Result(
        bool Success,
        int ExitCode,
        string Stdout,
        string Stderr,
        string? Error = null);

    /// <summary>
    /// Run an external process and capture its output.
    /// </summary>
    /// <param name="fileName">Command to run (e.g. "systemctl", "brew", "docker").</param>
    /// <param name="arguments">Arguments string.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="workingDirectory">Optional working directory.</param>
    /// <param name="timeoutSeconds">Hard timeout in seconds (default: 60).</param>
    /// <returns>Result with success flag, exit code, stdout, stderr, and optional error.</returns>
    public static async Task<Result> RunAsync(
        string fileName,
        string arguments,
        CancellationToken ct = default,
        string? workingDirectory = null,
        int timeoutSeconds = 60)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            if (workingDirectory != null)
                psi.WorkingDirectory = workingDirectory;

            using var proc = Process.Start(psi);
            if (proc is null)
                return new Result(false, -1, "", "", "Process.Start returned null");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var stdoutTask = proc.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = proc.StandardError.ReadToEndAsync(cts.Token);
            await proc.WaitForExitAsync(cts.Token);
            return new Result(
                proc.ExitCode == 0,
                proc.ExitCode,
                await stdoutTask,
                await stderrTask);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessRunner] {fileName} {arguments}: {ex.Message}");
            return new Result(false, -1, "", "", ex.Message);
        }
    }

    /// <summary>
    /// Check whether a command exists in PATH.
    /// </summary>
    public static async Task<bool> CommandExistsAsync(string command, CancellationToken ct = default)
    {
        var lookup = OperatingSystem.IsWindows() ? "where" : "which";
        var result = await RunAsync(lookup, command, ct, timeoutSeconds: 10);
        return result.Success && !string.IsNullOrWhiteSpace(result.Stdout);
    }
}
