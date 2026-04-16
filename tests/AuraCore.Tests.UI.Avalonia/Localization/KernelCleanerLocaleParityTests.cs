using System.Collections.Generic;
using System.Linq;
using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Localization;

/// <summary>
/// Phase 4.3.4 locale parity: every Kernel Cleaner key resolves to a
/// non-empty translated string in both EN and TR. Guards against "key typo"
/// regressions (key added in one dict, forgotten in the other).
/// Mirrors DockerCleanerLocaleParityTests / SnapFlatpakCleanerLocaleParityTests.
/// </summary>
[Collection("Localization")]
public class KernelCleanerLocaleParityTests
{
    private static readonly string[] Keys = new[]
    {
        "nav.kernelCleaner",
        "kernelCleaner.subtitle",
        "kernelCleaner.stat.active",
        "kernelCleaner.stat.removable",
        "kernelCleaner.removable.summary",
        "kernelCleaner.action.scan",
        "kernelCleaner.action.cancel",
        "kernelCleaner.safe.kicker",
        "kernelCleaner.safe.description",
        "kernelCleaner.safe.action",
        "kernelCleaner.manual.heading",
        "kernelCleaner.badge.running",
        "kernelCleaner.badge.newest",
        "kernelCleaner.selected.summary",
        "kernelCleaner.selected.none",
        "kernelCleaner.action.removeSelected",
        "kernelCleaner.danger.kicker",
        "kernelCleaner.warning.noFallback",
        "kernelCleaner.danger.acknowledge",
        "kernelCleaner.action.removeAllButRunning",
        "kernelCleaner.status.idle",
        "kernelCleaner.status.scanning",
        "kernelCleaner.status.removing",
        "kernelCleaner.status.done",
        "kernelCleaner.status.error",
        "kernelCleaner.status.unavailable",
        "kernelCleaner.warning.privilege",
    };

    public static IEnumerable<object[]> KeyList() => Keys.Select(k => new object[] { k });

    [Fact]
    public void KernelCleanerKeys_ExistInBothLocales()
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
