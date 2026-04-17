namespace AuraCore.Application.Interfaces.Platform;

/// <summary>
/// Cross-cutting navigation contract. Consumers (like the Dashboard's
/// Smart Optimize button) raise an intent via <see cref="NavigateTo"/>;
/// the shell (MainWindow) subscribes to <see cref="SectionRequested"/>
/// and routes by inspecting the section id.
///
/// Well-known section ids (Phase 5.4 scope):
///   "ai-recommendations"  → AIFeaturesView recommendations section
///   "ai-insights"         → AIFeaturesView insights section
///   "ai-schedule"         → AIFeaturesView schedule section
///
/// Additional ids may be registered without changing this interface.
/// </summary>
public interface INavigationService
{
    void NavigateTo(string sectionId);
    event EventHandler<NavigationRequestedEventArgs>? SectionRequested;
}

public sealed class NavigationRequestedEventArgs : EventArgs
{
    public string SectionId { get; }
    public NavigationRequestedEventArgs(string sectionId) => SectionId = sectionId;
}
