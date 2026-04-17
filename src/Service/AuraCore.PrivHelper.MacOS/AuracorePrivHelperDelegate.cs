using AuraCore.PrivHelper.MacOS.Interop;
using Microsoft.Extensions.Logging;

namespace AuraCore.PrivHelper.MacOS;

// ---------------------------------------------------------------------------
// IActionHandler contract + ActionHandlerResult
// ---------------------------------------------------------------------------

/// <summary>Outcome record returned by <see cref="IActionHandler.Invoke"/>.</summary>
internal sealed record ActionHandlerResult(long ExitCode, string Stdout, string Stderr, string AuthState);

/// <summary>
/// Invoked by <see cref="AuracorePrivHelperDelegate"/> ONLY AFTER peer
/// verification passes. Task 27 replaces <see cref="StubActionHandler"/> with
/// the real <c>ActionWhitelist</c>-backed implementation.
/// </summary>
internal interface IActionHandler
{
    /// <summary>Execute the privileged action and return the result.</summary>
    ActionHandlerResult Invoke(XpcRequest request, PeerIdentity peer);
}

// ---------------------------------------------------------------------------
// StubActionHandler — Task 27 pending
// ---------------------------------------------------------------------------

/// <summary>
/// Placeholder implementation of <see cref="IActionHandler"/> shipped with
/// Task 26. Always returns exit=-255 with <c>auth_state="rejected"</c> and a
/// stderr message indicating that the real <c>ActionWhitelist</c> has not been
/// wired yet. Task 27 replaces this with the real dispatch implementation.
/// </summary>
internal sealed class StubActionHandler : IActionHandler
{
    /// <inheritdoc />
    public ActionHandlerResult Invoke(XpcRequest request, PeerIdentity peer) =>
        new(-255L, string.Empty,
            "ActionWhitelist not wired until Task 27",
            "rejected");
}

// ---------------------------------------------------------------------------
// AuracorePrivHelperDelegate — 8-step dispatch flow per spec §3.3
// ---------------------------------------------------------------------------

/// <summary>
/// Dispatches a single incoming XPC message through the full security pipeline:
/// peer verification → payload decode → action handler → audit → reply.
/// <para>
/// <b>Dispatch flow (spec §3.3):</b>
/// <list type="number">
///   <item>Get peer PID from XPC connection.</item>
///   <item>Verify peer identity via <see cref="IPeerVerifier"/>.</item>
///   <item>If verification fails: audit + return exit=-102 reply.</item>
///   <item>Decode <c>xpc_dictionary</c> payload into <see cref="XpcRequest"/>.</item>
///   <item>If decode fails (malformed): audit + return exit=-101 reply.</item>
///   <item>Invoke <see cref="IActionHandler"/>.</item>
///   <item>Audit the action.</item>
///   <item>Return <see cref="XpcReply"/>.</item>
/// </list>
/// </para>
/// <para>
/// <b>Test-only path:</b> <see cref="HandleFake"/> accepts an already-decoded
/// <see cref="XpcRequest"/> + peer PID directly, bypassing the XPC pointer
/// dereference. This lets unit tests drive all 8 dispatch steps with
/// NSubstitute mocks without a real XPC connection.
/// </para>
/// </summary>
internal sealed class AuracorePrivHelperDelegate
{
    private readonly IPeerVerifier _peerVerifier;
    private readonly IActionHandler _actionHandler;
    private readonly IAuditLogger _audit;
    private readonly ILogger<AuracorePrivHelperDelegate> _logger;

    public AuracorePrivHelperDelegate(
        IPeerVerifier peerVerifier,
        IActionHandler actionHandler,
        IAuditLogger audit,
        ILogger<AuracorePrivHelperDelegate> logger)
    {
        _peerVerifier  = peerVerifier;
        _actionHandler = actionHandler;
        _audit         = audit;
        _logger        = logger;
    }

    // -----------------------------------------------------------------------
    // Production entry point — called by the xpc_connection event handler
    // -----------------------------------------------------------------------

    /// <summary>
    /// Handle one incoming XPC message. Returns the <see cref="XpcReply"/>
    /// (caller serializes it back as an <c>xpc_dictionary</c> and sends the reply).
    /// </summary>
    public XpcReply Handle(IntPtr connection, IntPtr incomingMessage)
    {
        // Step 1: get peer PID from XPC connection.
        int peerPid;
        try
        {
            peerPid = LibXpc.xpc_connection_get_pid(connection);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Non-Mac dev host: libxpc not available — treat as unknown peer.
            _logger.LogWarning("[delegate] xpc_connection_get_pid failed (non-Mac host): {Msg}", ex.Message);
            return new XpcReply(-102, string.Empty, "xpc_connection_get_pid unavailable", "rejected");
        }

        // Step 2: decode inbound payload.
        XpcRequest? request = null;
        try
        {
            request = XpcMessageCodec.DecodeXpcDictionary(incomingMessage);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Non-Mac dev host: fall through with null request (handled at step 5).
            _logger.LogDebug("[delegate] XpcMessageCodec.DecodeXpcDictionary failed (non-Mac): {Msg}", ex.Message);
        }

        return DispatchCore(peerPid, request);
    }

    // -----------------------------------------------------------------------
    // Test-only entry point (internal, not public API)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Test-visible overload that bypasses real XPC pointer dereference.
    /// Runs the same 8-step dispatch flow starting from already-decoded values.
    /// Declared <c>internal</c>; accessible to the test project via
    /// <c>InternalsVisibleTo("AuraCore.Tests.Platform")</c> in AssemblyInfo.cs.
    /// </summary>
    internal XpcReply HandleFake(int peerPid, XpcRequest? request)
        => DispatchCore(peerPid, request);

    // -----------------------------------------------------------------------
    // Core 8-step dispatch (shared by production + test paths)
    // -----------------------------------------------------------------------

    private XpcReply DispatchCore(int peerPid, XpcRequest? decodedRequest)
    {
        // Step 2: verify peer identity.
        PeerVerificationResult verify;
        try
        {
            verify = _peerVerifier.Verify(peerPid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[delegate] PeerVerifier.Verify threw unexpectedly for pid={Pid}", peerPid);
            _audit.LogPeerRejection(peerPid, $"verifier exception: {ex.Message}");
            return new XpcReply(-102, string.Empty, "peer verification error", "rejected");
        }

        // Step 3: reject if peer verification failed.
        if (!verify.Ok)
        {
            // PRIVACY: do NOT log argv here — peer is untrusted.
            _audit.LogPeerRejection(peerPid, verify.RejectionReason ?? "unknown rejection reason");
            _logger.LogWarning(
                "[delegate] peer rejected pid={Pid} reason={Reason}",
                peerPid, verify.RejectionReason);
            return new XpcReply(-102, string.Empty, verify.RejectionReason ?? "peer verification failed", "rejected");
        }

        var peer = verify.Identity!.Value;

        // Step 5: reject if message was malformed (decode returned null).
        if (decodedRequest is null)
        {
            _audit.LogMalformedMessage(peer);
            _logger.LogWarning("[delegate] malformed XPC message from pid={Pid}", peer.Pid);
            return new XpcReply(-101, string.Empty, "malformed XPC request", "rejected");
        }

        // Step 6: invoke action handler (ONLY after peer is verified).
        ActionHandlerResult result;
        try
        {
            result = _actionHandler.Invoke(decodedRequest, peer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[delegate] IActionHandler.Invoke threw for action={Action} pid={Pid}",
                decodedRequest.ActionId, peer.Pid);
            return new XpcReply(-200, string.Empty, $"action handler error: {ex.Message}", "rejected");
        }

        // Step 7: audit the dispatched action.
        _audit.LogAction(peer, decodedRequest, result);

        // Step 8: return reply.
        return new XpcReply(result.ExitCode, result.Stdout, result.Stderr, result.AuthState);
    }
}
