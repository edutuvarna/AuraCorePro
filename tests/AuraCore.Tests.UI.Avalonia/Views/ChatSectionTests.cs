using AuraCore.UI.Avalonia.Views.Pages.AI;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Task 31 (spec §4.6 ChatSection): the chat header model chip is now a
/// SplitButton exposing a MenuFlyout. These tests verify the XAML wiring
/// compiles + renders correctly. The dropdown content itself (installed
/// models + "Download more..." entry) is populated at runtime from
/// IInstalledModelStore + IModelCatalog + AppSettings — covered by
/// InstalledModelStoreTests and ModelCatalogTests at the service layer.
/// </summary>
public class ChatSectionTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var section = new ChatSection();
        Assert.NotNull(section);
    }

    [AvaloniaFact]
    public void ModelChip_IsSplitButton_WithMenuFlyout()
    {
        // Previously the chip was a plain Button; Task 31 upgraded it to a
        // SplitButton so clicking the chevron opens the model-switcher menu.
        // This guards against accidental downgrade during future chat edits.
        var section = new ChatSection();
        using var handle = AvaloniaTestBase.RenderInWindow(section, 800, 600);

        var chip = section.FindControl<SplitButton>("ModelChip");
        Assert.NotNull(chip);
        Assert.NotNull(chip!.Flyout);
        Assert.IsType<MenuFlyout>(chip.Flyout);
    }

    [AvaloniaFact]
    public void ModelChip_InitialContent_IsPlaceholder_WhenNoActiveModel()
    {
        // The initial chip label is "⚙ No model selected ▾" until BuildModelMenu
        // runs in OnLoaded and discovers installed models. If no model is
        // active (default AppSettings state), the chip keeps the placeholder.
        var section = new ChatSection();
        using var handle = AvaloniaTestBase.RenderInWindow(section, 800, 600);

        var chip = section.FindControl<SplitButton>("ModelChip");
        Assert.NotNull(chip);
        Assert.NotNull(chip!.Content);
        var content = chip.Content?.ToString() ?? "";
        // Either the XAML placeholder OR the BuildModelMenu-applied placeholder —
        // both variants contain "No model selected".
        Assert.Contains("No model selected", content);
    }
}
