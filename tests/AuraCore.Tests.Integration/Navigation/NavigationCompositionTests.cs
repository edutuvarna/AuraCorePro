using AuraCore.Application.Interfaces.Platform;
using AuraCore.Desktop.Services.Navigation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.Integration.Navigation;

public class NavigationCompositionTests
{
    [Fact]
    public void DI_resolves_INavigationService_as_singleton()
    {
        var sc = new ServiceCollection();
        sc.AddSingleton<INavigationService, NavigationService>();
        var sp = sc.BuildServiceProvider();

        var a = sp.GetRequiredService<INavigationService>();
        var b = sp.GetRequiredService<INavigationService>();
        a.Should().BeSameAs(b);
    }
}
