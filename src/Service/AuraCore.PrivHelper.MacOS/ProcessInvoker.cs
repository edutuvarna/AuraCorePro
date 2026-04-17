using System.Diagnostics;
using System.Text;

namespace AuraCore.PrivHelper.MacOS;

// ---------------------------------------------------------------------------
// IProcessInvoker + ProcessInvokerResult
// ---------------------------------------------------------------------------

/// <summary>
/// Contract for spawning a privileged subprocess and capturing its output.
/// Isolated from the Linux daemon — daemons intentionally share no packages
/// (defense-in-depth: a supply-chain compromise of one doesn't cascade).
/// </summary>
internal interface IProcessInvoker
{
    /// <summary>
    /// Spawn <paramref name="executable"/> with <paramref name="argv"/>,
    /// wait up to <paramref name="timeoutSeconds"/>, and return the result.
    /// </summary>
    Task<ProcessInvokerResult> InvokeAsync(
        string executable,
        string[] argv,
        int timeoutSeconds,
        CancellationToken ct = default);
}

/// <summary>
/// Outcome record from <see cref="IProcessInvoker.InvokeAsync"/>.
/// </summary>
internal sealed record ProcessInvokerResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    bool TimedOut);

// ---------------------------------------------------------------------------
// ProcessInvoker — System.Diagnostics.Process based implementation
// ---------------------------------------------------------------------------

/// <summary>
/// Production implementation of <see cref="IProcessInvoker"/>.
/// Mirrors the Linux daemon's ProcessInvoker — see
/// <c>AuraCore.PrivHelper.Linux.ProcessInvoker</c>.
/// <para>
/// Security properties:
/// <list type="bullet">
///   <item><c>UseShellExecute = false</c> — no shell expansion of argv.</item>
///   <item>Each argv token added to <c>ArgumentList</c> — no concatenation.</item>
///   <item>Timeout enforced via linked <c>CancellationTokenSource</c>; process
///   killed (entire tree) on timeout.</item>
/// </list>
/// </para>
/// </summary>
internal sealed class ProcessInvoker : IProcessInvoker
{
    /// <inheritdoc />
    public async Task<ProcessInvokerResult> InvokeAsync(
        string executable,
        string[] argv,
        int timeoutSeconds,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };

        // Add each token individually — prevents shell expansion
        foreach (var arg in argv)
            psi.ArgumentList.Add(arg);

        using var proc = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) stdout.AppendLine(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderr.AppendLine(e.Data);
        };

        try
        {
            proc.Start();
        }
        catch (Exception ex)
        {
            return new ProcessInvokerResult(
                ExitCode: -2,
                Stdout: string.Empty,
                Stderr: $"failed to start {executable}: {ex.Message}",
                TimedOut: false);
        }

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await proc.WaitForExitAsync(timeoutCts.Token);
            return new ProcessInvokerResult(
                ExitCode: proc.ExitCode,
                Stdout: stdout.ToString(),
                Stderr: stderr.ToString(),
                TimedOut: false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!proc.HasExited)
                    proc.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore kill errors — process may have already exited
            }

            return new ProcessInvokerResult(
                ExitCode: -1,
                Stdout: stdout.ToString(),
                Stderr: stderr.ToString() + "\nkilled by timeout",
                TimedOut: true);
        }
    }
}
