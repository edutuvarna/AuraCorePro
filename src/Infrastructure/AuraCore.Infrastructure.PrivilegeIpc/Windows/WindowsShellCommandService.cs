using AuraCore.Application.Interfaces.Platform;
using Microsoft.Extensions.Logging;

namespace AuraCore.Infrastructure.PrivilegeIpc.Windows;

public sealed class WindowsShellCommandService : IShellCommandService
{
    private readonly ILogger<WindowsShellCommandService> _logger;

    public WindowsShellCommandService(ILogger<WindowsShellCommandService> logger)
    {
        _logger = logger;
    }

    public Task<ShellResult> RunPrivilegedAsync(PrivilegedCommand command, CancellationToken ct = default)
    {
        // Phase 5.2 scope: Windows privileged path is not wired yet. Real Named Pipe
        // server + elevated-helper integration lands in Phase 5.5 alongside Driver /
        // Defender / Service write capability. For now every caller on Windows gets
        // a consistent HelperMissing signal so the shared UX (global banner) can
        // surface it. Non-privileged Windows ops continue to use ProcessRunner
        // directly, NOT IShellCommandService.
        _logger.LogInformation(
            "[windows] action={ActionId} intercepted; helper not yet implemented", command.Id);

        return Task.FromResult(new ShellResult(
            Success: false, ExitCode: -3,
            Stdout: "",
            Stderr: "Windows privilege helper not implemented (Phase 5.5 work).",
            AuthResult: PrivilegeAuthResult.HelperMissing));
    }
}
