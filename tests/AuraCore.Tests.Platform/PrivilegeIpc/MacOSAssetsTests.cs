using AuraCore.PrivHelper.MacOS;
using FluentAssertions;
using System.Xml;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class MacOSAssetsTests
{
    private static Stream Open(string name)
    {
        var asm = typeof(HelperVersion).Assembly;
        var full = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(name, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"resource '{name}' missing. Available: {string.Join(", ", asm.GetManifestResourceNames())}");
        return asm.GetManifestResourceStream(full)!;
    }

    [Fact]
    public void LaunchdPlist_is_wellformed_xml()
    {
        using var s = Open("pro.auracore.privhelper.plist");
        var doc = new XmlDocument();
        doc.Load(s);
        doc.DocumentElement!.Name.Should().Be("plist");
    }

    [Fact]
    public void LaunchdPlist_declares_expected_label_and_mach_service()
    {
        using var s = Open("pro.auracore.privhelper.plist");
        var xml = new StreamReader(s).ReadToEnd();
        xml.Should().Contain("<key>Label</key>");
        xml.Should().Contain("<string>pro.auracore.PrivHelper</string>");
        xml.Should().Contain("<key>MachServices</key>");
    }

    [Fact]
    public void LaunchdPlist_runs_as_root()
    {
        using var s = Open("pro.auracore.privhelper.plist");
        var xml = new StreamReader(s).ReadToEnd();
        xml.Should().Contain("<key>UserName</key>");
        xml.Should().Contain("<string>root</string>");
    }

    [Fact]
    public void LaunchdPlist_program_path_is_canonical()
    {
        using var s = Open("pro.auracore.privhelper.plist");
        var xml = new StreamReader(s).ReadToEnd();
        xml.Should().Contain("/Library/PrivilegedHelperTools/pro.auracore.privhelper");
    }

    [Fact]
    public void LaunchdPlist_caps_file_descriptors()
    {
        using var s = Open("pro.auracore.privhelper.plist");
        var xml = new StreamReader(s).ReadToEnd();
        xml.Should().Contain("SoftResourceLimits");
        xml.Should().Contain("NumberOfFiles");
    }

    [Fact]
    public void Entitlements_is_wellformed_xml()
    {
        using var s = Open("entitlements.plist");
        var doc = new XmlDocument();
        doc.Load(s);
        doc.DocumentElement!.Name.Should().Be("plist");
    }

    [Fact]
    public void Entitlements_does_not_enable_network_client_or_server()
    {
        using var s = Open("entitlements.plist");
        var xml = new StreamReader(s).ReadToEnd();
        if (xml.Contains("com.apple.security.network.client"))
            xml.Should().MatchRegex(@"com\.apple\.security\.network\.client</key>\s*<false/>");
        if (xml.Contains("com.apple.security.network.server"))
            xml.Should().MatchRegex(@"com\.apple\.security\.network\.server</key>\s*<false/>");
    }

    [Fact]
    public void Entitlements_does_not_grant_app_sandbox_to_privileged_daemon()
    {
        using var s = Open("entitlements.plist");
        var xml = new StreamReader(s).ReadToEnd();
        if (xml.Contains("com.apple.security.app-sandbox"))
            xml.Should().MatchRegex(@"com\.apple\.security\.app-sandbox</key>\s*<false/>");
    }
}
