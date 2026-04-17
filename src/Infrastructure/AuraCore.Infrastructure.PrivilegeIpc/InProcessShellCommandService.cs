using System.Diagnostics;
using System.Text;
using AuraCore.Application.Interfaces.Platform;
using Microsoft.Extensions.Logging;

namespace AuraCore.Infrastructure.PrivilegeIpc;

public sealed class InProcessShellCommandService : IShellCommandService
{
    private readonly ILogger<InProcessShellCommandService> _logger;

    public InProcessShellCommandService(ILogger<InProcessShellCommandService> logger)
    {
        _logger = logger;
    }

    public async Task<ShellResult> RunPrivilegedAsync(PrivilegedCommand command, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[inproc] action={ActionId} exe={Executable} argv={Argv}",
            command.Id, command.Executable, string.Join(' ', command.Arguments));

        var psi = new ProcessStartInfo(command.Executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in command.Arguments)
            psi.ArgumentList.Add(arg);

        try
        {
            using var proc = new Process { StartInfo = psi };
            var stdoutSb = new StringBuilder();
            var stderrSb = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdoutSb.AppendLine(e.Data); };
            proc.ErrorDataReceived  += (_, e) => { if (e.Data is not null) stderrSb.AppendLine(e.Data); };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(command.TimeoutSeconds));

            try
            {
                await proc.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return new ShellResult(
                    Success: false, ExitCode: -1,
                    Stdout: stdoutSb.ToString(), Stderr: stderrSb.ToString(),
                    AuthResult: PrivilegeAuthResult.AlreadyAuthorized);
            }

            return new ShellResult(
                Success: proc.ExitCode == 0,
                ExitCode: proc.ExitCode,
                Stdout: stdoutSb.ToString(),
                Stderr: stderrSb.ToString(),
                AuthResult: PrivilegeAuthResult.AlreadyAuthorized);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[inproc] action={ActionId} failed to start", command.Id);
            return new ShellResult(
                Success: false, ExitCode: -2,
                Stdout: "", Stderr: ex.Message,
                AuthResult: PrivilegeAuthResult.HelperMissing);
        }
    }
}
