using System.Collections.Generic;
using System.Linq;
using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Localization;

/// <summary>
/// Phase 4.4.2 locale parity: every Purgeable Space Manager key resolves to a
/// non-empty translated string in both EN and TR. Mirrors DnsFlusher /
/// GrubManager / KernelCleaner / DockerCleaner locale parity tests.
/// </summary>
[Collection("Localization")]
public class PurgeableSpaceLocaleParityTests
{
    private static readonly string[] Keys = new[]
    {
        "nav.purgeableSpace",
        "purgeableSpace.subtitle",
        "purgeableSpace.breakdown.heading",
        "purgeableSpace.legend.used",
        "purgeableSpace.legend.purgeable",
        "purgeableSpace.legend.free",
        "purgeableSpace.stat.purgeable",
        "purgeableSpace.stat.snapshots",
        "purgeableSpace.education.heading",
        "purgeableSpace.education.body1",
        "purgeableSpace.education.body2",
        "purgeableSpace.actions.heading",
        "purgeableSpace.action.cleanCaches",
        "purgeableSpace.action.cleanCaches.hint",
        "purgeableSpace.action.runPeriodic",
        "purgeableSpace.action.runPeriodic.hint",
        "purgeableSpace.action.thinSnapshots",
        "purgeableSpace.action.thinSnapshots.description",
        "purgeableSpace.status.idle",
        "purgeableSpace.status.scanning",
        "purgeableSpace.status.cleaning",
        "purgeableSpace.status.done",
        "purgeableSpace.status.error",
        "purgeableSpace.status.unavailable",
        "purgeableSpace.warning.privilege",
        "purgeableSpace.action.cancel",
    };

    public static IEnumerable<object[]> KeyList() => Keys.Select(k => new object[] { k });

    [Fact]
    public void PurgeableSpaceKeys_ExistInBothLocales()
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
