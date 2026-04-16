using AuraCore.Infrastructure.PrivilegeIpc.Linux;
using Microsoft.Extensions.Logging;
using Tmds.DBus;

namespace AuraCore.PrivHelper.Linux;

public sealed class AuracorePrivHelperService : IPrivHelper
{
    private readonly ActionWhitelist _whitelist;
    private readonly IProcessInvoker _invoker;
    private readonly ILogger<AuracorePrivHelperService> _logger;

    public AuracorePrivHelperService(
        ActionWhitelist whitelist,
        IProcessInvoker invoker,
        ILogger<AuracorePrivHelperService> logger)
    {
        _whitelist = whitelist;
        _invoker = invoker;
        _logger = logger;
    }

    public ObjectPath ObjectPath => new(HelperRuntimeOptions.ObjectPath);

    public async Task<PrivHelperResult> RunActionAsync(string actionId, string[] args, int timeoutSeconds)
    {
        _logger.LogInformation(
            "[privhelper] action={ActionId} argv=[{Argv}] timeout={Timeout}s",
            actionId, string.Join(' ', args), timeoutSeconds);

        var resolution = _whitelist.Dispatch(actionId, args);
        if (resolution.IsRejected)
        {
            _logger.LogWarning("[privhelper] rejected: {Reason}", resolution.RejectionReason);
            return new PrivHelperResult
            {
                ExitCode = -100,
                Stdout = string.Empty,
                Stderr = resolution.RejectionReason,
                AuthState = "rejected",
            };
        }

        var result = await _invoker.InvokeAsync(resolution.Executable, resolution.Argv, timeoutSeconds);

        _logger.LogInformation(
            "[privhelper] action={ActionId} exit={Exit} stderr_len={StderrLen}",
            actionId, result.ExitCode, result.Stderr?.Length ?? 0);

        return new PrivHelperResult
        {
            ExitCode = result.ExitCode,
            Stdout = result.Stdout ?? string.Empty,
            Stderr = result.Stderr ?? string.Empty,
            AuthState = "cached",
        };
    }

    public Task<string> GetVersionAsync() => Task.FromResult(HelperVersion.Current);
}
