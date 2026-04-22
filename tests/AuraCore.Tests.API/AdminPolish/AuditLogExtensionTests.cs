using System.Reflection;
using AuraCore.API.Controllers.Admin;
using AuraCore.API.Filters;
using Xunit;

namespace AuraCore.Tests.API.AdminPolish;

public class AuditLogExtensionTests
{
    // Contract-level tests: verify each mutation method declares [AuditAction].
    // Reflection is sufficient — attribute presence is the invariant; Phase 6.8's
    // AuditLogAttributeTests.cs already pins the filter's runtime behavior.

    [Theory]
    [InlineData(typeof(AdminConfigController), "Update", "UpdateAppConfig")]
    [InlineData(typeof(AdminIpWhitelistController), "Add", "AddIpWhitelist")]
    [InlineData(typeof(AdminIpWhitelistController), "Delete", "RemoveIpWhitelist")]
    [InlineData(typeof(AdminUserController), "DeleteUser", "DeleteUser")]
    [InlineData(typeof(AdminUserController), "ResetPassword", "ResetPassword")]
    public void Mutation_method_declares_AuditAction_attribute(Type controllerType, string methodName, string expectedAction)
    {
        var method = controllerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);

        var attr = method!.GetCustomAttribute<AuditActionAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(expectedAction, attr!.Action);
    }

    [Fact]
    public void AdminCrashReportController_Delete_has_AuditAction_if_method_exists()
    {
        var method = typeof(AdminCrashReportController)
            .GetMethod("Delete", BindingFlags.Public | BindingFlags.Instance);
        if (method is null) return;  // Skip if controller has no Delete

        var attr = method.GetCustomAttribute<AuditActionAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("DeleteCrashReport", attr!.Action);
    }
}
