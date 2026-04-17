namespace AuraCore.IPC.Contracts;
public abstract record IpcRequest(string CorrelationId);
public abstract record IpcResponse(string CorrelationId, bool Success, string? Error = null);
public sealed record PingRequest(string CorrelationId) : IpcRequest(CorrelationId);
public sealed record PingResponse(string CorrelationId, bool Success) : IpcResponse(CorrelationId, Success);
public sealed record RegistryWriteRequest(string CorrelationId, string KeyPath, string ValueName, string Value) : IpcRequest(CorrelationId);
public sealed record RegistryWriteResponse(string CorrelationId, bool Success, string? Error = null) : IpcResponse(CorrelationId, Success, Error);

// Phase 5.5 — Driver Updater

public sealed record DriverScanRequest(string CorrelationId, string? DriverClass = null)
    : IpcRequest(CorrelationId)
{
    public string ActionId => "driver.scan";
}

public sealed record DriverExportRequest(string CorrelationId, string BackupDirectory)
    : IpcRequest(CorrelationId)
{
    public string ActionId => "driver.export";
}

public sealed record DriverOperationResponse(string CorrelationId, bool Success, string Output, int ExitCode, string? Error = null)
    : IpcResponse(CorrelationId, Success, Error);

// Phase 5.5 — Defender Manager

public enum DefenderAction
{
    UpdateSignatures,
    QuickScan,
    FullScan,
    SetRealtimeEnabled,
    SetRealtimeDisabled,
    AddExclusion,
    RemoveExclusion,
    RemoveThreat
}

public sealed record DefenderActionRequest(string CorrelationId, DefenderAction Action, string? Target = null)
    : IpcRequest(CorrelationId)
{
    public string ActionId => Action switch
    {
        DefenderAction.UpdateSignatures     => "defender.update-signatures",
        DefenderAction.QuickScan            => "defender.scan-quick",
        DefenderAction.FullScan             => "defender.scan-full",
        DefenderAction.SetRealtimeEnabled   => "defender.set-realtime",
        DefenderAction.SetRealtimeDisabled  => "defender.set-realtime",
        DefenderAction.AddExclusion         => "defender.add-exclusion",
        DefenderAction.RemoveExclusion      => "defender.remove-exclusion",
        DefenderAction.RemoveThreat         => "defender.remove-threat",
        _ => throw new System.ArgumentOutOfRangeException(nameof(Action))
    };
}

public sealed record DefenderActionResponse(string CorrelationId, bool Success, string Output, int ExitCode, string? Error = null)
    : IpcResponse(CorrelationId, Success, Error);

// Phase 5.5 — Service Manager

public enum ServiceAction
{
    Start,
    Stop,
    Restart,
    SetStartupAutomatic,
    SetStartupManual,
    SetStartupDisabled
}

public sealed record ServiceControlRequest(string CorrelationId, string ServiceName, ServiceAction Action)
    : IpcRequest(CorrelationId)
{
    public string ActionId => Action switch
    {
        ServiceAction.Start                 => "service.start",
        ServiceAction.Stop                  => "service.stop",
        ServiceAction.Restart               => "service.restart",
        ServiceAction.SetStartupAutomatic   => "service.set-startup",
        ServiceAction.SetStartupManual      => "service.set-startup",
        ServiceAction.SetStartupDisabled    => "service.set-startup",
        _ => throw new System.ArgumentOutOfRangeException(nameof(Action))
    };
}

public sealed record ServiceControlResponse(string CorrelationId, bool Success, string Output, int ExitCode, string? Error = null)
    : IpcResponse(CorrelationId, Success, Error);
