using System.ComponentModel;

namespace AuraCore.Application.Interfaces.Engines;

public interface IAuraCoreLLM : IDisposable, INotifyPropertyChanged
{
    bool IsAvailable { get; }
    Task<string> AskAsync(string question, LlmContext? context, CancellationToken ct = default);

    /// <summary>
    /// Reloads the LLM without app restart. Null <paramref name="newConfig"/>
    /// reloads the current config from disk (useful when the model file has
    /// been updated). Non-null hot-swaps to the new config.
    /// </summary>
    /// <remarks>
    /// - Throws <see cref="InvalidOperationException"/> if
    ///   <see cref="IsReloading"/> is already true.
    /// - If an inference is in flight, waits for it to drain before
    ///   disposing the old instance.
    /// - Sets <see cref="IsReloading"/> true at entry, false on exit
    ///   (including exceptions + cancellation). INPC raised for both
    ///   transitions.
    /// </remarks>
    Task ReloadAsync(LlmConfiguration? newConfig = null, CancellationToken ct = default);

    /// <summary>
    /// True while <see cref="ReloadAsync"/> is executing. INPC-raised for
    /// UI state (disable inference buttons, show spinner).
    /// </summary>
    bool IsReloading { get; }
}

/// <summary>
/// Configuration passed to <see cref="IAuraCoreLLM.ReloadAsync"/> for hot-swapping
/// the model. <see cref="ModelPath"/> is the path to the GGUF model file.
/// </summary>
public sealed record LlmConfiguration(string ModelPath, string? RagEndpointUrl = null);

public sealed record LlmContext(
    double CpuPercent,
    double RamPercent,
    double DiskPercent,
    string? ProfileType,
    string Language = "en",
    IReadOnlyList<string>? ActiveAlerts = null);
