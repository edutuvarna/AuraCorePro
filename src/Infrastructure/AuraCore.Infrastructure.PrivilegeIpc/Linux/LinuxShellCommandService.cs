using AuraCore.Application.Interfaces.Platform;
using Microsoft.Extensions.Logging;
using Tmds.DBus;

namespace AuraCore.Infrastructure.PrivilegeIpc.Linux;

/// <summary>
/// Abstraction over the D-Bus connection factory so tests can inject a mock
/// <see cref="IPrivHelper"/> without touching the real system bus.
/// </summary>
public interface IPrivHelperConnectionFactory
{
    /// <summary>
    /// Returns an <see cref="IPrivHelper"/> proxy ready for method calls, or
    /// <c>null</c> if the daemon isn't reachable on the system bus.
    /// </summary>
    Task<IPrivHelper?> TryConnectAsync(CancellationToken ct = default);
}

/// <summary>
/// Production implementation: connects to the system D-Bus and resolves the
/// <c>pro.auracore.PrivHelper</c> service.
/// </summary>
public sealed class DefaultPrivHelperConnectionFactory : IPrivHelperConnectionFactory
{
    private const string BusName    = "pro.auracore.PrivHelper";
    private const string ObjectPath = "/pro/auracore/PrivHelper";

    public async Task<IPrivHelper?> TryConnectAsync(CancellationToken ct = default)
    {
        try
        {
            var connection = new Connection(Address.System);
            await connection.ConnectAsync();
            var proxy = connection.CreateProxy<IPrivHelper>(BusName, new ObjectPath(ObjectPath));
            // Probe with GetVersion to confirm the daemon is alive.
            await proxy.GetVersionAsync().WaitAsync(TimeSpan.FromSeconds(3), ct);
            return proxy;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// D-Bus client side of the Linux privilege helper. Connects to
/// <c>pro.auracore.PrivHelper</c> on the system bus and dispatches
/// whitelisted actions to the daemon. Surfaces HelperMissing to the
/// shared availability service when the daemon isn't reachable.
/// </summary>
public sealed class LinuxShellCommandService : IShellCommandService
{
    private readonly IPrivHelperConnectionFactory _connectionFactory;
    private readonly IHelperAvailabilityService   _availability;
    private readonly ILogger<LinuxShellCommandService> _logger;

    public LinuxShellCommandService(
        IPrivHelperConnectionFactory connectionFactory,
        IHelperAvailabilityService   availability,
        ILogger<LinuxShellCommandService> logger)
    {
        _connectionFactory = connectionFactory;
        _availability      = availability;
        _logger            = logger;
    }

    public async Task<ShellResult> RunPrivilegedAsync(PrivilegedCommand command, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[linux-shell] action={ActionId} timeout={Timeout}s", command.Id, command.TimeoutSeconds);

        var proxy = await _connectionFactory.TryConnectAsync(ct);
        if (proxy is null)
        {
            _logger.LogWarning("[linux-shell] helper unreachable; marking availability missing");
            _availability.ReportMissing();
            return new ShellResult(
                Success: false, ExitCode: -3,
                Stdout: string.Empty, Stderr: "Linux helper not installed.",
                AuthResult: PrivilegeAuthResult.HelperMissing);
        }

        PrivHelperResult daemonResult;
        try
        {
            daemonResult = await proxy.RunActionAsync(command.Id, command.Arguments, command.TimeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[linux-shell] D-Bus call failed");
            _availability.ReportMissing();
            return new ShellResult(
                Success: false, ExitCode: -4,
                Stdout: string.Empty, Stderr: $"D-Bus call failed: {ex.Message}",
                AuthResult: PrivilegeAuthResult.HelperMissing);
        }

        var authResult = MapAuthState(daemonResult.AuthState);
        _availability.ReportAvailable();

        var success = daemonResult.ExitCode == 0
                      && (authResult == PrivilegeAuthResult.AlreadyAuthorized
                          || authResult == PrivilegeAuthResult.Prompted);

        return new ShellResult(
            Success: success,
            ExitCode: daemonResult.ExitCode,
            Stdout: daemonResult.Stdout ?? string.Empty,
            Stderr: daemonResult.Stderr ?? string.Empty,
            AuthResult: authResult);
    }

    private static PrivilegeAuthResult MapAuthState(string? state) => state switch
    {
        "cached"   => PrivilegeAuthResult.AlreadyAuthorized,
        "prompted" => PrivilegeAuthResult.Prompted,
        "denied"   => PrivilegeAuthResult.Denied,
        "rejected" => PrivilegeAuthResult.HelperMissing,
        _          => PrivilegeAuthResult.HelperMissing,
    };
}
