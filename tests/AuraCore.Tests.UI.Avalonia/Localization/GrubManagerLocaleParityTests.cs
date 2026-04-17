using System.Collections.Generic;
using System.Linq;
using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Localization;

/// <summary>
/// Phase 4.3.6 locale parity: every GRUB Manager key resolves to a
/// non-empty translated string in both EN and TR. Mirrors
/// KernelCleaner / DockerCleaner / LinuxAppInstaller locale parity tests.
/// </summary>
[Collection("Localization")]
public class GrubManagerLocaleParityTests
{
    private static readonly string[] Keys = new[]
    {
        "nav.grubManager",
        "grubManager.subtitle",
        "grubManager.backup.kicker",
        "grubManager.backup.exists",
        "grubManager.backup.missing",
        "grubManager.action.rollback",
        "grubManager.action.cancel",
        "grubManager.settings.heading",
        "grubManager.timeout.label",
        "grubManager.timeout.help",
        "grubManager.timeout.diff",
        "grubManager.timeout.unchanged",
        "grubManager.default.label",
        "grubManager.default.help",
        "grubManager.default.diff",
        "grubManager.default.unchanged",
        "grubManager.default.entry",
        "grubManager.default.saved",
        "grubManager.osProber.label",
        "grubManager.osProber.help",
        "grubManager.osProber.enabled",
        "grubManager.osProber.disabled",
        "grubManager.kernels.heading",
        "grubManager.kernels.runningSuffix",
        "grubManager.kernels.helperText",
        "grubManager.pending.kicker",
        "grubManager.pending.helpText",
        "grubManager.pending.acknowledge",
        "grubManager.action.reset",
        "grubManager.action.apply",
        "grubManager.change.timeout",
        "grubManager.change.default",
        "grubManager.change.osProber",
        "grubManager.status.idle",
        "grubManager.status.scanning",
        "grubManager.status.applying",
        "grubManager.status.rollback",
        "grubManager.status.done",
        "grubManager.status.error",
        "grubManager.status.unavailable",
        "grubManager.warning.bootRisk",
    };

    public static IEnumerable<object[]> KeyList() => Keys.Select(k => new object[] { k });

    [Fact]
    public void GrubManagerKeys_ExistInBothLocales()
    {
        LocalizationService.SetLanguage("en");
        foreach (var key in Keys)
        {
            var v = LocalizationService._(key);
            Assert.False(string.IsNullOrWhiteSpace(v), $"EN missing: {key}");
            Assert.NotEqual(key, v);
        }

        LocalizationService.SetLanguage("tr");
        foreach (var key in Keys)
        {
            var v = LocalizationService._(key);
            Assert.False(string.IsNullOrWhiteSpace(v), $"TR missing: {key}");
            Assert.NotEqual(key, v);
        }

        // Reset so other tests in the collection aren't surprised
        LocalizationService.SetLanguage("en");
    }

    [Theory]
    [MemberData(nameof(KeyList))]
    public void EachKey_ResolvesInEnglish(string key)
    {
        LocalizationService.SetLanguage("en");
        var value = LocalizationService._(key);
        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.NotEqual(key, value);
    }

    [Theory]
    [MemberData(nameof(KeyList))]
    public void EachKey_ResolvesInTurkish(string key)
    {
        LocalizationService.SetLanguage("tr");
        var value = LocalizationService._(key);
        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.NotEqual(key, value);
    }
}
