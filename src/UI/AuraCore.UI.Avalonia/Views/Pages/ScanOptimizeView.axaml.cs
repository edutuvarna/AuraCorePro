using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;

namespace AuraCore.UI.Avalonia.Views.Pages;

/// <summary>
/// Smart module view that adapts its UI based on module behavior.
/// Replaces GenericModuleView — every module gets a functional interface.
/// </summary>
public partial class ScanOptimizeView : UserControl
{
    private readonly IOptimizationModule _module;
    private ScanResult? _lastScan;
    private readonly ModuleBehavior _behavior = new("Optimize", false, "Module");

    // Per-module behavior configuration
    private record ModuleBehavior(
        string? ActionLabel,   // null = no action button
        bool ShowSpaceSaved,   // show bytes freed stat
        string Description     // what the action does
    );

    private static readonly Dictionary<string, ModuleBehavior> Behaviors = new()
    {
        ["junk-cleaner"]        = new("Clean",    true,  "Remove junk files and free disk space"),
        ["disk-cleanup"]        = new("Clean",    true,  "Deep clean Windows system files"),
        ["ram-optimizer"]       = new("Optimize", false, "Free up RAM by trimming working sets"),
        ["storage-compression"] = new("Compress", true,  "Compress files to save disk space"),
        ["registry-optimizer"]  = new("Fix",      false, "Fix broken and orphaned registry entries"),
        ["bloatware-removal"]   = new("Remove",   true,  "Remove pre-installed bloatware apps"),
        ["privacy-cleaner"]     = new("Clean",    true,  "Remove privacy traces and telemetry data"),
        ["battery-optimizer"]   = new("Apply",    false, "Apply battery-saving optimizations"),
        ["network-optimizer"]   = new("Apply",    false, "Apply network performance tweaks"),
        ["driver-updater"]      = new(null,       false, "Scan and view driver information"),
        ["context-menu"]        = new("Apply",    false, "Apply selected context menu tweaks"),
        ["taskbar-tweaks"]      = new("Apply",    false, "Apply selected taskbar customizations"),
        ["explorer-tweaks"]     = new("Apply",    false, "Apply File Explorer tweaks"),
        ["gaming-mode"]         = new("Activate", false, "Activate gaming performance mode"),
        ["app-installer"]       = new(null,       false, "Browse and install applications via WinGet"),
        ["defender-manager"]    = new(null,       false, "View Windows Defender status"),
    };

    private static ModuleBehavior GetBehavior(string moduleId) =>
        Behaviors.TryGetValue(moduleId, out var b) ? b : new("Optimize", false, "Run module optimization");

    public ScanOptimizeView() : this(null!) { }

    public ScanOptimizeView(IOptimizationModule module)
    {
        InitializeComponent();
        _module = module;
        if (module is null) return;

        _behavior = GetBehavior(module.Id);

        // Set page text
        PageTitle.Text = module.DisplayName;
        PageSubtitle.Text = _behavior.Description;
        ModuleInfo.Text = $"Module: {module.Id} | Risk: {module.Risk} | Platform: {module.Platform}";
        ModuleMeta.Text = $"{module.DisplayName} | {module.Category} | Risk: {module.Risk}";
        PlatformBadge.Text = module.Platform.ToString();

        // Configure action button based on behavior
        if (_behavior.ActionLabel is null)
        {
            OptimizeBtn.IsVisible = false;
        }
        else
        {
            OptimizeLabel.Text = _behavior.ActionLabel;
        }

        // Space saved stat visibility
        if (!_behavior.ShowSpaceSaved)
            SpaceSaved.Text = "N/A";

        Loaded += async (s, e) => { ApplyLocalization(); await RunScan(); };
        LocalizationService.LanguageChanged += OnLanguageChanged;
        Unloaded += (s, e) => LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged() =>
        global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);

    private void ApplyLocalization()
    {
        var L = LocalizationService._;
        ScanLabel.Text = L("common.scan");
        LblItemsFound.Text = L("scanOpt.itemsFound");
        LblSpace.Text = L("scanOpt.space");
        LblRisk.Text = L("scanOpt.risk");
        LblClickToScan.Text = L("scanOpt.clickToScan");
        if (string.IsNullOrEmpty(ProgressText.Text))
            ProgressText.Text = L("scanOpt.optimizing");
    }

    private async Task RunScan()
    {
        if (_module is null) return;
        ScanLabel.Text = LocalizationService._("common.scanning");
        ShowPanel("before");

        try
        {
            _lastScan = await _module.ScanAsync(new ScanOptions(DeepScan: true));

            ItemsFound.Text = _lastScan.ItemsFound.ToString();
            if (_behavior.ShowSpaceSaved)
                SpaceSaved.Text = FormatBytes(_lastScan.EstimatedBytesFreed);
            RiskLabel.Text = _module.Risk.ToString();

            if (_lastScan.Success && _lastScan.ItemsFound > 0)
            {
                // Customize result text based on module type
                ScanResultText.Text = _module.Id switch
                {
                    "driver-updater"  => $"Found {_lastScan.ItemsFound} drivers needing attention",
                    "app-installer"   => $"Found {_lastScan.ItemsFound} installed applications",
                    "context-menu"    => $"Found {_lastScan.ItemsFound} context menu tweaks available",
                    "taskbar-tweaks"  => $"Found {_lastScan.ItemsFound} taskbar tweaks available",
                    "explorer-tweaks" => $"Found {_lastScan.ItemsFound} explorer tweaks available",
                    "gaming-mode"     => $"Found {_lastScan.ItemsFound} background processes",
                    "ram-optimizer"   => $"Found {_lastScan.ItemsFound} processes using memory",
                    _ => $"Found {_lastScan.ItemsFound} items to {_behavior.ActionLabel?.ToLower() ?? "review"}"
                };

                ScanDetailText.Text = _module.Id switch
                {
                    "driver-updater"  => "Review driver details above. Use Windows Update for driver updates.",
                    "app-installer"   => "Use the WinUI3 version for full app management features.",
                    "gaming-mode"     => "Toggle gaming mode to suspend background processes and boost performance.",
                    _ when _behavior.ActionLabel is not null && _lastScan.EstimatedBytesFreed > 0
                        => $"Estimated savings: {FormatBytes(_lastScan.EstimatedBytesFreed)}. Click '{_behavior.ActionLabel}' to proceed.",
                    _ when _behavior.ActionLabel is not null
                        => $"Ready to {_behavior.ActionLabel.ToLower()}. Click the button to proceed.",
                    _ => "Scan complete. Review the results above."
                };

                if (_behavior.ActionLabel is not null)
                    OptimizeBtn.IsEnabled = true;

                ShowPanel("scan");
            }
            else if (_lastScan.Success)
            {
                ScanResultText.Text = _module.Id switch
                {
                    "driver-updater"  => "All drivers are up to date!",
                    "junk-cleaner"    => "System is clean - no junk files found!",
                    "disk-cleanup"    => "Disk is clean - nothing to remove!",
                    "registry-optimizer" => "Registry is healthy - no issues found!",
                    "privacy-cleaner" => "No privacy traces found!",
                    _ => "Everything looks good!"
                };
                ScanDetailText.Text = "No action needed. Your system is already optimized for this module.";
                OptimizeBtn.IsEnabled = false;
                ShowPanel("scan");
            }
            else
            {
                ScanResultText.Text = "Scan blocked";
                ScanDetailText.Text = _lastScan.BlockedReason ?? "This module requires administrator privileges.";
                ShowPanel("scan");
            }
        }
        catch (Exception ex)
        {
            ScanResultText.Text = "Scan failed";
            ScanDetailText.Text = ex.Message;
            ShowPanel("scan");
        }
        finally { ScanLabel.Text = LocalizationService._("common.scan"); }
    }

    private async Task RunOptimize()
    {
        if (_lastScan is null || !_lastScan.Success || _behavior.ActionLabel is null) return;

        OptimizeBtn.IsEnabled = false;
        OptimizeLabel.Text = LocalizationService._("common.working");
        ShowPanel("progress");

        try
        {
            var plan = new OptimizationPlan(_module.Id, new List<string> { "all" });
            var progress = new Progress<TaskProgress>(p =>
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    ProgressText.Text = $"{_behavior.ActionLabel}ing... {p.Percentage:F0}%";
                    ProgressBar.Width = 300 * p.Percentage / 100.0;
                    ProgressDetail.Text = p.StatusText;
                });
            });

            var result = await _module.OptimizeAsync(plan, progress);

            ResultTitle.Text = result.Success
                ? $"{_behavior.ActionLabel} Complete"
                : $"{_behavior.ActionLabel} Failed";

            var detail = $"Processed {result.ItemsProcessed} items in {result.Duration.TotalSeconds:F1}s.";
            if (result.BytesFreed > 0)
                detail += $" Freed {FormatBytes(result.BytesFreed)}.";
            ResultDetail.Text = result.Success ? detail
                : "Some items could not be processed. Try running as administrator.";

            ShowPanel("result");
        }
        catch (Exception ex)
        {
            ResultTitle.Text = "Error";
            ResultDetail.Text = ex.Message;
            ShowPanel("result");
        }
        finally
        {
            OptimizeLabel.Text = _behavior.ActionLabel;
            OptimizeBtn.IsEnabled = true;
        }
    }

    private void ShowPanel(string which)
    {
        BeforeScanPanel.IsVisible = which == "before";
        AfterScanPanel.IsVisible = which == "scan";
        ProgressPanel.IsVisible = which == "progress";
        ResultPanel.IsVisible = which == "result";
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScan();
    private async void Optimize_Click(object? sender, RoutedEventArgs e) => await RunOptimize();

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1073741824 => $"{bytes / 1073741824.0:F1} GB",
        >= 1048576    => $"{bytes / 1048576.0:F1} MB",
        >= 1024       => $"{bytes / 1024.0:F1} KB",
        > 0           => $"{bytes} B",
        _             => "0 B"
    };
}
