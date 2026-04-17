using System.Collections.Generic;
using System.Linq;
using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Localization;

/// <summary>
/// Phase 4.3.5 locale parity: every Linux App Installer key resolves to a
/// non-empty translated string in both EN and TR. Guards against "key typo"
/// regressions (key added in one dict, forgotten in the other).
/// Mirrors KernelCleanerLocaleParityTests / DockerCleanerLocaleParityTests.
/// </summary>
[Collection("Localization")]
public class LinuxAppInstallerLocaleParityTests
{
    private static readonly string[] Keys = new[]
    {
        "nav.linuxAppInstaller",
        "linuxAppInstaller.subtitle",
        "linuxAppInstaller.stat.total",
        "linuxAppInstaller.stat.totalValue",
        "linuxAppInstaller.stat.installed",
        "linuxAppInstaller.stat.available",
        "linuxAppInstaller.action.scan",
        "linuxAppInstaller.action.cancel",
        "linuxAppInstaller.search.placeholder",
        "linuxAppInstaller.bundle.summary",
        "linuxAppInstaller.app.installed",
        "linuxAppInstaller.selected.summary",
        "linuxAppInstaller.selected.breakdown",
        "linuxAppInstaller.action.installSelected",
        "linuxAppInstaller.status.idle",
        "linuxAppInstaller.status.scanning",
        "linuxAppInstaller.status.installing",
        "linuxAppInstaller.status.done",
        "linuxAppInstaller.status.error",
        "linuxAppInstaller.warning.privilege",
    };

    public static IEnumerable<object[]> KeyList() => Keys.Select(k => new object[] { k });

    [Fact]
    public void LinuxAppInstallerKeys_ExistInBothLocales()
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
