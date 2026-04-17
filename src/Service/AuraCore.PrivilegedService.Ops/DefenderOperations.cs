using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.IPC.Contracts;

namespace AuraCore.PrivilegedService.Ops;

public sealed class DefenderOperations
{
    public async Task<DefenderActionResponse> ExecuteAsync(string correlationId, DefenderAction action, string? target, CancellationToken ct = default)
    {
        var cmd = BuildPowerShellCommand(action, target);
        if (cmd is null)
            return new DefenderActionResponse(correlationId, false, "", -1, "invalid action parameters");

        return await RunPowerShellAsync(correlationId, cmd, ct);
    }

    private static string? BuildPowerShellCommand(DefenderAction action, string? target)
    {
        switch (action)
        {
            case DefenderAction.UpdateSignatures:
                return "Update-MpSignature -ErrorAction Stop";

            case DefenderAction.QuickScan:
                return "Start-MpScan -ScanType QuickScan -ErrorAction Stop";

            case DefenderAction.FullScan:
                return "Start-MpScan -ScanType FullScan -ErrorAction Stop";

            case DefenderAction.SetRealtimeEnabled:
                return "Set-MpPreference -DisableRealtimeMonitoring $false -ErrorAction Stop";

            case DefenderAction.SetRealtimeDisabled:
                return "Set-MpPreference -DisableRealtimeMonitoring $true -ErrorAction Stop";

            case DefenderAction.AddExclusion:
                if (string.IsNullOrEmpty(target) || !ArgumentValidator.IsSafeArgument(target))
                    return null;
                return $"Add-MpPreference -ExclusionPath '{target.Replace("'", "''")}' -ErrorAction Stop";

            case DefenderAction.RemoveExclusion:
                if (string.IsNullOrEmpty(target) || !ArgumentValidator.IsSafeArgument(target))
                    return null;
                return $"Remove-MpPreference -ExclusionPath '{target.Replace("'", "''")}' -ErrorAction Stop";

            case DefenderAction.RemoveThreat:
                if (string.IsNullOrEmpty(target) || !long.TryParse(target, out _))
                    return null;
                return $"Remove-MpThreat -ThreatID {target} -ErrorAction Stop";

            default:
                return null;
        }
    }

    private static async Task<DefenderActionResponse> RunPowerShellAsync(string correlationId, string command, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -Command \"{command.Replace("\"", "`\"")}\"")
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
        linkedCts.CancelAfter(TimeSpan.FromMinutes(2));

        try
        {
            await proc.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            return new DefenderActionResponse(correlationId, false, "", -2, "timeout");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return new DefenderActionResponse(correlationId, proc.ExitCode == 0, stdout, proc.ExitCode, proc.ExitCode == 0 ? null : stderr);
    }
}
