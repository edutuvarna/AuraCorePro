namespace AuraCore.IPC.Contracts;
public abstract record IpcRequest(string CorrelationId);
public abstract record IpcResponse(string CorrelationId, bool Success, string? Error = null);
public sealed record PingRequest(string CorrelationId) : IpcRequest(CorrelationId);
public sealed record PingResponse(string CorrelationId, bool Success) : IpcResponse(CorrelationId, Success);
public sealed record RegistryWriteRequest(string CorrelationId, string KeyPath, string ValueName, string Value) : IpcRequest(CorrelationId);
public sealed record RegistryWriteResponse(string CorrelationId, bool Success, string? Error = null) : IpcResponse(CorrelationId, Success, Error);
