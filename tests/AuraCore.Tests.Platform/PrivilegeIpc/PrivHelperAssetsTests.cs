using AuraCore.PrivHelper.Linux;
using FluentAssertions;
using System.Reflection;
using System.Xml;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class PrivHelperAssetsTests
{
    private static Stream OpenEmbeddedResource(string name)
    {
        var asm = typeof(HelperVersion).Assembly;
        var full = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(name, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Embedded resource '{name}' not found. Available: {string.Join(", ", asm.GetManifestResourceNames())}");
        return asm.GetManifestResourceStream(full)!;
    }

    [Fact]
    public void Polkit_policy_is_wellformed_xml()
    {
        using var stream = OpenEmbeddedResource("pro.auracore.privhelper.policy");
        var doc = new XmlDocument();
        doc.Load(stream);
        doc.DocumentElement!.Name.Should().Be("policyconfig");
    }

    [Fact]
    public void Polkit_policy_has_all_seven_action_ids()
    {
        using var stream = OpenEmbeddedResource("pro.auracore.privhelper.policy");
        var doc = new XmlDocument();
        doc.Load(stream);
        var actionIds = doc.SelectNodes("//action/@id")!.Cast<XmlAttribute>().Select(a => a.Value).ToList();
        actionIds.Should().BeEquivalentTo(new[]
        {
            "pro.auracore.privhelper.journal",
            "pro.auracore.privhelper.snap-flatpak",
            "pro.auracore.privhelper.docker",
            "pro.auracore.privhelper.kernel",
            "pro.auracore.privhelper.app-installer",
            "pro.auracore.privhelper.grub",
            "pro.auracore.privhelper.symlink-create",  // Phase 5.5
        });
    }

    [Fact]
    public void Polkit_policy_uses_auth_admin_keep_for_all_actions()
    {
        using var stream = OpenEmbeddedResource("pro.auracore.privhelper.policy");
        var doc = new XmlDocument();
        doc.Load(stream);
        var defaults = doc.SelectNodes("//defaults/allow_active")!.Cast<XmlElement>().Select(e => e.InnerText).ToList();
        defaults.Should().AllBe("auth_admin_keep");
        defaults.Should().HaveCount(7); // Phase 5.5: added symlink-create
    }

    [Fact]
    public void Systemd_service_has_required_directives()
    {
        using var stream = OpenEmbeddedResource("pro.auracore.privhelper.service");
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        content.Should().Contain("[Unit]");
        content.Should().Contain("[Service]");
        content.Should().Contain("[Install]");  // Even though D-Bus activated, Install section still present for manual enable
        content.Should().Contain("Type=dbus");
        content.Should().Contain("BusName=pro.auracore.PrivHelper");
        content.Should().Contain("User=root");
        content.Should().Contain("ProtectSystem=strict");
        content.Should().Contain("ProtectHome=true");
        content.Should().Contain("PrivateTmp=true");
        // Daemon binary path: install.sh copies to /usr/local/lib/auracore/privhelper
        content.Should().MatchRegex(@"ExecStart=.*privhelper");
    }
}
