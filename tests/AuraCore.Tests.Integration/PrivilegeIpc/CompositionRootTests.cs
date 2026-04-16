using AuraCore.Application.Interfaces.Platform;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.Integration.PrivilegeIpc;

public class CompositionRootTests
{
    [Fact]
    public void App_service_provider_resolves_IShellCommandService()
    {
        var services = AuraCore.Desktop.AppBootstrapper.BuildServices();
        var svc = services.GetRequiredService<IShellCommandService>();
        svc.Should().NotBeNull();
    }

    [Fact]
    public void App_service_provider_resolves_IHelperAvailabilityService_as_singleton()
    {
        var services = AuraCore.Desktop.AppBootstrapper.BuildServices();
        var a = services.GetRequiredService<IHelperAvailabilityService>();
        var b = services.GetRequiredService<IHelperAvailabilityService>();
        a.Should().BeSameAs(b);
    }
}
