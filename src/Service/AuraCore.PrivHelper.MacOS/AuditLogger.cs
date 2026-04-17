using AuraCore.PrivHelper.MacOS.Interop;
using Microsoft.Extensions.Logging;

namespace AuraCore.PrivHelper.MacOS;

// ---------------------------------------------------------------------------
// IAuditLogger contract
// ---------------------------------------------------------------------------

/// <summary>
/// Structured audit log contract for the macOS privilege helper.
/// <para>
/// Privacy requirement: <see cref="LogPeerRejection"/> MUST NOT log argv — the
/// caller's arguments may contain sensitive file paths. Only the peer PID and
/// rejection reason are recorded.
/// </para>
/// </summary>
internal interface IAuditLogger
{
    /// <summary>
    /// Records that an XPC peer failed identity verification.
    /// MUST NOT include argv (privacy — peer not yet trusted).
    /// </summary>
    void LogPeerRejection(int peerPid, string reason);

    /// <summary>
    /// Records that an incoming XPC message could not be decoded.
    /// Called AFTER peer identity is confirmed, so peer identity IS recorded.
    /// Argv is still not logged (decode failed — no validated argv exists).
    /// </summary>
    void LogMalformedMessage(PeerIdentity? peer);

    /// <summary>
    /// Records a successfully dispatched privileged action, including outcome.
    /// </summary>
    void LogAction(PeerIdentity peer, XpcRequest request, ActionHandlerResult result);
}

// ---------------------------------------------------------------------------
// AuditLogger implementation
// ---------------------------------------------------------------------------

/// <summary>
/// Production implementation of <see cref="IAuditLogger"/>.
/// <para>
/// Primary channel: <c>os_log</c> via <see cref="OsLog"/> P/Invoke, subsystem
/// <c>pro.auracore.privhelper</c>, category <c>audit</c>. Inspect with:
/// <code>log show --predicate 'subsystem == "pro.auracore.privhelper"'</code>
/// </para>
/// <para>
/// Fallback channel (when os_log P/Invoke fails — e.g. Windows dev host):
/// <c>Console.Error.WriteLine</c> with a <c>[PRIVHELPER audit]</c> prefix.
/// When launchd is managing the process, <c>StandardErrorPath</c> in the plist
/// (Task 28) captures stderr to a rotating log file.
/// </para>
/// </summary>
internal sealed class AuditLogger : IAuditLogger
{
    private const string Subsystem = "pro.auracore.privhelper";
    private const string Category  = "audit";
    private const string Prefix    = "[PRIVHELPER audit]";

    private readonly IntPtr _osLogHandle;
    private readonly bool   _osLogAvailable;
    private readonly ILogger<AuditLogger>? _logger;

    public AuditLogger(ILogger<AuditLogger>? logger = null)
    {
        _logger = logger;
        try
        {
            _osLogHandle   = OsLog.os_log_create(Subsystem, Category);
            _osLogAvailable = _osLogHandle != IntPtr.Zero;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            _osLogAvailable = false;
            _logger?.LogDebug("[AuditLogger] os_log unavailable on this host — using stderr fallback");
        }
    }

    // -----------------------------------------------------------------------
    // IAuditLogger
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    /// <remarks>PRIVACY: argv is intentionally NOT logged here.</remarks>
    public void LogPeerRejection(int peerPid, string reason)
    {
        var msg = $"{Prefix} event=peer_rejection caller_pid={peerPid} reason=\"{reason}\"";
        Write(msg, OsLog.OS_LOG_TYPE_ERROR);
    }

    /// <inheritdoc />
    public void LogMalformedMessage(PeerIdentity? peer)
    {
        var pidPart  = peer.HasValue ? $"caller_pid={peer.Value.Pid}" : "caller_pid=unknown";
        var teamPart = peer.HasValue ? $"caller_team={peer.Value.TeamId}" : string.Empty;
        var msg = $"{Prefix} event=malformed_message {pidPart} {teamPart}".TrimEnd();
        Write(msg, OsLog.OS_LOG_TYPE_ERROR);
    }

    /// <inheritdoc />
    public void LogAction(PeerIdentity peer, XpcRequest request, ActionHandlerResult result)
    {
        // argv IS logged here — peer has been verified so this is an authorised call.
        var argv = string.Join(' ', request.Arguments);
        var msg = $"{Prefix} event=action_dispatched " +
                  $"action={request.ActionId} " +
                  $"caller_pid={peer.Pid} " +
                  $"caller_team={peer.TeamId} " +
                  $"argv=\"{argv}\" " +
                  $"exit={result.ExitCode} " +
                  $"auth={result.AuthState}";
        Write(msg, OsLog.OS_LOG_TYPE_DEFAULT);
    }

    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    private void Write(string message, byte osLogType)
    {
        if (_osLogAvailable)
        {
            try
            {
                OsLog.os_log_impl(
                    dso:    IntPtr.Zero,
                    log:    _osLogHandle,
                    type:   osLogType,
                    format: message,
                    buf:    IntPtr.Zero,
                    size:   0);
                return;
            }
            catch
            {
                // Swallow — fall through to stderr
            }
        }

        // Stderr fallback — captured by launchd's StandardErrorPath plist key.
        Console.Error.WriteLine(message);
    }
}
