using AuraCore.Application.Interfaces.Platform;
using Microsoft.Extensions.Logging;

namespace AuraCore.Infrastructure.PrivilegeIpc.MacOS;

/// <summary>
/// Real XPC client implementation of <see cref="IShellCommandService"/> for macOS.
/// Connects to the pro.auracore.PrivHelper Mach service via libxpc and uses
/// the synchronous xpc_connection_send_message_with_reply_sync API.
/// </summary>
public sealed class MacOSShellCommandService : IShellCommandService
{
    private readonly IXpcConnectionFactory _factory;
    private readonly IHelperAvailabilityService _availability;
    private readonly ILogger<MacOSShellCommandService> _logger;

    public MacOSShellCommandService(
        IXpcConnectionFactory factory,
        IHelperAvailabilityService availability,
        ILogger<MacOSShellCommandService> logger)
    {
        _factory = factory;
        _availability = availability;
        _logger = logger;
    }

    public async Task<ShellResult> RunPrivilegedAsync(PrivilegedCommand command, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[macos-shell] action={ActionId} timeout={Timeout}s", command.Id, command.TimeoutSeconds);

        using var conn = await _factory.TryConnectAsync(ct);
        if (conn is null)
        {
            _logger.LogWarning("[macos-shell] XPC helper unreachable; marking availability missing");
            _availability.ReportMissing();
            return new ShellResult(
                Success: false, ExitCode: -3,
                Stdout: string.Empty, Stderr: "macOS helper not installed.",
                AuthResult: PrivilegeAuthResult.HelperMissing);
        }

        XpcClientReply? reply;
        try
        {
            reply = await conn.SendRequestAsync(
                new XpcClientRequest(command.Id, command.Arguments, command.TimeoutSeconds), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[macos-shell] XPC call threw");
            _availability.ReportMissing();
            return new ShellResult(false, -4, string.Empty, $"XPC call failed: {ex.Message}",
                PrivilegeAuthResult.HelperMissing);
        }

        if (reply is null)
        {
            _logger.LogWarning("[macos-shell] XPC reply was null");
            _availability.ReportMissing();
            return new ShellResult(false, -4, string.Empty, "XPC reply was null.",
                PrivilegeAuthResult.HelperMissing);
        }

        _availability.ReportAvailable();

        var authResult = MapAuthState(reply.AuthState);
        var success = reply.ExitCode == 0 && authResult == PrivilegeAuthResult.AlreadyAuthorized;

        return new ShellResult(
            Success: success,
            ExitCode: (int)Math.Clamp(reply.ExitCode, int.MinValue, int.MaxValue),
            Stdout: reply.Stdout ?? string.Empty,
            Stderr: reply.Stderr ?? string.Empty,
            AuthResult: authResult);
    }

    /// <summary>
    /// Maps the daemon-returned auth_state string to a <see cref="PrivilegeAuthResult"/>.
    /// Per mini-spec §3.5: macOS never emits "prompted" or "denied" (SMAppService handles
    /// one-time consent; no per-call prompts). Only "cached" maps to AlreadyAuthorized;
    /// all other values — including "rejected", "prompted", "denied", or unknown —
    /// map to HelperMissing.
    /// </summary>
    private static PrivilegeAuthResult MapAuthState(string? state) => state switch
    {
        "cached" => PrivilegeAuthResult.AlreadyAuthorized,
        _        => PrivilegeAuthResult.HelperMissing,
    };
}
