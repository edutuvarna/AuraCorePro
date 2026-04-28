using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application.Interfaces.Platform;

namespace AuraCore.Module.ServiceManager;

public sealed record ServiceOperationOutcome(bool Success, string Output, bool HelperMissing, string? Error = null);

public sealed class ServiceManagerEngine
{
    private readonly IShellCommandService? _shell;

    public ServiceManagerEngine() { }

    public ServiceManagerEngine(IShellCommandService shell) { _shell = shell; }

    public Task<IReadOnlyList<ServiceEntry>> ListServicesAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult<IReadOnlyList<ServiceEntry>>(Array.Empty<ServiceEntry>());

        var list = ServiceController.GetServices()
            .Select(ProjectEntry)
            .OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.FromResult<IReadOnlyList<ServiceEntry>>(list);
    }

    // Phase 6.16.F: explicit-platform helper extracted from LINQ Select so CA1416
    // analyzer can see ServiceController members are touched on a Windows-annotated method.
    [SupportedOSPlatform("windows")]
    private static ServiceEntry ProjectEntry(ServiceController sc)
        => new(sc.ServiceName, sc.DisplayName, sc.Status.ToString(), sc.StartType.ToString());

    public Task<ServiceOperationOutcome> StartAsync(string name, CancellationToken ct = default)
        => DispatchAsync("service.start", new[] { name }, ct);

    public Task<ServiceOperationOutcome> StopAsync(string name, CancellationToken ct = default)
        => DispatchAsync("service.stop", new[] { name }, ct);

    public Task<ServiceOperationOutcome> RestartAsync(string name, CancellationToken ct = default)
        => DispatchAsync("service.restart", new[] { name }, ct);

    public Task<ServiceOperationOutcome> SetStartupAsync(string name, string mode, CancellationToken ct = default)
    {
        if (mode != "auto" && mode != "demand" && mode != "disabled")
            return Task.FromResult(new ServiceOperationOutcome(false, "", false, $"invalid mode {mode}"));
        return DispatchAsync("service.set-startup", new[] { name, mode }, ct);
    }

    private async Task<ServiceOperationOutcome> DispatchAsync(string id, string[] args, CancellationToken ct)
    {
        if (_shell is null)
            return new ServiceOperationOutcome(false, "", true, "shell command service not wired");

        var result = await _shell.RunPrivilegedAsync(
            new PrivilegedCommand(id, "sc.exe", args, 30), ct);
        return new ServiceOperationOutcome(
            result.Success,
            result.Stdout,
            result.AuthResult == PrivilegeAuthResult.HelperMissing,
            result.Success ? null : result.Stderr);
    }
}
