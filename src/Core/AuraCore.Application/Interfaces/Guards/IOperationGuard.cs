namespace AuraCore.Application.Interfaces.Guards;

public interface IOperationGuard
{
    Task<GuardResult> CheckAsync(string moduleId, string operationId, CancellationToken ct = default);
}

public sealed record GuardResult(bool IsAllowed, string? Reason = null);
