using AuraCore.UI.Avalonia.Views.Pages.AI;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Regression tests for ScheduleSection. The initial Phase 3 implementation
/// used <c>(IBrush)this.FindResource("TextPrimaryBrush")!</c> in
/// <c>BuildCards()</c>, which crashed at runtime with
/// <c>InvalidCastException: UnsetValueType → IBrush</c> because
/// AuraCoreThemeV2 brushes live under <c>ThemeDictionaries.Dark</c> and the
/// default <c>FindResource</c> uses <c>ThemeVariant.Default</c>. The hotfix
/// applied the same theme-variant-aware <c>FindBrush</c> pattern as Phase 2's
/// MainWindow fix (commit <c>442518f</c>). These tests guard against
/// reintroducing the regression.
/// </summary>
public class ScheduleSectionTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var section = new ScheduleSection();
        Assert.NotNull(section);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash_OnLoaded()
    {
        // Attaches to visual tree, triggers Loaded event which calls BuildCards.
        // Before hotfix: InvalidCastException on (IBrush)this.FindResource(...)
        // After hotfix: FindBrush returns theme-variant-resolved brush or fallback.
        var section = new ScheduleSection();
        using var handle = AvaloniaTestBase.RenderInWindow(section, 800, 600);
        // If BuildCards ran successfully, TaskList has the 6 hardcoded cards
        // (junk, ram, registry, privacy, disk, health). If Loaded didn't fire
        // in headless mode, TaskList is empty — but the absence of crash is
        // itself the regression guard.
        var taskList = section.FindControl<StackPanel>("TaskList");
        Assert.NotNull(taskList);
    }

    [AvaloniaFact]
    public void TaskList_PopulatedWith6Cards_AfterLoad()
    {
        var section = new ScheduleSection();
        using var handle = AvaloniaTestBase.RenderInWindow(section, 800, 600);

        // Avalonia.Headless defers Loaded to the dispatcher — pump it so
        // BuildCards runs synchronously before assertion.
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var taskList = section.FindControl<StackPanel>("TaskList");
        Assert.NotNull(taskList);
        // TaskDefs has 6 entries (junk, ram, registry, privacy, disk, health)
        // — BuildCards constructs one card Border per entry.
        Assert.Equal(6, taskList!.Children.Count);
    }
}
