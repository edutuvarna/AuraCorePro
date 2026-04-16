using System.Collections.Generic;
using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Localization;

/// <summary>
/// Phase 4.3.1 locale parity: every Journal Cleaner key resolves to a non-empty
/// translated string in both EN and TR. Guards against "key typo" regressions
/// (key added in one dict, forgotten in the other).
/// </summary>
[Collection("Localization")]
public class JournalCleanerLocaleParityTests
{
    private static readonly string[] Keys = new[]
    {
        "nav.journalCleaner",
        "journalCleaner.subtitle",
        "journalCleaner.stat.usage",
        "journalCleaner.stat.files",
        "journalCleaner.stat.oldest",
        "journalCleaner.action.scan",
        "journalCleaner.action.cancel",
        "journalCleaner.action.vacuum500m",
        "journalCleaner.action.vacuum1g",
        "journalCleaner.action.vacuum7days",
        "journalCleaner.action.vacuum30days",
        "journalCleaner.status.idle",
        "journalCleaner.status.scanning",
        "journalCleaner.status.vacuuming",
        "journalCleaner.status.done",
        "journalCleaner.status.error",
        "journalCleaner.status.unavailable",
        "journalCleaner.warning.privilege",
    };

    public static IEnumerable<object[]> KeyList() => Keys.Select(k => new object[] { k });

    [Fact]
    public void JournalCleanerKeys_ExistInBothLocales()
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
