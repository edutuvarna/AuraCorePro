using AuraCore.PrivilegedService.Ops;
using FluentAssertions;
using Xunit;

namespace AuraCore.Tests.Platform.Security;

public class ActionWhitelistTests
{
    [Theory]
    [InlineData("driver.scan")]
    [InlineData("driver.export")]
    [InlineData("defender.update-signatures")]
    [InlineData("defender.scan-quick")]
    [InlineData("defender.scan-full")]
    [InlineData("defender.set-realtime")]
    [InlineData("defender.add-exclusion")]
    [InlineData("defender.remove-exclusion")]
    [InlineData("defender.remove-threat")]
    [InlineData("service.start")]
    [InlineData("service.stop")]
    [InlineData("service.restart")]
    [InlineData("service.set-startup")]
    public void IsAllowed_returns_true_for_whitelisted_actions(string id)
    {
        ActionWhitelist.Windows.IsAllowed(id).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown.action")]
    [InlineData("driver.fake")]
    [InlineData("system.format")]
    public void IsAllowed_returns_false_for_non_whitelisted(string id)
    {
        ActionWhitelist.Windows.IsAllowed(id).Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_is_case_sensitive()
    {
        ActionWhitelist.Windows.IsAllowed("DRIVER.SCAN").Should().BeFalse();
        ActionWhitelist.Windows.IsAllowed("driver.scan").Should().BeTrue();
    }
}
