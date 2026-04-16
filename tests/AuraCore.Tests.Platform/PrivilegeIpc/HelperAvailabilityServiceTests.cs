using AuraCore.Application.Interfaces.Platform;
using AuraCore.Desktop.Services.PrivilegeIpc;
using FluentAssertions;
using System.ComponentModel;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class HelperAvailabilityServiceTests
{
    [Fact]
    public void ReportMissing_flips_IsMissing_and_raises_INPC()
    {
        var svc = new HelperAvailabilityService();
        var raised = new List<string?>();
        ((INotifyPropertyChanged)svc).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        svc.ReportMissing();

        svc.IsMissing.Should().BeTrue();
        raised.Should().Contain(nameof(IHelperAvailabilityService.IsMissing));
    }

    [Fact]
    public void ReportAvailable_flips_back()
    {
        var svc = new HelperAvailabilityService();
        svc.ReportMissing();
        svc.ReportAvailable();
        svc.IsMissing.Should().BeFalse();
    }

    [Fact]
    public void Dismiss_hides_banner_without_changing_IsMissing()
    {
        var svc = new HelperAvailabilityService();
        svc.ReportMissing();
        svc.DismissBanner();
        svc.IsMissing.Should().BeTrue();
        svc.IsBannerVisible.Should().BeFalse();
    }
}
