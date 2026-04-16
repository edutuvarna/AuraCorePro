namespace AuraCore.Application.Interfaces.Platform;

public interface IShellCommandService
{
    Task<ShellResult> RunPrivilegedAsync(PrivilegedCommand command, CancellationToken ct = default);
}

public sealed record PrivilegedCommand(
    string Id,
    string Executable,
    string[] Arguments,
    int TimeoutSeconds = 60);

public sealed record ShellResult(
    bool Success,
    int ExitCode,
    string Stdout,
    string Stderr,
    PrivilegeAuthResult AuthResult);

public enum PrivilegeAuthResult
{
    AlreadyAuthorized,
    Prompted,
    Denied,
    HelperMissing,
}
