using System.Collections.Generic;
using System.Linq;
using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Localization;

/// <summary>
/// Phase 4.4.3 locale parity: every Spotlight Manager key resolves to a
/// non-empty translated string in both EN and TR. Mirrors DnsFlusher /
/// PurgeableSpace / KernelCleaner locale parity tests.
/// </summary>
[Collection("Localization")]
public class SpotlightLocaleParityTests
{
    private static readonly string[] Keys = new[]
    {
        "nav.spotlightManager",
        "spotlight.subtitle",
        "spotlight.stat.volumes",
        "spotlight.stat.indexed",
        "spotlight.stat.disabled",
        "spotlight.volumes.heading",
        "spotlight.row.indexing.enabled",
        "spotlight.row.indexing.disabled",
        "spotlight.badge.indexed",
        "spotlight.badge.off",
        "spotlight.action.rebuild",
        "spotlight.rebuild.confirm.title",
        "spotlight.rebuild.confirm.body",
        "spotlight.rebuild.confirm.cancel",
        "spotlight.rebuild.confirm.proceed",
        "spotlight.empty",
        "spotlight.about.heading",
        "spotlight.about.body1",
        "spotlight.about.body2",
        "spotlight.status.idle",
        "spotlight.status.scanning",
        "spotlight.status.toggling",
        "spotlight.status.rebuilding",
        "spotlight.status.done",
        "spotlight.status.error",
        "spotlight.status.unavailable",
        "spotlight.action.cancel",
        "spotlight.warning.privilege",
    };

    public static IEnumerable<object[]> KeyList() => Keys.Select(k => new object[] { k });

    [Fact]
    public void SpotlightKeys_ExistInBothLocales()
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
