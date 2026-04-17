using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace AuraCore.UI.Avalonia.Helpers;

public enum PrivilegedHelperInstallOutcome
{
    Success,
    UserCancelled,
    Timeout,
    Failed
}

public interface IPipeProbe
{
    Task<bool> CanConnectAsync(int timeoutMs, CancellationToken ct);
}

public sealed class NamedPipeProbe : IPipeProbe
{
    private readonly string _pipeName;

    public NamedPipeProbe(string pipeName = "AuraCorePro") { _pipeName = pipeName; }

    public async Task<bool> CanConnectAsync(int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(timeoutMs, ct);
            return client.IsConnected;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class PrivilegedHelperInstaller
{
    private readonly IPipeProbe _probe;
    private readonly Func<string, Task<bool>> _elevatorInvoke;
    private const int PipeConnectTimeoutMs = 2000;
    private const int PostInstallPollBudgetMs = 5000;
    private const int PostInstallPollStepMs = 200;

    public PrivilegedHelperInstaller(IPipeProbe probe, Func<string, Task<bool>> elevatorInvoke)
    {
        _probe = probe;
        _elevatorInvoke = elevatorInvoke;
    }

    public Task<bool> IsInstalledAsync(CancellationToken ct)
        => _probe.CanConnectAsync(PipeConnectTimeoutMs, ct);

    public async Task<PrivilegedHelperInstallOutcome> InstallAsync(CancellationToken ct)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "install-privileged-service.ps1");
        if (!File.Exists(scriptPath))
        {
            return PrivilegedHelperInstallOutcome.Failed;
        }

        bool elevatorOk;
        try { elevatorOk = await _elevatorInvoke(scriptPath); }
        catch { return PrivilegedHelperInstallOutcome.Failed; }

        if (!elevatorOk) return PrivilegedHelperInstallOutcome.UserCancelled;

        var deadline = DateTime.UtcNow.AddMilliseconds(PostInstallPollBudgetMs);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (await _probe.CanConnectAsync(PostInstallPollStepMs, ct))
                return PrivilegedHelperInstallOutcome.Success;
            try { await Task.Delay(PostInstallPollStepMs, ct); } catch (OperationCanceledException) { throw; }
        }

        return PrivilegedHelperInstallOutcome.Timeout;
    }

    public static Task<bool> DefaultElevatorInvoke(string scriptPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = false
            };
            using var proc = Process.Start(psi);
            if (proc is null) return Task.FromResult(false);
            proc.WaitForExit();
            return Task.FromResult(proc.ExitCode == 0);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User clicked "No" on UAC: Win32Exception with native error code 1223.
            return Task.FromResult(false);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
