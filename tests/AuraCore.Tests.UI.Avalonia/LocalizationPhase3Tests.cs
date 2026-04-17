using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia;

[Collection("Localization")]
public class LocalizationPhase3Tests
{
    private static readonly string[] Phase3Keys = new[]
    {
        "nav.aiFeatures.title", "aiFeatures.hero.title", "aiFeatures.hero.status.active",
        "aiFeatures.hero.status.paused", "aiFeatures.hero.status.ready",
        "aiFeatures.card.chat.experimentalBadge", "aiFeatures.chat.warningBanner",
        "chatOptIn.step1.title", "chatOptIn.step1.continueButton", "chatOptIn.step2.title",
        "modelManager.title.optIn", "modelManager.title.manage",
        "modelManager.tier.lite", "modelManager.tier.standard", "modelManager.tier.advanced", "modelManager.tier.heavy",
        "modelManager.speed.fast", "modelManager.speed.medium", "modelManager.speed.slow",
        "modelManager.recommended", "modelManager.downloadButton",
        "modelManager.model.tinyllama.description",
        "modelManager.model.phi3-mini-q4km.description",
        "modelDownload.error.network", "modelDownload.error.blocked",
        "tier.lockedTooltip", "tier.upgrade.dialog.title",
        "nav.module.system-health", "nav.module.admin-panel",
    };

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void EachKey_ResolvesInEnglish(string key)
    {
        LocalizationService.SetLanguage("en");
        var value = LocalizationService._(key);
        Assert.NotEqual(key, value);
        Assert.False(string.IsNullOrWhiteSpace(value));
    }

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void EachKey_ResolvesInTurkish(string key)
    {
        LocalizationService.SetLanguage("tr");
        var value = LocalizationService._(key);
        Assert.NotEqual(key, value);
        Assert.False(string.IsNullOrWhiteSpace(value));
    }

    public static IEnumerable<object[]> AllKeys =>
        Phase3Keys.Select(k => new object[] { k });
}
