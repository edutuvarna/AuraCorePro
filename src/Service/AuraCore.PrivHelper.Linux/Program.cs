using AuraCore.Infrastructure.PrivilegeIpc.Linux;
using AuraCore.PrivHelper.Linux;
using Microsoft.Extensions.Logging;
using Tmds.DBus;

// Top-level statements — the daemon entry point.
// Task 15 ships the SCAFFOLD only: parse --version flag, print version, exit.
// Task 17 replaces this with the full D-Bus host loop.
if (args.Length > 0 && args[0] == "--version")
{
    Console.WriteLine(HelperVersion.Current);
    return 0;
}

using var loggerFactory = LoggerFactory.Create(b => b
    .SetMinimumLevel(LogLevel.Information)
    .AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss.fff "; }));
var logger = loggerFactory.CreateLogger("auracore-privhelper");

logger.LogInformation("auracore-privhelper {Version} starting (scaffold — no D-Bus host yet; Task 17 wires service)", HelperVersion.Current);
logger.LogInformation("BusName={BusName} ObjectPath={ObjectPath} IdleExit={IdleExitSeconds}s",
    HelperRuntimeOptions.BusName, HelperRuntimeOptions.ObjectPath, HelperRuntimeOptions.IdleExitTimeoutSeconds);
logger.LogWarning("This is a scaffold build. The daemon does not yet register on D-Bus. Exiting after 1s so unit runs don't hang.");
await Task.Delay(1000);
logger.LogInformation("auracore-privhelper exiting");
return 0;

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
