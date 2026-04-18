using global::Avalonia.Controls;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class GenericModuleView : UserControl
{
    private IOptimizationModule? _module;

    public GenericModuleView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        Unloaded += (s, e) =>
            LocalizationService.LanguageChanged -= () =>
                global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    public GenericModuleView(IOptimizationModule module) : this()
    {
        _module = module;
        ModTitle.Text = module.DisplayName;
        ModSubtitle.Text = $"{LocalizationService._("genMod.category")}: {module.Category} | {LocalizationService._("genMod.risk")}: {module.Risk}";
        ModPlatform.Text = $"{LocalizationService._("genMod.platform")}: {module.Platform}";
        ModIcon.Text = GetCategoryIcon(module.Category);
    }

    private void ApplyLocalization()
    {
        var L = LocalizationService._;
        ModStatus.Text       = L("genMod.loadedOk");
        ModPortingNote.Text  = L("genMod.portingNote");
        if (_module is not null)
        {
            ModSubtitle.Text = $"{L("genMod.category")}: {_module.Category} | {L("genMod.risk")}: {_module.Risk}";
            ModPlatform.Text = $"{L("genMod.platform")}: {_module.Platform}";
        }
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
