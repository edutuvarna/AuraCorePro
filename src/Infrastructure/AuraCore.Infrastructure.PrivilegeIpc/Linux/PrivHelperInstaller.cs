using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AuraCore.Infrastructure.PrivilegeIpc.Linux;

public sealed record InstallOutcome(
    bool Success,
    int ExitCode,
    string Stdout,
    string Stderr,
    string? StageDir);      // null if extract failed before any staging

public interface IPkexecInvoker
{
    /// <summary>
    /// Spawns `pkexec bash <installScript> <stageDir>` and waits for it to complete.
    /// Implementations may mock this for testing.
    /// </summary>
    Task<(int ExitCode, string Stdout, string Stderr)> InvokeAsync(
        string installScriptPath, string stageDir, CancellationToken ct = default);
}

public sealed class DefaultPkexecInvoker : IPkexecInvoker
{
    public async Task<(int ExitCode, string Stdout, string Stderr)> InvokeAsync(
        string installScriptPath, string stageDir, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("pkexec")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("bash");
        psi.ArgumentList.Add(installScriptPath);
        psi.ArgumentList.Add(stageDir);

        using var proc = new Process { StartInfo = psi };
        var stdout = new System.Text.StringBuilder();
        var stderr = new System.Text.StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        try
        {
            proc.Start();
        }
        catch (Exception ex)
        {
            return (-2, string.Empty, $"failed to launch pkexec: {ex.Message}");
        }
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            return (-3, stdout.ToString(), stderr.ToString() + "\ncancelled");
        }

        return (proc.ExitCode, stdout.ToString(), stderr.ToString());
    }
}

public interface IDaemonBinaryLocator
{
    /// <summary>
    /// Returns the filesystem path of the auracore-privhelper binary
    /// shipped alongside the main app, or null if it cannot be located.
    /// </summary>
    string? LocateDaemonBinary();
}

public sealed class DefaultDaemonBinaryLocator : IDaemonBinaryLocator
{
    // Search order:
    //   1. env var AURACORE_PRIVHELPER_BIN (override for dev/packaging)
    //   2. <AppContext.BaseDirectory>/privhelper/auracore-privhelper
    //   3. <AppContext.BaseDirectory>/auracore-privhelper
    //   4. <AppContext.BaseDirectory>/../lib/auracore-privhelper
    public string? LocateDaemonBinary()
    {
        var envOverride = Environment.GetEnvironmentVariable("AURACORE_PRIVHELPER_BIN");
        if (!string.IsNullOrWhiteSpace(envOverride) && File.Exists(envOverride))
            return envOverride;

        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "privhelper", "auracore-privhelper"),
            Path.Combine(baseDir, "auracore-privhelper"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "lib", "auracore-privhelper")),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }
}

public sealed class PrivHelperInstaller
{
    private readonly IPkexecInvoker _pkexec;
    private readonly IDaemonBinaryLocator _binaryLocator;
    private readonly ILogger<PrivHelperInstaller> _logger;

    public PrivHelperInstaller(
        IPkexecInvoker pkexec,
        IDaemonBinaryLocator binaryLocator,
        ILogger<PrivHelperInstaller> logger)
    {
        _pkexec = pkexec;
        _binaryLocator = binaryLocator;
        _logger = logger;
    }

    /// <summary>
    /// One-shot flow: extract embedded resources to a fresh staging dir,
    /// copy the daemon binary alongside them, then invoke pkexec bash install.sh.
    /// </summary>
    public async Task<InstallOutcome> ExtractAndInstallAsync(CancellationToken ct = default)
    {
        string stageDir;
        try
        {
            stageDir = await ExtractStageAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[privhelper-install] stage extraction failed");
            return new InstallOutcome(false, -1, string.Empty, ex.Message, null);
        }

        _logger.LogInformation("[privhelper-install] invoking pkexec bash {Script} {Stage}",
            Path.Combine(stageDir, "install.sh"), stageDir);
        var (exit, stdout, stderr) = await _pkexec.InvokeAsync(
            Path.Combine(stageDir, "install.sh"), stageDir, ct);

        return new InstallOutcome(exit == 0, exit, stdout, stderr, stageDir);
    }

    /// <summary>
    /// Test-visible helper that performs only the extraction half.
    /// Returns path of stage dir; caller cleans up.
    /// </summary>
    public async Task<string> ExtractStageAsync(CancellationToken ct = default)
    {
        var binaryPath = _binaryLocator.LocateDaemonBinary()
            ?? throw new FileNotFoundException(
                "auracore-privhelper binary not available in this build. " +
                "Ensure the daemon is built and packaged alongside the main app, " +
                "or set AURACORE_PRIVHELPER_BIN environment variable.");

        var stageDir = Path.Combine(
            Path.GetTempPath(),
            "auracore-privhelper-stage-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(stageDir);
        _logger.LogInformation("[privhelper-install] stage dir {Path}", stageDir);

        await ExtractResourceAsync("install.sh", Path.Combine(stageDir, "install.sh"), ct);
        await ExtractResourceAsync("pro.auracore.privhelper.policy",
            Path.Combine(stageDir, "pro.auracore.privhelper.policy"), ct);
        await ExtractResourceAsync("pro.auracore.privhelper.service",
            Path.Combine(stageDir, "pro.auracore.privhelper.service"), ct);

        var stagedBinaryPath = Path.Combine(stageDir, "privhelper");
        File.Copy(binaryPath, stagedBinaryPath, overwrite: true);

        // Best-effort chmod +x on install.sh and the binary. No-op on Windows.
        TrySetExecutable(Path.Combine(stageDir, "install.sh"));
        TrySetExecutable(stagedBinaryPath);

        return stageDir;
    }

    private static async Task ExtractResourceAsync(string resourceName, string destPath, CancellationToken ct)
    {
        var asm = typeof(PrivHelperInstaller).Assembly;
        var fullName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found on assembly {asm.FullName}. " +
                $"Available: {string.Join(", ", asm.GetManifestResourceNames())}");
        using var resourceStream = asm.GetManifestResourceStream(fullName)!;
        using var fileStream = File.Create(destPath);
        await resourceStream.CopyToAsync(fileStream, ct);
    }

    private static void TrySetExecutable(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;   // chmod unavailable on Windows

        try
        {
            // Use File.SetUnixFileMode (NET 7+)
            var mode = File.GetUnixFileMode(path)
                     | UnixFileMode.UserExecute
                     | UnixFileMode.GroupExecute
                     | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(path, mode);
        }
        catch
        {
            // Non-fatal — install.sh can be bash-invoked without +x bit since we call `pkexec bash <script>`.
        }
    }
}
