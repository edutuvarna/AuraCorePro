using System.ComponentModel;

namespace AuraCore.Application.Interfaces.Platform;

public interface IHelperAvailabilityService : INotifyPropertyChanged
{
    bool IsMissing { get; }
    bool IsBannerVisible { get; }
    void ReportMissing();
    void ReportAvailable();
    void DismissBanner();
}
