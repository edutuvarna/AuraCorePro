using global::Avalonia.Controls;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class GenericModuleView : UserControl
{
    public GenericModuleView() => InitializeComponent();

    public GenericModuleView(IOptimizationModule module) : this()
    {
        ModTitle.Text = module.DisplayName;
        ModSubtitle.Text = $"Category: {module.Category} | Risk: {module.Risk}";
        ModPlatform.Text = $"Platform: {module.Platform}";
        ModIcon.Text = GetCategoryIcon(module.Category);
    }

    private static string GetCategoryIcon(AuraCore.Domain.Enums.OptimizationCategory cat) => cat switch
    {
        AuraCore.Domain.Enums.OptimizationCategory.SystemHealth => "\u2764",
        AuraCore.Domain.Enums.OptimizationCategory.DiskCleanup => "\u1F9F9",
        AuraCore.Domain.Enums.OptimizationCategory.MemoryOptimization => "\u1F4BE",
        AuraCore.Domain.Enums.OptimizationCategory.NetworkOptimization => "\u1F310",
        AuraCore.Domain.Enums.OptimizationCategory.GamingPerformance => "\u1F3AE",
        AuraCore.Domain.Enums.OptimizationCategory.ShellCustomization => "\u2699",
        AuraCore.Domain.Enums.OptimizationCategory.AutorunManagement => "\u26A1",
        AuraCore.Domain.Enums.OptimizationCategory.ProcessManagement => "\u1F4CA",
        AuraCore.Domain.Enums.OptimizationCategory.NetworkTools => "\u1F310",
        _ => "\u2699"
    };
}
