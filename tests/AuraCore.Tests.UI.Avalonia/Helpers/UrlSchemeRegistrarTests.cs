using System;
using AuraCore.UI.Avalonia.Helpers;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Helpers;

[Trait("Category", "Windows")]
public class UrlSchemeRegistrarTests
{
    // Tests mutate HKCU\Software\Classes\auracore. Each test cleans up in finally.

    [Fact]
    public void RegisterIfNeeded_writes_keys_and_IsRegisteredForBinary_returns_true()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fakePath = @"C:\TestBin\AuraCoreTest.exe";
        try
        {
            UrlSchemeRegistrar.Unregister();

            var registered = UrlSchemeRegistrar.RegisterIfNeeded(fakePath);
            Assert.True(registered);
            Assert.True(UrlSchemeRegistrar.IsRegisteredForBinary(fakePath));
        }
        finally
        {
            UrlSchemeRegistrar.Unregister();
        }
    }

    [Fact]
    public void RegisterIfNeeded_is_idempotent()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fakePath = @"C:\TestBin\AuraCoreTest.exe";
        try
        {
            UrlSchemeRegistrar.Unregister();

            var first = UrlSchemeRegistrar.RegisterIfNeeded(fakePath);
            var second = UrlSchemeRegistrar.RegisterIfNeeded(fakePath);

            Assert.True(first);
            Assert.False(second);
        }
        finally
        {
            UrlSchemeRegistrar.Unregister();
        }
    }

    [Fact]
    public void RegisterIfNeeded_with_different_binary_overwrites()
    {
        if (!OperatingSystem.IsWindows()) return;

        var pathA = @"C:\TestBin\A.exe";
        var pathB = @"C:\TestBin\B.exe";
        try
        {
            UrlSchemeRegistrar.Unregister();

            UrlSchemeRegistrar.RegisterIfNeeded(pathA);
            Assert.True(UrlSchemeRegistrar.IsRegisteredForBinary(pathA));

            UrlSchemeRegistrar.RegisterIfNeeded(pathB);
            Assert.True(UrlSchemeRegistrar.IsRegisteredForBinary(pathB));
            Assert.False(UrlSchemeRegistrar.IsRegisteredForBinary(pathA));
        }
        finally
        {
            UrlSchemeRegistrar.Unregister();
        }
    }

    [Fact]
    public void Unregister_removes_scheme()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fakePath = @"C:\TestBin\AuraCoreTest.exe";
        UrlSchemeRegistrar.RegisterIfNeeded(fakePath);
        Assert.True(UrlSchemeRegistrar.Unregister());
        Assert.False(UrlSchemeRegistrar.IsRegisteredForBinary(fakePath));
    }
}
