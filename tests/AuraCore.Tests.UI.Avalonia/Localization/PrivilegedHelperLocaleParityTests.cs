using System.Collections.Generic;
using System.Linq;
using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Localization;

/// <summary>
/// Task A2 locale parity: every privhelper key resolves to a non-empty translated
/// string in both EN and TR. Mirrors the pattern of DnsFlusherLocaleParityTests.
/// </summary>
[Collection("Localization")]
public class PrivilegedHelperLocaleParityTests
{
    private static readonly string[] Keys = new[]
    {
        "privhelper.dialog.title",
        "privhelper.dialog.intro",
        "privhelper.dialog.installLabel",
        "privhelper.dialog.cancelLabel",
        "privhelper.dialog.installing",
        "privhelper.dialog.success",
        "privhelper.dialog.userCancelled",
        "privhelper.dialog.timeout",
        "privhelper.dialog.failed",
        "privhelper.notInstalled.toast",
    };

    public static IEnumerable<object[]> KeyList() => Keys.Select(k => new object[] { k });

    [Fact]
    public void PrivHelperKeys_ExistInBothLocales()
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
