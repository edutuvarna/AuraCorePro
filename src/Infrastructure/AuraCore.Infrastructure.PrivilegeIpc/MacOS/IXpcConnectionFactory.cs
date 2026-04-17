using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace AuraCore.Infrastructure.PrivilegeIpc.MacOS;

/// <summary>
/// Represents an active XPC connection to the pro.auracore.PrivHelper daemon.
/// </summary>
public interface IXpcConnection : IDisposable
{
    /// <summary>
    /// Sends a single XPC request and waits synchronously for the reply.
    /// Returns null if the wire protocol failed (connection broken, malformed reply).
    /// </summary>
    Task<XpcClientReply?> SendRequestAsync(XpcClientRequest request, CancellationToken ct = default);
}

/// <summary>Wire-format request sent over XPC to the daemon.</summary>
public sealed record XpcClientRequest(string ActionId, string[] Arguments, long TimeoutSeconds);

/// <summary>Wire-format reply received from the daemon over XPC.</summary>
public sealed record XpcClientReply(long ExitCode, string Stdout, string Stderr, string AuthState);

/// <summary>
/// Creates XPC connections to pro.auracore.PrivHelper on the Mach system service.
/// </summary>
public interface IXpcConnectionFactory
{
    /// <summary>
    /// Creates an XPC connection to pro.auracore.PrivHelper on the Mach
    /// system service. Returns null if the service can't be reached
    /// (helper not installed, not registered with SMAppService, etc.).
    /// </summary>
    Task<IXpcConnection?> TryConnectAsync(CancellationToken ct = default);
}

/// <summary>
/// Production implementation that uses libxpc P/Invoke to connect to the
/// pro.auracore.PrivHelper Mach service. Probes the connection with a
/// "__ping__" action to confirm the helper is alive before returning.
/// </summary>
public sealed class DefaultXpcConnectionFactory : IXpcConnectionFactory
{
    private const string MachServiceName = "pro.auracore.PrivHelper";
    private readonly ILogger<DefaultXpcConnectionFactory> _logger;

    public DefaultXpcConnectionFactory(ILogger<DefaultXpcConnectionFactory> logger)
        => _logger = logger;

    public Task<IXpcConnection?> TryConnectAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsMacOS())
        {
            _logger.LogDebug("[xpc-client] skipping XPC connect on non-macOS host");
            return Task.FromResult<IXpcConnection?>(null);
        }

        try
        {
            var conn = Interop.LibXpcClient.xpc_connection_create_mach_service(
                MachServiceName, IntPtr.Zero, Interop.LibXpcClient.XpcConnectionClientFlag);
            if (conn == IntPtr.Zero)
            {
                _logger.LogWarning("[xpc-client] xpc_connection_create_mach_service returned NULL");
                return Task.FromResult<IXpcConnection?>(null);
            }

            Interop.LibXpcClient.xpc_connection_resume(conn);

            // Probe — a malformed / unknown-action request exercises the full
            // round-trip. If the helper is alive it replies with "rejected";
            // if dead we get null. Either way we know more than just "handle was non-NULL".
            var probe = DefaultXpcConnection.SendProbe(conn);
            if (!probe)
            {
                _logger.LogWarning("[xpc-client] probe failed — helper not reachable");
                Interop.LibXpcClient.xpc_connection_cancel(conn);
                Interop.LibXpcClient.xpc_release(conn);
                return Task.FromResult<IXpcConnection?>(null);
            }

            return Task.FromResult<IXpcConnection?>(new DefaultXpcConnection(conn, _logger));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[xpc-client] connection setup failed");
            return Task.FromResult<IXpcConnection?>(null);
        }
    }
}

/// <summary>
/// Wraps a native XPC connection handle and provides typed send/receive.
/// </summary>
internal sealed class DefaultXpcConnection : IXpcConnection
{
    private readonly IntPtr _connection;
    private readonly ILogger _logger;
    private bool _disposed;

    public DefaultXpcConnection(IntPtr connection, ILogger logger)
    {
        _connection = connection;
        _logger = logger;
    }

    /// <summary>
    /// Sends a probe request (action_id="__ping__") to determine if the helper
    /// is alive. Daemon rejects unknown actions but will still send a reply
    /// dictionary, confirming the connection is live.
    /// </summary>
    internal static bool SendProbe(IntPtr conn)
    {
        var req = BuildRequest(new XpcClientRequest("__ping__", Array.Empty<string>(), 1));
        try
        {
            var reply = Interop.LibXpcClient.xpc_connection_send_message_with_reply_sync(conn, req);
            var alive = reply != IntPtr.Zero;
            if (reply != IntPtr.Zero) Interop.LibXpcClient.xpc_release(reply);
            return alive;
        }
        finally
        {
            Interop.LibXpcClient.xpc_release(req);
        }
    }

    public async Task<XpcClientReply?> SendRequestAsync(XpcClientRequest request, CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DefaultXpcConnection));

        return await Task.Run(() =>
        {
            var req = BuildRequest(request);
            try
            {
                var reply = Interop.LibXpcClient.xpc_connection_send_message_with_reply_sync(_connection, req);
                if (reply == IntPtr.Zero) return null;
                try
                {
                    return ReadReply(reply);
                }
                finally
                {
                    Interop.LibXpcClient.xpc_release(reply);
                }
            }
            finally
            {
                Interop.LibXpcClient.xpc_release(req);
            }
        }, ct);
    }

    private static IntPtr BuildRequest(XpcClientRequest req)
    {
        var dict = Interop.LibXpcClient.xpc_dictionary_create(IntPtr.Zero, IntPtr.Zero, 0);
        Interop.LibXpcClient.xpc_dictionary_set_string(dict, "action_id", req.ActionId);
        Interop.LibXpcClient.xpc_dictionary_set_int64(dict, "timeout_seconds", req.TimeoutSeconds);

        var argsArr = Interop.LibXpcClient.xpc_array_create(IntPtr.Zero, 0);
        foreach (var a in req.Arguments)
        {
            var s = Interop.LibXpcClient.xpc_string_create(a);
            Interop.LibXpcClient.xpc_array_append_value(argsArr, s);
            Interop.LibXpcClient.xpc_release(s);
        }
        Interop.LibXpcClient.xpc_dictionary_set_value(dict, "args", argsArr);
        Interop.LibXpcClient.xpc_release(argsArr);
        return dict;
    }

    private static XpcClientReply ReadReply(IntPtr reply)
    {
        var exitCode = Interop.LibXpcClient.xpc_dictionary_get_int64(reply, "exit_code");
        var stdout = ReadUtf8(Interop.LibXpcClient.xpc_dictionary_get_string(reply, "stdout"));
        var stderr = ReadUtf8(Interop.LibXpcClient.xpc_dictionary_get_string(reply, "stderr"));
        var auth   = ReadUtf8(Interop.LibXpcClient.xpc_dictionary_get_string(reply, "auth_state"));
        return new XpcClientReply(exitCode, stdout, stderr, auth);
    }

    private static string ReadUtf8(IntPtr cstr)
    {
        if (cstr == IntPtr.Zero) return string.Empty;
        return Marshal.PtrToStringUTF8(cstr) ?? string.Empty;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { Interop.LibXpcClient.xpc_connection_cancel(_connection); } catch { /* best-effort */ }
        try { Interop.LibXpcClient.xpc_release(_connection); } catch { /* best-effort */ }
    }
}
