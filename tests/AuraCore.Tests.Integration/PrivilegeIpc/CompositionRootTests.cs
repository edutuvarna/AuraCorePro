using AuraCore.Application.Interfaces.Platform;
using AuraCore.Desktop.Services.PrivilegeIpc;
using AuraCore.Infrastructure.PrivilegeIpc;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    [Fact]
    public void App_UI_service_provider_resolves_linux_module_dependencies()
    {
        // Mirrors what App.axaml.cs does — just enough to verify the Phase 5.2.1
        // DI fix: AddPrivilegeIpc() + IHelperAvailabilityService registration must
        // allow BuildServiceProvider() to succeed (no unresolved ctor deps).
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddPrivilegeIpc();
        sc.AddSingleton<IHelperAvailabilityService, HelperAvailabilityService>();
        var sp = sc.BuildServiceProvider();
        sp.GetRequiredService<IShellCommandService>().Should().NotBeNull();
        sp.GetRequiredService<IHelperAvailabilityService>().Should().NotBeNull();
    }
}
