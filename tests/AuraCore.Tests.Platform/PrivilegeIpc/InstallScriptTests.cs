using AuraCore.PrivHelper.Linux;
using FluentAssertions;
using System.Reflection;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class InstallScriptTests
{
    private static string LoadInstallScript()
    {
        var asm = typeof(HelperVersion).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("install.sh", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"install.sh resource not found. Available: {string.Join(", ", asm.GetManifestResourceNames())}");
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void InstallScript_has_bash_shebang()
    {
        var s = LoadInstallScript();
        s.Should().StartWith("#!");
        (s.StartsWith("#!/bin/bash") || s.StartsWith("#!/usr/bin/env bash"))
            .Should().BeTrue("must use bash shebang");
    }

    [Fact]
    public void InstallScript_enables_strict_mode()
    {
        var s = LoadInstallScript();
        s.Should().Contain("set -euo pipefail");
    }

    [Fact]
    public void InstallScript_quotes_positional_argument()
    {
        var s = LoadInstallScript();
        // Unquoted "$1" is a command-injection risk when pkexec is in play.
        // Every use of $1 should appear inside double quotes.
        // Rough heuristic: the script must never contain the pattern ` $1 ` or `=$1`
        // without surrounding quotes.
        s.Should().NotContain(" $1 ");
        s.Should().NotContain("cp $1");
        s.Should().NotContain("rm $1");
        s.Should().Contain("\"$1\"");
    }

    [Fact]
    public void InstallScript_does_not_use_eval_on_external_input()
    {
        var s = LoadInstallScript();
        // Defensive — eval on anything reaching $1 is a red flag.
        // Allow 'eval' in literal comments/examples but not with $1 interpolation.
        s.Should().NotMatchRegex(@"eval.*\$1");
    }

    [Fact]
    public void InstallScript_installs_to_canonical_paths()
    {
        var s = LoadInstallScript();
        s.Should().Contain("/usr/local/lib/auracore/privhelper");
        s.Should().Contain("/usr/share/polkit-1/actions/pro.auracore.privhelper.policy");
        s.Should().Contain("/usr/lib/systemd/system/pro.auracore.privhelper.service");
    }

    [Fact]
    public void InstallScript_reloads_systemd_and_polkit()
    {
        var s = LoadInstallScript();
        s.Should().Contain("systemctl daemon-reload");
        // polkit reload — at least one fallback path must appear
        var polkitReloadPaths = new[]
        {
            "systemctl reload polkit",
            "pkill -HUP polkitd",
            "ReloadConfiguration",   // dbus-send to PolicyKit1
        };
        polkitReloadPaths.Any(p => s.Contains(p)).Should().BeTrue("at least one polkit reload path must be present");
    }

    [Fact]
    public void InstallScript_is_idempotent_via_hash_compare_or_install_tool()
    {
        var s = LoadInstallScript();
        // Either explicit hash compare OR reliance on `install` which is idempotent for identical bytes.
        var idempotentMarkers = new[] { "cmp -s", "sha256sum", "install -m" };
        idempotentMarkers.Any(m => s.Contains(m)).Should().BeTrue("must use cmp / sha256sum / install for idempotent copy");
    }
}
