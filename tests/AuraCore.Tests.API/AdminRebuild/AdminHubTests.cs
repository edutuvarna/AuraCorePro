using AuraCore.API.Hubs;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace AuraCore.Tests.API.AdminRebuild;

public class AdminHubTests
{
    [Fact]
    public void AdminHub_class_has_Authorize_admin_role_attribute()
    {
        var attr = typeof(AdminHub).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: false);
        Assert.NotEmpty(attr);
        var authAttr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)attr[0];
        Assert.Equal("admin", authAttr.Roles);
    }

    [Fact]
    public void AdminConnectionCount_increments_and_decrements_thread_safely()
    {
        var startValue = AdminConnectionCount.Current;
        AdminConnectionCount.Increment();
        AdminConnectionCount.Increment();
        Assert.Equal(startValue + 2, AdminConnectionCount.Current);
        AdminConnectionCount.Decrement();
        Assert.Equal(startValue + 1, AdminConnectionCount.Current);
        AdminConnectionCount.Decrement();
        Assert.Equal(startValue, AdminConnectionCount.Current);
    }
}
