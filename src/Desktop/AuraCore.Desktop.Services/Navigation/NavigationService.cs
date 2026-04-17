using AuraCore.Application.Interfaces.Platform;

namespace AuraCore.Desktop.Services.Navigation;

public sealed class NavigationService : INavigationService
{
    public event EventHandler<NavigationRequestedEventArgs>? SectionRequested;

    public void NavigateTo(string sectionId)
    {
        ArgumentNullException.ThrowIfNull(sectionId);
        SectionRequested?.Invoke(this, new NavigationRequestedEventArgs(sectionId));
    }
}
