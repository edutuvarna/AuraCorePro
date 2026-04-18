using System.Collections.Generic;
using AuraCore.UI.Avalonia.Helpers;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Helpers;

public class UrlSchemeHandlerTests
{
    private static readonly HashSet<string> KnownSections = new(System.StringComparer.Ordinal)
    {
        "dashboard", "settings", "disk-health",
        "ai-recommendations", "ai-insights", "ai-schedule"
    };

    private static readonly HashSet<string> KnownModules = new(System.StringComparer.Ordinal)
    {
        "driver-updater", "defender-manager", "service-manager", "junk-cleaner"
    };

    // ── Happy paths ──────────────────────────────────────────────

    [Theory]
    [InlineData("auracore://dashboard",          "dashboard")]
    [InlineData("auracore://disk-health",        "disk-health")]
    [InlineData("auracore://ai-recommendations", "ai-recommendations")]
    [InlineData("auracore://ai-insights",        "ai-insights")]
    [InlineData("auracore://ai-schedule",        "ai-schedule")]
    [InlineData("auracore://settings",           "settings")]
    public void Parse_known_section_returns_Section_intent(string url, string expectedId)
    {
        var result = UrlSchemeHandler.Parse(url, KnownSections, KnownModules);
        Assert.NotNull(result);
        Assert.Equal(IntentKind.Section, result!.Kind);
        Assert.Equal(expectedId, result.Id);
    }

    [Theory]
    [InlineData("auracore://module/driver-updater",   "driver-updater")]
    [InlineData("auracore://module/defender-manager", "defender-manager")]
    [InlineData("auracore://module/service-manager",  "service-manager")]
    [InlineData("auracore://module/junk-cleaner",     "junk-cleaner")]
    public void Parse_known_module_returns_Module_intent(string url, string expectedId)
    {
        var result = UrlSchemeHandler.Parse(url, KnownSections, KnownModules);
        Assert.NotNull(result);
        Assert.Equal(IntentKind.Module, result!.Kind);
        Assert.Equal(expectedId, result.Id);
    }

    // ── Null / empty / malformed ─────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("auracore://")]
    [InlineData("auracore:///")]
    [InlineData("auracore:///disk-health")]
    [InlineData("auracore:/disk-health")]
    [InlineData("://disk-health")]
    [InlineData("notauracore://disk-health")]
    public void Parse_null_or_malformed_returns_null(string? input)
    {
        var result = UrlSchemeHandler.Parse(input, KnownSections, KnownModules);
        Assert.Null(result);
    }

    // ── Unknown IDs ──────────────────────────────────────────────

    [Theory]
    [InlineData("auracore://unknown-section")]
    [InlineData("auracore://module/unknown-module")]
    [InlineData("auracore://module/")]
    [InlineData("auracore://module")]
    [InlineData("auracore://DASHBOARD")]
    [InlineData("auracore://Dashboard")]
    [InlineData("auracore://MODULE/driver-updater")]
    public void Parse_unknown_id_returns_null(string url)
    {
        var result = UrlSchemeHandler.Parse(url, KnownSections, KnownModules);
        Assert.Null(result);
    }

    // ── Shape rejection: 3+ segments ─────────────────────────────

    [Theory]
    [InlineData("auracore://disk-health/extra")]
    [InlineData("auracore://ai-insights/sub/page")]
    [InlineData("auracore://module/driver-updater/action")]
    [InlineData("auracore://a/b/c/d/e")]
    public void Parse_three_or_more_segments_returns_null(string url)
    {
        var result = UrlSchemeHandler.Parse(url, KnownSections, KnownModules);
        Assert.Null(result);
    }

    // ── Security: path traversal ─────────────────────────────────

    [Theory]
    [InlineData("auracore://../../system32")]
    [InlineData("auracore://..")]
    [InlineData("auracore://..%2f..%2fsystem32")]
    [InlineData("auracore://%2e%2e/%2e%2e/etc/passwd")]
    [InlineData("auracore://disk-health/../settings")]
    public void Parse_path_traversal_returns_null(string url)
    {
        var result = UrlSchemeHandler.Parse(url, KnownSections, KnownModules);
        Assert.Null(result);
    }

    // ── Security: null byte + unicode ────────────────────────────

    [Theory]
    [InlineData("auracore://disk-health\0evil")]
    [InlineData("auracore://disk-health%00evil")]
    [InlineData("auracore://ai\u2024insights")]
    [InlineData("auracore://ai\u00adinsights")]
    public void Parse_malicious_characters_returns_null(string url)
    {
        var result = UrlSchemeHandler.Parse(url, KnownSections, KnownModules);
        Assert.Null(result);
    }

    // ── Security: whitespace around URL ──────────────────────────

    [Theory]
    [InlineData("  auracore://disk-health  ")]
    [InlineData("\tauracore://disk-health")]
    [InlineData("auracore://disk-health\n")]
    public void Parse_whitespace_in_url_returns_null(string url)
    {
        var result = UrlSchemeHandler.Parse(url, KnownSections, KnownModules);
        Assert.Null(result);
    }

    // ── Security: scheme confusion ───────────────────────────────

    [Theory]
    [InlineData("javascript:auracore://disk-health")]
    [InlineData("data:auracore://disk-health")]
    [InlineData("file:///C:/auracore://disk-health")]
    public void Parse_scheme_confusion_returns_null(string url)
    {
        var result = UrlSchemeHandler.Parse(url, KnownSections, KnownModules);
        Assert.Null(result);
    }

    // ── Whitelist-empty edge case ────────────────────────────────

    [Fact]
    public void Parse_with_empty_whitelists_always_returns_null()
    {
        var empty = new HashSet<string>();
        var result = UrlSchemeHandler.Parse("auracore://disk-health", empty, empty);
        Assert.Null(result);
    }
}
