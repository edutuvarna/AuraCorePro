using System.Reflection;
using AuraCore.API.Controllers.Admin;
using AuraCore.API.Controllers.Payment;
using AuraCore.API.Filters;
using AuraCore.API.Helpers;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class ControllerAttributeApplicationTests
{
    private static bool HasRequiresPermission(MethodInfo m, string key)
        => m.GetCustomAttributes<RequiresPermissionAttribute>().Any(a => a.Permission == key);

    private static bool HasDestructiveAction(MethodInfo m)
        => m.GetCustomAttributes<DestructiveActionAttribute>().Any();

    [Fact]
    public void AdminUserController_DeleteUser_has_ActionUsersDelete()
    {
        var m = typeof(AdminUserController).GetMethod("DeleteUser")!;
        Assert.True(HasRequiresPermission(m, PermissionKeys.ActionUsersDelete));
    }

    [Fact]
    public void AdminUserController_BanUser_has_ActionUsersBan()
    {
        var m = typeof(AdminUserController).GetMethod("BanUser")!;
        Assert.NotNull(m);
        Assert.True(HasRequiresPermission(m, PermissionKeys.ActionUsersBan));
    }

    [Fact]
    public void AdminSubscriptionController_mutations_have_permissions()
    {
        var t = typeof(AdminSubscriptionController);
        var grant = t.GetMethod("Grant") ?? t.GetMethods().FirstOrDefault(m => m.Name.Contains("Grant"));
        var revoke = t.GetMethod("Revoke") ?? t.GetMethods().FirstOrDefault(m => m.Name.Contains("Revoke"));
        Assert.NotNull(grant);
        Assert.NotNull(revoke);
        Assert.True(HasRequiresPermission(grant!, PermissionKeys.ActionSubscriptionsGrant));
        Assert.True(HasRequiresPermission(revoke!, PermissionKeys.ActionSubscriptionsRevoke));
    }

    [Fact]
    public void CryptoController_admin_verify_reject_have_permissions()
    {
        var t = typeof(CryptoController);
        var verify = t.GetMethods().First(m => m.Name.Contains("Verify", StringComparison.OrdinalIgnoreCase) && m.Name.Contains("Admin", StringComparison.OrdinalIgnoreCase));
        var reject = t.GetMethods().First(m => m.Name.Contains("Reject", StringComparison.OrdinalIgnoreCase) && m.Name.Contains("Admin", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasRequiresPermission(verify, PermissionKeys.ActionPaymentsApproveCrypto));
        Assert.True(HasRequiresPermission(reject, PermissionKeys.ActionPaymentsRejectCrypto));
    }

    [Fact]
    public void AdminLicenseController_Revoke_and_Activate_have_DestructiveAction()
    {
        var t = typeof(AdminLicenseController);
        var revoke = t.GetMethods().First(m => m.Name == "Revoke");
        var activate = t.GetMethods().First(m => m.Name == "Activate");
        Assert.True(HasDestructiveAction(revoke));
        Assert.True(HasDestructiveAction(activate));
    }
}
