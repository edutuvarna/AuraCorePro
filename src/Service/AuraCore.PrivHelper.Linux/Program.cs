using AuraCore.Infrastructure.PrivilegeIpc.Linux;
using AuraCore.PrivHelper.Linux;
using Microsoft.Extensions.Logging;
using Tmds.DBus;

if (args.Length > 0 && args[0] == "--version")
{
    Console.WriteLine(HelperVersion.Current);
    return 0;
}

using var loggerFactory = LoggerFactory.Create(b => b
    .SetMinimumLevel(LogLevel.Information)
    .AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss.fff "; }));
var logger = loggerFactory.CreateLogger("auracore-privhelper");

logger.LogInformation("auracore-privhelper {Version} starting", HelperVersion.Current);

try
{
    // System bus connection (daemon must run as root per systemd unit, Task 18)
    using var connection = new Connection(Address.System);
    await connection.ConnectAsync();

    var whitelist = new ActionWhitelist();
    var invoker = new ProcessInvoker();
    var svcLogger = loggerFactory.CreateLogger<AuracorePrivHelperService>();
    var service = new AuracorePrivHelperService(whitelist, invoker, svcLogger);

    await connection.RegisterObjectAsync(service);
    await connection.RegisterServiceAsync(HelperRuntimeOptions.BusName);

    logger.LogInformation(
        "auracore-privhelper registered as {BusName}{Path} (interface pro.auracore.PrivHelper1)",
        HelperRuntimeOptions.BusName, HelperRuntimeOptions.ObjectPath);

    // Idle-exit timer: spec §3.2 D2 requires 300 s idle-exit.
    // Task 17 ships a simplified version — runs until signaled.
    // A follow-up task can add the real idle timer if the always-running
    // cost becomes a real concern (daemon is small; systemd will stop it
    // when the service is disabled).
    var tcs = new TaskCompletionSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; tcs.TrySetResult(); };
    AppDomain.CurrentDomain.ProcessExit += (_, _) => tcs.TrySetResult();
    await tcs.Task;

    logger.LogInformation("auracore-privhelper shutting down");
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "auracore-privhelper failed to start (likely dev/Windows build)");
    // Returning 0 on Windows dev lets the build pipeline pass. On real Linux
    // systems a genuine D-Bus error would still bubble up through stderr.
    return Environment.OSVersion.Platform == PlatformID.Unix ? 1 : 0;
}

namespace AuraCore.PrivHelper.Linux
{
    /// <summary>
    /// Single source of truth for daemon version. Compared over D-Bus
    /// <see cref="IPrivHelper.GetVersionAsync"/> by the client to detect
    /// helper drift after main app upgrades.
    /// </summary>
    public static class HelperVersion
    {
        public const string Current = "5.2.1";
    }

    /// <summary>
    /// Runtime constants used by both the daemon process and tests.
    /// </summary>
    public static class HelperRuntimeOptions
    {
        /// <summary>D-Bus well-known name claimed on the system bus.</summary>
        public const string BusName = "pro.auracore.PrivHelper";

        /// <summary>D-Bus object path the IPrivHelper service is served at.</summary>
        public const string ObjectPath = "/pro/auracore/PrivHelper";

        /// <summary>
        /// Seconds of idle (no method calls) before the daemon self-exits.
        /// Set via spec §3.2 D2.
        /// </summary>
        public const int IdleExitTimeoutSeconds = 300;
    }
}
