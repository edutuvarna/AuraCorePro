using System.Collections.Generic;
using System.Linq;
using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Localization;

/// <summary>
/// Phase 4.3.2 locale parity: every Snap/Flatpak Cleaner key resolves to a
/// non-empty translated string in both EN and TR. Guards against "key typo"
/// regressions (key added in one dict, forgotten in the other).
/// Mirrors JournalCleanerLocaleParityTests.
/// </summary>
[Collection("Localization")]
public class SnapFlatpakCleanerLocaleParityTests
{
    private static readonly string[] Keys = new[]
    {
        "nav.snapFlatpakCleaner",
        "snapFlatpakCleaner.subtitle",
        "snapFlatpakCleaner.stat.snap",
        "snapFlatpakCleaner.stat.flatpak",
        "snapFlatpakCleaner.action.scan",
        "snapFlatpakCleaner.action.cancel",
        "snapFlatpakCleaner.action.cleanSnap",
        "snapFlatpakCleaner.action.cleanFlatpak",
        "snapFlatpakCleaner.action.cleanBoth",
        "snapFlatpakCleaner.status.idle",
        "snapFlatpakCleaner.status.scanning",
        "snapFlatpakCleaner.status.cleaning",
        "snapFlatpakCleaner.status.done",
        "snapFlatpakCleaner.status.error",
        "snapFlatpakCleaner.status.unavailable",
        "snapFlatpakCleaner.warning.privilege",
    };

    public static IEnumerable<object[]> KeyList() => Keys.Select(k => new object[] { k });

    [Fact]
    public void SnapFlatpakCleanerKeys_ExistInBothLocales()
    {
        // EN pass
        LocalizationService.SetLanguage("en");
        foreach (var key in Keys)
        {
            var v = LocalizationService._(key);
            Assert.False(string.IsNullOrWhiteSpace(v), $"EN missing: {key}");
            Assert.NotEqual(key, v); // falling back to key = unresolved
        }

        // TR pass
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
