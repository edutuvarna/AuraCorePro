using System.Collections.Generic;
using System.Linq;
using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Localization;

/// <summary>
/// Phase 4.4.1 locale parity: every DNS Flusher key resolves to a
/// non-empty translated string in both EN and TR. Mirrors
/// GrubManager / KernelCleaner / DockerCleaner / LinuxAppInstaller locale parity tests.
/// </summary>
[Collection("Localization")]
public class DnsFlusherLocaleParityTests
{
    private static readonly string[] Keys = new[]
    {
        "nav.dnsFlusher",
        "dnsFlusher.subtitle",
        "dnsFlusher.hero.description",
        "dnsFlusher.hero.action",
        "dnsFlusher.hero.flushing",
        "dnsFlusher.lastFlush.never",
        "dnsFlusher.lastFlush.prefix",
        "dnsFlusher.success",
        "dnsFlusher.relative.justNow",
        "dnsFlusher.relative.secondsAgo",
        "dnsFlusher.relative.minutesAgo",
        "dnsFlusher.relative.hoursAgo",
        "dnsFlusher.whenToUse.heading",
        "dnsFlusher.whenToUse.item1",
        "dnsFlusher.whenToUse.item2",
        "dnsFlusher.whenToUse.item3",
        "dnsFlusher.whenToUse.item4",
        "dnsFlusher.status.idle",
        "dnsFlusher.status.scanning",
        "dnsFlusher.status.error",
        "dnsFlusher.status.unavailable",
        "dnsFlusher.warning.privilege",
    };

    public static IEnumerable<object[]> KeyList() => Keys.Select(k => new object[] { k });

    [Fact]
    public void DnsFlusherKeys_ExistInBothLocales()
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
