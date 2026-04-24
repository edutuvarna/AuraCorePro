using AuraCore.API.Helpers;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class PermissionKeysTests
{
    [Fact]
    public void AllTier1_lists_four_tab_keys()
    {
        Assert.Equal(4, PermissionKeys.AllTier1.Count);
        Assert.Contains("tab:configuration", PermissionKeys.AllTier1);
        Assert.Contains("tab:ipwhitelist", PermissionKeys.AllTier1);
        Assert.Contains("tab:updates", PermissionKeys.AllTier1);
        Assert.Contains("tab:rolechange", PermissionKeys.AllTier1);
    }

    [Fact]
    public void AllTier2_lists_six_action_keys()
    {
        Assert.Equal(6, PermissionKeys.AllTier2.Count);
        Assert.Contains("action:users.delete", PermissionKeys.AllTier2);
        Assert.Contains("action:users.ban", PermissionKeys.AllTier2);
        Assert.Contains("action:subscriptions.grant", PermissionKeys.AllTier2);
        Assert.Contains("action:subscriptions.revoke", PermissionKeys.AllTier2);
        Assert.Contains("action:payments.approveCrypto", PermissionKeys.AllTier2);
        Assert.Contains("action:payments.rejectCrypto", PermissionKeys.AllTier2);
    }

    [Fact]
    public void IsTabKey_classifies_correctly()
    {
        Assert.True(PermissionKeys.IsTabKey("tab:configuration"));
        Assert.False(PermissionKeys.IsTabKey("action:users.delete"));
        Assert.False(PermissionKeys.IsTabKey("unknown"));
    }
}
