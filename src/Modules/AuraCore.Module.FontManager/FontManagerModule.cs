using System.Diagnostics;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.Module.FontManager;

public class FontManagerModule : IOptimizationModule
{
    public string Id => "font-manager";
    public string DisplayName => "Font Manager";
    public OptimizationCategory Category => OptimizationCategory.ShellCustomization;
    public RiskLevel Risk => RiskLevel.Low;
    public SupportedPlatform Platform => SupportedPlatform.Windows;

    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
        => Task.FromResult(new ScanResult(Id, true, 0, 0));

    public Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
        => Task.FromResult(new OptimizationResult(Id, Guid.NewGuid().ToString()[..8], true, 0, 0, TimeSpan.Zero));

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;
}

public static class FontManagerRegistration
{
    public static void AddFontManagerModule(IServiceCollection sc)
        => sc.AddSingleton<IOptimizationModule, FontManagerModule>();
}
