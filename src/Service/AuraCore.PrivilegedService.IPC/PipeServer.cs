using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.IPC.Contracts;
using AuraCore.PrivilegedService.Ops;
using Microsoft.Extensions.Logging;

namespace AuraCore.PrivilegedService.IPC;

public sealed class PipeServer
{
    private readonly string _pipeName;
    private readonly DriverOperations _driverOps;
    private readonly DefenderOperations _defenderOps;
    private readonly ServiceOperations _serviceOps;
    private readonly ILogger<PipeServer> _logger;

    private const int MaxRequestBytes = 64 * 1024; // 64 KB hard cap

    public PipeServer(
        string pipeName,
        DriverOperations driverOps,
        DefenderOperations defenderOps,
        ServiceOperations serviceOps,
        ILogger<PipeServer> logger)
    {
        _pipeName    = pipeName    ?? throw new ArgumentNullException(nameof(pipeName));
        _driverOps   = driverOps   ?? throw new ArgumentNullException(nameof(driverOps));
        _defenderOps = defenderOps ?? throw new ArgumentNullException(nameof(defenderOps));
        _serviceOps  = serviceOps  ?? throw new ArgumentNullException(nameof(serviceOps));
        _logger      = logger      ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Accept-loop: one new server stream per connection, handler runs on thread-pool.
    /// Cancellation causes a clean exit.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("PipeServer starting on pipe '{PipeName}'", _pipeName);

        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream stream;
            try
            {
                stream = CreateServerStream();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create named pipe server stream — retrying in 500 ms");
                try { await Task.Delay(500, ct); } catch (OperationCanceledException) { break; }
                continue;
            }

            try
            {
                await stream.WaitForConnectionAsync(ct);
                // Fire-and-forget per connection; capture stream by reference in the closure.
                _ = Task.Run(() => HandleConnectionAsync(stream, ct), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                await stream.DisposeAsync();
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pipe accept failed");
                await stream.DisposeAsync();
            }
        }

        _logger.LogInformation("PipeServer stopped");
    }

    // ---------------------------------------------------------------------------
    // Pipe creation
    // ---------------------------------------------------------------------------

    private NamedPipeServerStream CreateServerStream()
    {
        if (OperatingSystem.IsWindows())
        {
            var security = new PipeSecurity();

            // Allow Authenticated Users (not Everyone) to connect and create instances.
            var authenticatedUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
            security.AddAccessRule(new PipeAccessRule(
                authenticatedUsers,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

            // The service process owner (SYSTEM / elevated user) retains full control.
            var localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            security.AddAccessRule(new PipeAccessRule(
                localSystem,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

            return NamedPipeServerStreamAcl.Create(
                _pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize:  4096,
                outBufferSize: 4096,
                pipeSecurity:  security);
        }

        // Non-Windows (Linux / macOS) — no ACL support via this API; rely on filesystem perms.
        return new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    // ---------------------------------------------------------------------------
    // Per-connection handler
    // ---------------------------------------------------------------------------

    private async Task HandleConnectionAsync(NamedPipeServerStream stream, CancellationToken ct)
    {
        await using (stream)
        {
            try
            {
                // --- Read length-prefix (4 bytes, little-endian) ---
                var lengthBuf = new byte[4];
                await stream.ReadExactlyAsync(lengthBuf, ct);
                int length = BitConverter.ToInt32(lengthBuf);

                if (length <= 0 || length > MaxRequestBytes)
                {
                    _logger.LogWarning("Rejected request with invalid length {Length}", length);
                    await WriteResponseAsync(stream,
                        new IpcErrorResponse("", false, "request size out of bounds"), ct);
                    return;
                }

                // --- Read payload ---
                var requestBuf = new byte[length];
                await stream.ReadExactlyAsync(requestBuf, ct);

                // --- Parse envelope — extract CorrelationId and (optional) ActionId ---
                using var doc = JsonDocument.Parse(requestBuf);
                var root = doc.RootElement;

                string correlationId = root.TryGetProperty("CorrelationId", out var cidElem)
                    ? (cidElem.GetString() ?? string.Empty)
                    : string.Empty;

                bool hasActionId = root.TryGetProperty("ActionId", out var actionIdElem);
                string actionId  = hasActionId ? (actionIdElem.GetString() ?? string.Empty) : string.Empty;

                // --- Ping: no ActionId (or empty) → health-check response ---
                if (!hasActionId || string.IsNullOrEmpty(actionId))
                {
                    _logger.LogInformation("Ping corr={CorrelationId}", correlationId);
                    await WriteResponseAsync(stream, new PingResponse(correlationId, true), ct);
                    return;
                }

                // --- Whitelist gate — BEFORE dispatch ---
                if (!ActionWhitelist.Windows.IsAllowed(actionId))
                {
                    _logger.LogWarning(
                        "Rejected non-whitelisted action '{ActionId}' corr={CorrelationId}",
                        actionId, correlationId);
                    await WriteResponseAsync(stream,
                        new IpcErrorResponse(correlationId, false,
                            $"action not in whitelist: {actionId}"), ct);
                    return;
                }

                // --- Dispatch ---
                _logger.LogInformation(
                    "Dispatching '{ActionId}' corr={CorrelationId}", actionId, correlationId);

                var response = await DispatchAsync(actionId, correlationId, root, ct);
                await WriteResponseAsync(stream, response, ct);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown — no log noise.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in connection handler");
                try
                {
                    await WriteResponseAsync(stream,
                        new IpcErrorResponse("", false, "internal server error"), ct);
                }
                catch
                {
                    // Best-effort — ignore write failure on error path.
                }
            }
        }
    }

    // ---------------------------------------------------------------------------
    // Dispatcher
    // ---------------------------------------------------------------------------

    private async Task<IpcResponse> DispatchAsync(
        string actionId, string correlationId, JsonElement req, CancellationToken ct)
    {
        switch (actionId)
        {
            // ── Driver Updater ───────────────────────────────────────────────
            case "driver.scan":
                return await _driverOps.ScanAsync(correlationId, ct);

            case "driver.export":
                string backupDir = req.TryGetProperty("BackupDirectory", out var bdElem)
                    ? (bdElem.GetString() ?? string.Empty)
                    : string.Empty;
                return await _driverOps.ExportAsync(correlationId, backupDir, ct);

            // ── Defender Manager ─────────────────────────────────────────────
            case "defender.update-signatures":
            case "defender.scan-quick":
            case "defender.scan-full":
            case "defender.set-realtime":
            case "defender.add-exclusion":
            case "defender.remove-exclusion":
            case "defender.remove-threat":
            {
                if (!req.TryGetProperty("Action", out var defActionElem))
                    return new IpcErrorResponse(correlationId, false, "missing Action field");

                DefenderAction defAction;
                if (defActionElem.ValueKind == JsonValueKind.Number)
                {
                    defAction = (DefenderAction)defActionElem.GetInt32();
                }
                else if (defActionElem.ValueKind == JsonValueKind.String &&
                         Enum.TryParse<DefenderAction>(defActionElem.GetString(), out var parsed))
                {
                    defAction = parsed;
                }
                else
                {
                    return new IpcErrorResponse(correlationId, false, "invalid Action value");
                }

                string? defTarget = req.TryGetProperty("Target", out var tElem)
                    ? tElem.GetString()
                    : null;

                return await _defenderOps.ExecuteAsync(correlationId, defAction, defTarget, ct);
            }

            // ── Service Manager ──────────────────────────────────────────────
            case "service.start":
            case "service.stop":
            case "service.restart":
            case "service.set-startup":
            {
                if (!req.TryGetProperty("ServiceName", out var svcNameElem))
                    return new IpcErrorResponse(correlationId, false, "missing ServiceName field");
                if (!req.TryGetProperty("Action", out var svcActionElem))
                    return new IpcErrorResponse(correlationId, false, "missing Action field");

                string svcName = svcNameElem.GetString() ?? string.Empty;

                ServiceAction svcAction;
                if (svcActionElem.ValueKind == JsonValueKind.Number)
                {
                    svcAction = (ServiceAction)svcActionElem.GetInt32();
                }
                else if (svcActionElem.ValueKind == JsonValueKind.String &&
                         Enum.TryParse<ServiceAction>(svcActionElem.GetString(), out var parsed))
                {
                    svcAction = parsed;
                }
                else
                {
                    return new IpcErrorResponse(correlationId, false, "invalid Action value");
                }

                return await _serviceOps.ExecuteAsync(correlationId, svcName, svcAction, ct);
            }

            default:
                // Should never reach here because of the whitelist gate above,
                // but be defensive.
                _logger.LogWarning("No dispatcher branch for action '{ActionId}'", actionId);
                return new IpcErrorResponse(correlationId, false,
                    $"no dispatcher for action: {actionId}");
        }
    }

    // ---------------------------------------------------------------------------
    // Wire protocol: length-prefixed (4-byte LE) JSON
    // ---------------------------------------------------------------------------

    private static async Task WriteResponseAsync(Stream stream, IpcResponse resp, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(resp, resp.GetType());
        await stream.WriteAsync(BitConverter.GetBytes(payload.Length), ct);
        await stream.WriteAsync(payload, ct);
        await stream.FlushAsync(ct);
    }

    // ---------------------------------------------------------------------------
    // Private error-response concrete type
    // IpcResponse is abstract; we need a concrete record for rejection/error paths
    // without polluting the public IPC.Contracts namespace.
    // ---------------------------------------------------------------------------

    private sealed record IpcErrorResponse(string CorrelationId, bool Success, string? Error = null)
        : IpcResponse(CorrelationId, Success, Error);
}
