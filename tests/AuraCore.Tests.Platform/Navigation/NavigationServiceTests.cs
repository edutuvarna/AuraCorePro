using AuraCore.Application.Interfaces.Platform;
using AuraCore.Desktop.Services.Navigation;
using FluentAssertions;
using Xunit;

namespace AuraCore.Tests.Platform.Navigation;

public class NavigationServiceTests
{
    [Fact]
    public void NavigateTo_raises_SectionRequested_with_provided_id()
    {
        var svc = new NavigationService();
        string? received = null;
        svc.SectionRequested += (_, args) => received = args.SectionId;

        svc.NavigateTo("ai-recommendations");

        received.Should().Be("ai-recommendations");
    }

    [Fact]
    public void NavigateTo_with_no_subscribers_does_not_throw()
    {
        var svc = new NavigationService();
        Action act = () => svc.NavigateTo("ai-insights");
        act.Should().NotThrow();
    }

    [Fact]
    public void NavigateTo_fires_all_subscribers()
    {
        var svc = new NavigationService();
        int count = 0;
        svc.SectionRequested += (_, _) => count++;
        svc.SectionRequested += (_, _) => count++;

        svc.NavigateTo("x");

        count.Should().Be(2);
    }

    [Fact]
    public void NavigateTo_rejects_null_section_id()
    {
        var svc = new NavigationService();
        Action act = () => svc.NavigateTo(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
