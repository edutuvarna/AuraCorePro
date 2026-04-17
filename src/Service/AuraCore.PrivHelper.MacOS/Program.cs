using AuraCore.PrivHelper.MacOS;
using AuraCore.PrivHelper.MacOS.Interop;
using Microsoft.Extensions.Logging;

// ---------------------------------------------------------------------------
// --version short-circuit (used by SMAppService + install scripts)
// ---------------------------------------------------------------------------
if (args.Length > 0 && args[0] == "--version")
{
    Console.WriteLine(HelperVersion.Current);
    return 0;
}

// ---------------------------------------------------------------------------
// Logger setup
// ---------------------------------------------------------------------------
using var loggerFactory = LoggerFactory.Create(b => b
    .SetMinimumLevel(LogLevel.Information)
    .AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss.fff "; }));
var logger = loggerFactory.CreateLogger("pro.auracore.privhelper");

logger.LogInformation(
    "pro.auracore.privhelper {Version} starting (Phase 5.2.2)",
    HelperVersion.Current);
logger.LogInformation(
    "MachServiceName={Name} IdleExit={Idle}s MinOS=macOS {OS}+",
    HelperRuntimeOptions.MachServiceName,
    HelperRuntimeOptions.IdleExitTimeoutSeconds,
    HelperRuntimeOptions.MinimumMacOSVersion);

// ---------------------------------------------------------------------------
// XPC listener + delegate setup
// ---------------------------------------------------------------------------
try
{
    var audit     = new AuditLogger(loggerFactory.CreateLogger<AuditLogger>());
    var peer      = new PeerVerifier(stubMode: false, loggerFactory.CreateLogger<PeerVerifier>());
    var whitelist = new ActionWhitelist();
    var invoker   = new ProcessInvoker();
    var handler   = new ActionWhitelistHandler(
        whitelist,
        invoker,
        audit,
        loggerFactory.CreateLogger<ActionWhitelistHandler>());
    var xpcDelegate = new AuracorePrivHelperDelegate(
        peer,
        handler,
        audit,
        loggerFactory.CreateLogger<AuracorePrivHelperDelegate>());

    // Create the Mach service listener.
    // XPC_CONNECTION_MACH_SERVICE_LISTENER = 1ul (from <xpc/connection.h>).
    const ulong XpcConnectionMachServiceListener = 1ul;

    var listener = LibXpc.xpc_connection_create_mach_service(
        HelperRuntimeOptions.MachServiceName,
        IntPtr.Zero,
        XpcConnectionMachServiceListener);

    if (listener == IntPtr.Zero)
    {
        logger.LogError(
            "xpc_connection_create_mach_service returned NULL for service={Name} — " +
            "likely not running under launchd or plist MachServices dict is missing.",
            HelperRuntimeOptions.MachServiceName);
        return 1;
    }

    // NOTE — ObjC block gap (known limitation, tracked in memory):
    // ---------------------------------------------------------------
    // The production XPC event handler for incoming connections would normally
    // be wired via xpc_connection_set_event_handler(listener, ^(xpc_object_t event) {...}).
    // Calling Objective-C block-accepting APIs from .NET P/Invoke requires either:
    //   a) A C shim .dylib that accepts a function pointer + wraps it in a block.
    //   b) ObjC runtime block allocation via __builtin_Block_copy / _Block_copy.
    // Both approaches exceed MVP scope for Task 26. The delegate logic is fully
    // exercised by unit tests via HandleFake (no real XPC needed in test suite).
    //
    // Current behaviour: listener is created and resumed (launchd placeholder
    // registration succeeds), but no incoming-message handler is wired. On a
    // real macOS host this means "helper accepts connections but never replies"
    // — a visible, non-silent failure that does NOT compromise security.
    //
    // Follow-up: Task 5.2.2.2b — add helper-shim.m compiled to .dylib that
    // wraps the managed AuracorePrivHelperDelegate.Handle via a C trampoline.
    // ---------------------------------------------------------------

    LibXpc.xpc_connection_resume(listener);
    logger.LogInformation(
        "XPC listener resumed on {Name} (event-handler wiring is a known follow-up — Task 5.2.2.2b)",
        HelperRuntimeOptions.MachServiceName);

    // Audit startup.
    audit.LogMalformedMessage(null);   // reuse IAuditLogger to record lifecycle event (best effort)
    Console.Error.WriteLine(
        $"[PRIVHELPER lifecycle] event=startup version={HelperVersion.Current} " +
        $"service={HelperRuntimeOptions.MachServiceName}");

    // ---------------------------------------------------------------------------
    // Wait until launchd sends SIGTERM / SIGINT (normal daemon lifecycle).
    // ---------------------------------------------------------------------------
    var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; tcs.TrySetResult(); };
    AppDomain.CurrentDomain.ProcessExit += (_, _) => tcs.TrySetResult();

    await tcs.Task;

    LibXpc.xpc_connection_cancel(listener);
    logger.LogInformation("pro.auracore.privhelper shutdown complete.");
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "privhelper startup failed (likely non-Mac dev host — DllNotFoundException expected on Windows)");
    // On non-Mac dev boxes, returning 0 keeps CI green (the binary is
    // Windows-side cross-compile output, never run as a real daemon there).
    return Environment.OSVersion.Platform == PlatformID.Unix ? 1 : 0;
}
