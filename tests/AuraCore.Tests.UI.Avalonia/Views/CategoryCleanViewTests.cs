using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.0: CategoryCleanView migrated to ModuleHeader + GlassCard shell.
/// Phase 6.16.C: ctor now requires a non-null IOptimizationModule (gating done
/// upstream by ModuleNavigator); tests inject a stub module.
/// </summary>
public class CategoryCleanViewTests
{
    private sealed class StubCategoryModule : IOptimizationModule
    {
        public string Id => "junk-cleaner";
        public string DisplayName => "Junk Cleaner";
        public OptimizationCategory Category => OptimizationCategory.SystemHealth;
        public RiskLevel Risk => RiskLevel.None;
        public SupportedPlatform Platform => SupportedPlatform.All;
        public Task<ScanResult> ScanAsync(ScanOptions o, CancellationToken ct = default)
            => Task.FromResult(new ScanResult(Id, true, 0, 0));
        public Task<OptimizationResult> OptimizeAsync(OptimizationPlan p, IProgress<TaskProgress>? pr = null, CancellationToken ct = default)
            => Task.FromResult(new OptimizationResult(Id, "op", true, 0, 0, TimeSpan.Zero));
        public Task<bool> CanRollbackAsync(string id, CancellationToken ct = default) => Task.FromResult(false);
        public Task RollbackAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static CategoryCleanView NewView() => new CategoryCleanView(new StubCategoryModule());

    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = NewView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_RunsJobs_DoesNotCrash()
    {
        var v = NewView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader()
    {
        var v = NewView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
    }

    [AvaloniaFact]
    public void Layout_UsesGlassCard()
    {
        var v = NewView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var cards = v.GetVisualDescendants().OfType<GlassCard>().ToList();
        Assert.NotEmpty(cards);
    }
}
