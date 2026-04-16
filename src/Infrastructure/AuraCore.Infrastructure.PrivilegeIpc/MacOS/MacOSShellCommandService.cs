using AuraCore.Application.Interfaces.Platform;
using Microsoft.Extensions.Logging;

namespace AuraCore.Infrastructure.PrivilegeIpc.MacOS;

public sealed class MacOSShellCommandService : IShellCommandService
{
    private readonly ILogger<MacOSShellCommandService> _logger;
    public MacOSShellCommandService(ILogger<MacOSShellCommandService> logger) => _logger = logger;

    public Task<ShellResult> RunPrivilegedAsync(PrivilegedCommand command, CancellationToken ct = default)
    {
        // Real XPC client impl ships in sub-wave 5.2.2.
        _logger.LogInformation("[macos-stub] action={ActionId}; waiting for 5.2.2 impl", command.Id);
        return Task.FromResult(new ShellResult(
            false, -3, "", "macOS helper not installed.", PrivilegeAuthResult.HelperMissing));
    }
}
