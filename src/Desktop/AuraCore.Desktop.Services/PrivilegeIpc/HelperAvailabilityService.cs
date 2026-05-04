using System.ComponentModel;
using System.Runtime.CompilerServices;
using AuraCore.Application.Interfaces.Platform;

namespace AuraCore.Desktop.Services.PrivilegeIpc;

public sealed class HelperAvailabilityService : IHelperAvailabilityService
{
    // Phase 6.17.G: file-existence sentinel that install-privhelper.sh writes
    // when the daemon is successfully installed. A real D-Bus presence probe is
    // Phase 6.18 (requires Tmds.DBus session-bus query); for now, the install
    // script's success-marker is the source of truth.
    private const string InstallMarkerPath = "/opt/auracorepro/install-privhelper.sh.installed";

    private bool _isMissing;
    private bool _isBannerVisible;

    public HelperAvailabilityService()
    {
        // Phase 6.17.G: probe helper presence at startup so the banner can
        // light up immediately on launch (Linux/macOS only; Windows uses UAC
        // and never needs the banner). Background fire-and-forget; banner
        // surfaces as soon as the probe completes (~milliseconds for a file
        // existence check).
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            _ = Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(InstallMarkerPath))
                        ReportMissing();
                    else
                        ReportAvailable();
                }
                catch
                {
                    // Defensive: if the probe itself throws, leave defaults
                    // (banner stays hidden); next failed privileged op will
                    // call ReportMissing() to surface it.
                }
            });
        }
    }

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
