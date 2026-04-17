using System.Collections.Generic;
using System.Linq;
using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Localization;

/// <summary>
/// Phase 4.3.3 locale parity: every Docker Cleaner key resolves to a
/// non-empty translated string in both EN and TR. Guards against "key typo"
/// regressions (key added in one dict, forgotten in the other).
/// Mirrors SnapFlatpakCleanerLocaleParityTests / JournalCleanerLocaleParityTests.
/// </summary>
[Collection("Localization")]
public class DockerCleanerLocaleParityTests
{
    private static readonly string[] Keys = new[]
    {
        "nav.dockerCleaner",
        "dockerCleaner.subtitle",
        "dockerCleaner.stat.images",
        "dockerCleaner.stat.containers",
        "dockerCleaner.stat.volumes",
        "dockerCleaner.stat.buildCache",
        "dockerCleaner.containers.summary",
        "dockerCleaner.action.scan",
        "dockerCleaner.action.cancel",
        "dockerCleaner.safe.kicker",
        "dockerCleaner.safe.description",
        "dockerCleaner.safe.action",
        "dockerCleaner.granular.heading",
        "dockerCleaner.action.pruneImages",
        "dockerCleaner.action.pruneContainers",
        "dockerCleaner.action.pruneBuildCache",
        "dockerCleaner.danger.kicker",
        "dockerCleaner.warning.volumeDataLoss",
        "dockerCleaner.danger.acknowledge",
        "dockerCleaner.action.pruneVolumes",
        "dockerCleaner.status.idle",
        "dockerCleaner.status.scanning",
        "dockerCleaner.status.pruning",
        "dockerCleaner.status.done",
        "dockerCleaner.status.error",
        "dockerCleaner.status.unavailable",
        "dockerCleaner.warning.privilege",
    };

    public static IEnumerable<object[]> KeyList() => Keys.Select(k => new object[] { k });

    [Fact]
    public void DockerCleanerKeys_ExistInBothLocales()
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
