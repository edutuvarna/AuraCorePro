using System.Collections.Generic;
using System.Linq;
using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Localization;

/// <summary>
/// Phase 4.4.5 locale parity: every Mac App Installer key resolves to a
/// non-empty translated string in both EN and TR. Guards against "key typo"
/// regressions (key added in one dict, forgotten in the other).
/// Mirrors LinuxAppInstallerLocaleParityTests.
/// </summary>
[Collection("Localization")]
public class MacAppInstallerLocaleParityTests
{
    private static readonly string[] Keys = new[]
    {
        "nav.macAppInstaller",
        "macAppInstaller.subtitle",
        "macAppInstaller.stat.total",
        "macAppInstaller.stat.totalValue",
        "macAppInstaller.stat.installed",
        "macAppInstaller.stat.available",
        "macAppInstaller.action.scan",
        "macAppInstaller.action.cancel",
        "macAppInstaller.search.placeholder",
        "macAppInstaller.bundle.summary",
        "macAppInstaller.app.installed",
        "macAppInstaller.source.formula",
        "macAppInstaller.source.cask",
        "macAppInstaller.selected.summary",
        "macAppInstaller.selected.breakdown",
        "macAppInstaller.action.installSelected",
        "macAppInstaller.status.idle",
        "macAppInstaller.status.scanning",
        "macAppInstaller.status.installing",
        "macAppInstaller.status.done",
        "macAppInstaller.status.error",
        "macAppInstaller.status.brewMissing",
        "macAppInstaller.warning.privilege",
    };

    public static IEnumerable<object[]> KeyList() => Keys.Select(k => new object[] { k });

    [Fact]
    public void MacAppInstallerKeys_ExistInBothLocales()
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
