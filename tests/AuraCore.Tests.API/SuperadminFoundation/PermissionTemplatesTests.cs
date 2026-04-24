using AuraCore.API.Helpers;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class PermissionTemplatesTests
{
    [Fact]
    public void Default_grants_no_keys()
    {
        var keys = PermissionTemplates.GetPermissionsForTemplate(PermissionTemplates.Default);
        Assert.Empty(keys);
    }

    [Fact]
    public void Trusted_grants_all_tier2_actions()
    {
        var keys = PermissionTemplates.GetPermissionsForTemplate(PermissionTemplates.Trusted);
        Assert.Equal(6, keys.Count);
        Assert.All(PermissionKeys.AllTier2, t2 => Assert.Contains(t2, keys));
    }

    [Fact]
    public void ReadOnly_grants_no_keys_and_requires_is_readonly()
    {
        var keys = PermissionTemplates.GetPermissionsForTemplate(PermissionTemplates.ReadOnly);
        Assert.Empty(keys);
        Assert.True(PermissionTemplates.RequiresIsReadonlyFlag(PermissionTemplates.ReadOnly));
    }

    [Fact]
    public void Custom_throws_when_resolving_keys()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PermissionTemplates.GetPermissionsForTemplate(PermissionTemplates.Custom));
    }

    [Fact]
    public void Unknown_template_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PermissionTemplates.GetPermissionsForTemplate("Bogus"));
    }
}
