namespace AuraCore.Application.Interfaces.Engines;

public interface IAuraCoreLLM : IDisposable
{
    bool IsAvailable { get; }
    Task<string> AskAsync(string question, LlmContext? context, CancellationToken ct = default);
}

public sealed record LlmContext(
    double CpuPercent,
    double RamPercent,
    double DiskPercent,
    string? ProfileType,
    string Language = "en",
    IReadOnlyList<string>? ActiveAlerts = null);
