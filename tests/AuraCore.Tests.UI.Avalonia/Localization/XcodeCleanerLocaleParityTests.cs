using System.Collections.Generic;
using System.Linq;
using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Localization;

/// <summary>
/// Phase 4.4.4 locale parity: every Xcode Cleaner key resolves to a
/// non-empty translated string in both EN and TR. Mirrors DnsFlusher /
/// PurgeableSpace / Spotlight locale parity tests.
/// </summary>
[Collection("Localization")]
public class XcodeCleanerLocaleParityTests
{
    private static readonly string[] Keys = new[]
    {
        "nav.xcodeCleaner",
        "xcodeCleaner.subtitle",
        "xcodeCleaner.stat.total",
        "xcodeCleaner.stat.categories",
        "xcodeCleaner.stat.categoriesValue",
        "xcodeCleaner.stat.oldest",
        "xcodeCleaner.safe.kicker",
        "xcodeCleaner.safe.description",
        "xcodeCleaner.safe.action",
        "xcodeCleaner.granular.heading",
        "xcodeCleaner.granular.note",
        "xcodeCleaner.category.derivedData",
        "xcodeCleaner.category.archives",
        "xcodeCleaner.category.simulatorCaches",
        "xcodeCleaner.category.simulatorDevices",
        "xcodeCleaner.category.xcodeCache",
        "xcodeCleaner.category.iosDeviceSupport",
        "xcodeCleaner.category.watchosDeviceSupport",
        "xcodeCleaner.category.tvosDeviceSupport",
        "xcodeCleaner.category.unavailableSimulators",
        "xcodeCleaner.action.prune",
        "xcodeCleaner.danger.kicker",
        "xcodeCleaner.danger.warning",
        "xcodeCleaner.danger.acknowledge",
        "xcodeCleaner.danger.pruneAll",
        "xcodeCleaner.status.idle",
        "xcodeCleaner.status.scanning",
        "xcodeCleaner.status.pruning",
        "xcodeCleaner.status.done",
        "xcodeCleaner.status.error",
        "xcodeCleaner.status.unavailable",
        "xcodeCleaner.about.heading",
        "xcodeCleaner.about.item1",
        "xcodeCleaner.about.item2",
        "xcodeCleaner.about.item3",
        "xcodeCleaner.about.item4",
        "xcodeCleaner.about.item5",
        "xcodeCleaner.action.cancel",
    };

    public static IEnumerable<object[]> KeyList() => Keys.Select(k => new object[] { k });

    [Fact]
    public void XcodeCleanerKeys_ExistInBothLocales()
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
