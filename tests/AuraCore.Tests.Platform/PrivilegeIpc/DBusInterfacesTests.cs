using AuraCore.Infrastructure.PrivilegeIpc.Linux;
using FluentAssertions;
using System.Reflection;
using Tmds.DBus;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class DBusInterfacesTests
{
    [Fact]
    public void IPrivHelper_has_DBusInterface_attribute_with_pro_auracore_namespace()
    {
        var attr = typeof(IPrivHelper).GetCustomAttribute<DBusInterfaceAttribute>();
        attr.Should().NotBeNull();
        attr!.Name.Should().Be("pro.auracore.PrivHelper1");
    }

    [Fact]
    public void IPrivHelper_implements_IDBusObject()
    {
        typeof(IPrivHelper).IsAssignableTo(typeof(IDBusObject)).Should().BeTrue();
    }

    [Fact]
    public void IPrivHelper_exposes_RunActionAsync_method()
    {
        var method = typeof(IPrivHelper).GetMethod("RunActionAsync");
        method.Should().NotBeNull();
        method!.GetParameters().Should().HaveCount(3);
        method.GetParameters()[0].ParameterType.Should().Be(typeof(string));
        method.GetParameters()[1].ParameterType.Should().Be(typeof(string[]));
        method.GetParameters()[2].ParameterType.Should().Be(typeof(int));
    }

    [Fact]
    public void IPrivHelper_exposes_GetVersionAsync_method()
    {
        var method = typeof(IPrivHelper).GetMethod("GetVersionAsync");
        method.Should().NotBeNull();
    }

    [Fact]
    public void PrivHelperResult_captures_execution_outcome()
    {
        var r = new PrivHelperResult
        {
            ExitCode = 0,
            Stdout = "ok",
            Stderr = "",
            AuthState = "cached",
        };
        r.ExitCode.Should().Be(0);
        r.AuthState.Should().Be("cached");
    }
}
