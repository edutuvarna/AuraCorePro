using AuraCore.Application.Interfaces.Platform;
using AuraCore.Desktop.Services.Responsive;
using FluentAssertions;
using System.ComponentModel;
using Xunit;

namespace AuraCore.Tests.Platform.Responsive;

public class NarrowModeServiceTests
{
    [Fact]
    public void New_service_starts_wide_with_zero_width()
    {
        var svc = new NarrowModeService();
        svc.CurrentWidth.Should().Be(0);
        svc.IsNarrow.Should().BeFalse();
        svc.IsVeryNarrow.Should().BeFalse();
    }

    [Fact]
    public void UpdateWidth_1200_sets_wide_state()
    {
        var svc = new NarrowModeService();
        svc.UpdateWidth(1200);
        svc.CurrentWidth.Should().Be(1200);
        svc.IsNarrow.Should().BeFalse();
        svc.IsVeryNarrow.Should().BeFalse();
    }

    [Fact]
    public void UpdateWidth_950_is_narrow_but_not_very_narrow()
    {
        var svc = new NarrowModeService();
        svc.UpdateWidth(950);
        svc.IsNarrow.Should().BeTrue();
        svc.IsVeryNarrow.Should().BeFalse();
    }

    [Fact]
    public void UpdateWidth_800_is_very_narrow()
    {
        var svc = new NarrowModeService();
        svc.UpdateWidth(800);
        svc.IsNarrow.Should().BeTrue();
        svc.IsVeryNarrow.Should().BeTrue();
    }

    [Fact]
    public void Threshold_boundary_999_is_narrow()
    {
        var svc = new NarrowModeService();
        svc.UpdateWidth(999);
        svc.IsNarrow.Should().BeTrue();
    }

    [Fact]
    public void Threshold_boundary_1000_is_wide()
    {
        var svc = new NarrowModeService();
        svc.UpdateWidth(1000);
        svc.IsNarrow.Should().BeFalse();
    }

    [Fact]
    public void UpdateWidth_raises_INPC_for_changed_properties_only()
    {
        var svc = new NarrowModeService();
        var raised = new List<string?>();
        ((INotifyPropertyChanged)svc).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        svc.UpdateWidth(1200);
        raised.Should().Contain(nameof(INarrowModeService.CurrentWidth));

        raised.Clear();
        svc.UpdateWidth(1150);   // still wide — IsNarrow/IsVeryNarrow unchanged
        raised.Should().Contain(nameof(INarrowModeService.CurrentWidth));
        raised.Should().NotContain(nameof(INarrowModeService.IsNarrow));
        raised.Should().NotContain(nameof(INarrowModeService.IsVeryNarrow));

        raised.Clear();
        svc.UpdateWidth(850);   // crosses both thresholds
        raised.Should().Contain(nameof(INarrowModeService.IsNarrow));
        raised.Should().Contain(nameof(INarrowModeService.IsVeryNarrow));
    }

#if DEBUG
    [Fact]
    public void ForceNarrowOverride_true_forces_narrow_and_very_narrow_regardless_of_width()
    {
        var svc = new NarrowModeService();
        svc.UpdateWidth(2000);
        svc.ForceNarrowOverride = true;
        svc.IsNarrow.Should().BeTrue();
        svc.IsVeryNarrow.Should().BeTrue();
    }

    [Fact]
    public void ForceNarrowOverride_false_forces_wide_regardless_of_width()
    {
        var svc = new NarrowModeService();
        svc.UpdateWidth(300);
        svc.ForceNarrowOverride = false;
        svc.IsNarrow.Should().BeFalse();
        svc.IsVeryNarrow.Should().BeFalse();
    }

    [Fact]
    public void ForceNarrowOverride_null_restores_width_based_calculation()
    {
        var svc = new NarrowModeService();
        svc.UpdateWidth(800);
        svc.ForceNarrowOverride = false;
        svc.IsNarrow.Should().BeFalse();
        svc.ForceNarrowOverride = null;
        svc.IsNarrow.Should().BeTrue();
    }

    [Fact]
    public void ForceNarrowOverride_change_raises_INPC()
    {
        var svc = new NarrowModeService();
        svc.UpdateWidth(1500);
        var raised = new List<string?>();
        ((INotifyPropertyChanged)svc).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        svc.ForceNarrowOverride = true;
        raised.Should().Contain(nameof(INarrowModeService.IsNarrow));
        raised.Should().Contain(nameof(INarrowModeService.IsVeryNarrow));
    }
#endif
}
