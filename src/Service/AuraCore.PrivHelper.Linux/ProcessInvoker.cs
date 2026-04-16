using System.Diagnostics;
using System.Text;

namespace AuraCore.PrivHelper.Linux;

public interface IProcessInvoker
{
    Task<ProcessInvokerResult> InvokeAsync(string executable, string[] argv, int timeoutSeconds, CancellationToken ct = default);
}

public sealed record ProcessInvokerResult(int ExitCode, string Stdout, string Stderr, bool TimedOut);

public sealed class ProcessInvoker : IProcessInvoker
{
    public async Task<ProcessInvokerResult> InvokeAsync(string executable, string[] argv, int timeoutSeconds, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in argv)
            psi.ArgumentList.Add(arg);

        using var proc = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        try
        {
            proc.Start();
        }
        catch (Exception ex)
        {
            return new ProcessInvokerResult(-2, string.Empty, $"failed to start {executable}: {ex.Message}", false);
        }
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await proc.WaitForExitAsync(timeoutCts.Token);
            return new ProcessInvokerResult(proc.ExitCode, stdout.ToString(), stderr.ToString(), false);
        }
        catch (OperationCanceledException)
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            return new ProcessInvokerResult(-1, stdout.ToString(), stderr.ToString() + "\nkilled by timeout", true);
        }
    }
}
