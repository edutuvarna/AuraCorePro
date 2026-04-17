using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.IPC.Contracts;

namespace AuraCore.PrivilegedService.Ops;

public sealed class ServiceOperations
{
    public async Task<ServiceControlResponse> ExecuteAsync(string correlationId, string serviceName, ServiceAction action, CancellationToken ct = default)
    {
        if (!ArgumentValidator.IsValidServiceName(serviceName))
            return new ServiceControlResponse(correlationId, false, "", -1, "invalid service name");

        return action switch
        {
            ServiceAction.Start                 => await RunAsync(correlationId, $"start {serviceName}", ct),
            ServiceAction.Stop                  => await RunAsync(correlationId, $"stop {serviceName}", ct),
            ServiceAction.Restart               => await RestartAsync(correlationId, serviceName, ct),
            ServiceAction.SetStartupAutomatic   => await RunAsync(correlationId, $"config {serviceName} start= auto", ct),
            ServiceAction.SetStartupManual      => await RunAsync(correlationId, $"config {serviceName} start= demand", ct),
            ServiceAction.SetStartupDisabled    => await RunAsync(correlationId, $"config {serviceName} start= disabled", ct),
            _ => new ServiceControlResponse(correlationId, false, "", -1, "unknown action")
        };
    }

    private static async Task<ServiceControlResponse> RestartAsync(string correlationId, string name, CancellationToken ct)
    {
        var stopResp = await RunAsync(correlationId, $"stop {name}", ct);
        // sc.exe stop returns non-zero if service already stopped; treat as non-fatal
        await Task.Delay(1500, ct);
        var startResp = await RunAsync(correlationId, $"start {name}", ct);
        return new ServiceControlResponse(
            correlationId,
            startResp.Success,
            $"stop: {stopResp.Output}\nstart: {startResp.Output}",
            startResp.ExitCode,
            startResp.Error);
    }

    private static async Task<ServiceControlResponse> RunAsync(string correlationId, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("sc.exe", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await proc.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            return new ServiceControlResponse(correlationId, false, "", -2, "timeout");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return new ServiceControlResponse(correlationId, proc.ExitCode == 0, stdout, proc.ExitCode, proc.ExitCode == 0 ? null : stderr);
    }
}
