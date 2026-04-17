using AuraCore.PrivHelper.MacOS.Interop;
using FluentAssertions;
using System.Reflection;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

/// <summary>
/// Reflection-based contract tests for <see cref="CoreFoundation"/> interop class.
/// These tests verify P/Invoke signatures and constants exist and have the correct
/// values without actually loading CoreFoundation on non-macOS hosts.
/// </summary>
public class CoreFoundationInteropTests
{
    [Fact]
    public void CoreFoundation_exposes_CFStringCreateWithCString()
    {
        var method = typeof(CoreFoundation).GetMethod("CFStringCreateWithCString",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("CFStringCreateWithCString P/Invoke must be declared as internal static");
    }

    [Fact]
    public void CoreFoundation_exposes_CFNumberCreate()
    {
        var method = typeof(CoreFoundation).GetMethod("CFNumberCreate",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("CFNumberCreate P/Invoke must be declared as internal static");
    }

    [Fact]
    public void CoreFoundation_exposes_CFDictionaryCreate()
    {
        var method = typeof(CoreFoundation).GetMethod("CFDictionaryCreate",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("CFDictionaryCreate P/Invoke must be declared as internal static");
    }

    [Fact]
    public void CoreFoundation_exposes_CFRelease()
    {
        var method = typeof(CoreFoundation).GetMethod("CFRelease",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("CFRelease P/Invoke must be declared as internal static");
    }

    [Fact]
    public void CoreFoundation_constants_match_apple_values()
    {
        // kCFStringEncodingUTF8 = 0x08000100 per CFString.h
        typeof(CoreFoundation).GetField("KCFStringEncodingUTF8",
            BindingFlags.NonPublic | BindingFlags.Static)
            !.GetValue(null).Should().Be((uint)0x08000100,
                because: "kCFStringEncodingUTF8 must be 0x08000100 per Apple CFString.h");

        // kCFNumberIntType = 9 per CFNumber.h
        typeof(CoreFoundation).GetField("KCFNumberIntType",
            BindingFlags.NonPublic | BindingFlags.Static)
            !.GetValue(null).Should().Be(9,
                because: "kCFNumberIntType must be 9 per Apple CFNumber.h");
    }
}
