using AuraCore.Application.Interfaces.Platform;
using FluentAssertions;
using System.ComponentModel;
using System.Reflection;
using Xunit;

namespace AuraCore.Tests.Platform.Responsive;

public class INarrowModeServiceContractTests
{
    [Fact]
    public void Interface_extends_INotifyPropertyChanged()
    {
        typeof(INarrowModeService).IsAssignableTo(typeof(INotifyPropertyChanged)).Should().BeTrue();
    }

    [Fact]
    public void Interface_exposes_IsNarrow_IsVeryNarrow_CurrentWidth()
    {
        typeof(INarrowModeService).GetProperty("IsNarrow").Should().NotBeNull();
        typeof(INarrowModeService).GetProperty("IsVeryNarrow").Should().NotBeNull();
        typeof(INarrowModeService).GetProperty("CurrentWidth").Should().NotBeNull();
    }

    [Fact]
    public void Interface_property_types_are_correct()
    {
        typeof(INarrowModeService).GetProperty("IsNarrow")!.PropertyType.Should().Be(typeof(bool));
        typeof(INarrowModeService).GetProperty("IsVeryNarrow")!.PropertyType.Should().Be(typeof(bool));
        typeof(INarrowModeService).GetProperty("CurrentWidth")!.PropertyType.Should().Be(typeof(double));
    }

#if DEBUG
    [Fact]
    public void Interface_exposes_ForceNarrowOverride_only_in_DEBUG()
    {
        typeof(INarrowModeService).GetProperty("ForceNarrowOverride").Should().NotBeNull();
    }
#else
    [Fact]
    public void Interface_has_NO_ForceNarrowOverride_in_Release()
    {
        typeof(INarrowModeService).GetProperty("ForceNarrowOverride").Should().BeNull();
    }
#endif
}
