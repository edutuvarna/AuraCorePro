using AuraCore.Application.Interfaces.Platform;
using Microsoft.Extensions.Logging;

namespace AuraCore.Infrastructure.PrivilegeIpc.Linux;

public sealed class LinuxShellCommandService : IShellCommandService
{
    private readonly ILogger<LinuxShellCommandService> _logger;
    public LinuxShellCommandService(ILogger<LinuxShellCommandService> logger) => _logger = logger;

    public Task<ShellResult> RunPrivilegedAsync(PrivilegedCommand command, CancellationToken ct = default)
    {
        // Real D-Bus client impl ships in sub-wave 5.2.1. Until then every caller
        // sees HelperMissing and the install dialog triggers correctly.
        _logger.LogInformation("[linux-stub] action={ActionId}; waiting for 5.2.1 impl", command.Id);
        return Task.FromResult(new ShellResult(
            false, -3, "", "Linux helper not installed.", PrivilegeAuthResult.HelperMissing));
    }
}
