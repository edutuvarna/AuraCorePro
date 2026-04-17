using AuraCore.PrivHelper.MacOS.Interop;
using Microsoft.Extensions.Logging;

namespace AuraCore.PrivHelper.MacOS;

/// <summary>
/// Real implementation of <see cref="IActionHandler"/> that routes incoming XPC
/// requests through <see cref="ActionWhitelist"/> and — on acceptance — spawns
/// the privileged process via <see cref="IProcessInvoker"/>.
/// <para>
/// Replaces <see cref="StubActionHandler"/> per Task 27. Wired in
/// <c>Program.cs</c> in place of the stub.
/// </para>
/// <para>
/// <b>Reject path:</b> if <see cref="ActionWhitelist.Dispatch"/> returns a
/// rejected resolution, returns <see cref="ActionHandlerResult"/> with
/// <c>ExitCode=-100</c> and <c>AuthState="rejected"</c> immediately, without
/// invoking <see cref="IProcessInvoker"/>.
/// </para>
/// <para>
/// <b>Accept path:</b> invokes <see cref="IProcessInvoker.InvokeAsync"/> with
/// the whitelist's authoritative executable + argv. Maps the result to
/// <see cref="ActionHandlerResult"/> with <c>AuthState="cached"</c> (macOS
/// helpers are always-authorized post-registration — spec §3.5).
/// </para>
/// <para>
/// <b>Timeout clamping:</b> the XPC request's <c>timeout_seconds</c> is clamped
/// to [1, <see cref="HelperRuntimeOptions.MaxAllowedTimeoutSeconds"/>] before
/// being passed to <see cref="IProcessInvoker.InvokeAsync"/>.
/// </para>
/// </summary>
internal sealed class ActionWhitelistHandler : IActionHandler
{
    private readonly ActionWhitelist _whitelist;
    private readonly IProcessInvoker _invoker;
    private readonly IAuditLogger    _audit;
    private readonly ILogger<ActionWhitelistHandler> _logger;

    public ActionWhitelistHandler(
        ActionWhitelist whitelist,
        IProcessInvoker invoker,
        IAuditLogger    audit,
        ILogger<ActionWhitelistHandler> logger)
    {
        _whitelist = whitelist;
        _invoker   = invoker;
        _audit     = audit;
        _logger    = logger;
    }

    /// <inheritdoc />
    public ActionHandlerResult Invoke(XpcRequest request, PeerIdentity peer)
    {
        // --- Whitelist check ---
        var resolution = _whitelist.Dispatch(request.ActionId, request.Arguments);

        if (resolution.IsRejected)
        {
            _logger.LogWarning(
                "[handler] action rejected action={Action} pid={Pid} reason={Reason}",
                request.ActionId, peer.Pid, resolution.RejectionReason);
            return new ActionHandlerResult(-100, string.Empty, resolution.RejectionReason, "rejected");
        }

        // --- Spawn process (synchronous via GetAwaiter to match IActionHandler sync signature) ---
        var timeoutSeconds = (int)Math.Clamp(
            request.TimeoutSeconds,
            1L,
            HelperRuntimeOptions.MaxAllowedTimeoutSeconds);

        _logger.LogDebug(
            "[handler] dispatching action={Action} exe={Exe} timeout={Timeout}s pid={Pid}",
            request.ActionId, resolution.Executable, timeoutSeconds, peer.Pid);

        var invokerResult = _invoker.InvokeAsync(
            resolution.Executable,
            resolution.Argv,
            timeoutSeconds)
            .GetAwaiter().GetResult();

        return new ActionHandlerResult(
            ExitCode:  invokerResult.ExitCode,
            Stdout:    invokerResult.Stdout  ?? string.Empty,
            Stderr:    invokerResult.Stderr  ?? string.Empty,
            AuthState: "cached");
    }
}
