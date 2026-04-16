using System.ComponentModel;
using System.Runtime.CompilerServices;
using AuraCore.Application.Interfaces.Platform;

namespace AuraCore.Desktop.Services.PrivilegeIpc;

public sealed class HelperAvailabilityService : IHelperAvailabilityService
{
    private bool _isMissing;
    private bool _isBannerVisible;

    public bool IsMissing
    {
        get => _isMissing;
        private set => Set(ref _isMissing, value);
    }

    public bool IsBannerVisible
    {
        get => _isBannerVisible;
        private set => Set(ref _isBannerVisible, value);
    }

    public void ReportMissing()
    {
        IsMissing = true;
        IsBannerVisible = true;
    }

    public void ReportAvailable()
    {
        IsMissing = false;
        IsBannerVisible = false;
    }

    public void DismissBanner() => IsBannerVisible = false;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
