using AuraCore.Application.Interfaces.Platform;
using AuraCore.Desktop.Services.Responsive;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AuraCore.Tests.Integration.Responsive;

public class NarrowModeCompositionTests
{
    [Fact]
    public void App_service_provider_resolves_INarrowModeService_as_singleton()
    {
        // Mirror the App.axaml.cs composition — just the new registrations.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<INarrowModeService, NarrowModeService>();
        var sp = services.BuildServiceProvider();

        var a = sp.GetRequiredService<INarrowModeService>();
        var b = sp.GetRequiredService<INarrowModeService>();
        a.Should().BeSameAs(b);
        a.Should().BeOfType<NarrowModeService>();
    }

    [Fact]
    public void NarrowModeService_starts_in_wide_state_before_width_is_pushed()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<INarrowModeService, NarrowModeService>();
        var svc = services.BuildServiceProvider().GetRequiredService<INarrowModeService>();

        svc.IsNarrow.Should().BeFalse();
        svc.IsVeryNarrow.Should().BeFalse();
        svc.CurrentWidth.Should().Be(0);
    }
}
