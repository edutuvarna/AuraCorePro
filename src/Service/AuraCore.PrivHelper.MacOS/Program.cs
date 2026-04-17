using AuraCore.PrivHelper.MacOS;
using Microsoft.Extensions.Logging;

// Top-level statements — daemon entry point.
// Task 25 ships the SCAFFOLD only (--version flag + exit). Task 26 wires
// the real NSXPCListener / xpc_main service loop.
if (args.Length > 0 && args[0] == "--version")
{
    Console.WriteLine(HelperVersion.Current);
    return 0;
}

using var loggerFactory = LoggerFactory.Create(b => b
    .SetMinimumLevel(LogLevel.Information)
    .AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss.fff "; }));
var logger = loggerFactory.CreateLogger("pro.auracore.privhelper");

logger.LogInformation("pro.auracore.privhelper {Version} starting (scaffold — Task 26 wires XPC listener)",
    HelperVersion.Current);
logger.LogInformation("MachServiceName={Name} IdleExit={Idle}s MinOS=macOS {OS}+",
    HelperRuntimeOptions.MachServiceName,
    HelperRuntimeOptions.IdleExitTimeoutSeconds,
    HelperRuntimeOptions.MinimumMacOSVersion);
logger.LogWarning("Scaffold build — exits after 1s so CI doesn't hang.");
await Task.Delay(1000);
return 0;
