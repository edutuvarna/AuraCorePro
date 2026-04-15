using System.ComponentModel;

namespace AuraCore.UI.Avalonia.Services.AI;

/// <summary>
/// Aggregated activeness state of CORTEX across all AI features.
/// </summary>
public enum CortexActiveness
{
    /// <summary>At least one feature enabled.</summary>
    Active,
    /// <summary>All features disabled, but at least one has been enabled before.</summary>
    Paused,
    /// <summary>No feature has ever been enabled on this install.</summary>
    Ready
}

/// <summary>
/// Aggregates AI feature toggle state for display across the app (dashboard, status bar, AI Features page).
/// Subscribers bind to PropertyChanged events. Owners call <see cref="Refresh"/> after settings mutations.
/// </summary>
public interface ICortexAmbientService : INotifyPropertyChanged
{
    bool AnyFeatureEnabled { get; }
    int EnabledFeatureCount { get; }
    int TotalFeatureCount { get; }
    int LearningDay { get; }
    CortexActiveness Activeness { get; }
    string AggregatedStatusText { get; }

    /// <summary>
    /// Recomputes state from current settings and fires PropertyChanged.
    /// Call after any feature toggle change.
    /// </summary>
    void Refresh();
}
