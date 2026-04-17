using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.IPC.Contracts;

namespace AuraCore.PrivilegedService.Ops;

public sealed class DriverOperations
{
    private const string DriverBackupPrefix = @"C:\ProgramData\AuraCorePro\DriverBackup\";

    public async Task<DriverOperationResponse> ScanAsync(string correlationId, CancellationToken ct = default)
    {
        return await RunProcessAsync(correlationId, "pnputil.exe", "/scan-devices", ct);
    }

    public async Task<DriverOperationResponse> ExportAsync(string correlationId, string backupDirectory, CancellationToken ct = default)
    {
        if (!ArgumentValidator.IsSafeArgument(backupDirectory))
            return new DriverOperationResponse(correlationId, false, "", -1, "invalid backup path");
        if (!ArgumentValidator.IsPathUnderPrefix(backupDirectory, DriverBackupPrefix))
            return new DriverOperationResponse(correlationId, false, "", -1, "path outside allowed prefix");

        Directory.CreateDirectory(backupDirectory);
        return await RunProcessAsync(correlationId, "pnputil.exe", $"/export-driver * \"{backupDirectory}\"", ct);
    }

    private static async Task<DriverOperationResponse> RunProcessAsync(string correlationId, string exe, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe, args)
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
        linkedCts.CancelAfter(TimeSpan.FromSeconds(60));

        try
        {
            await proc.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            return new DriverOperationResponse(correlationId, false, "", -2, "timeout");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return new DriverOperationResponse(correlationId, proc.ExitCode == 0, stdout, proc.ExitCode, proc.ExitCode == 0 ? null : stderr);
    }
}
