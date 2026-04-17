using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AuraCore.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels.Dashboard;
using AuraCore.UI.Avalonia.Views.Controls;

namespace AuraCore.UI.Avalonia.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private double _cpu, _ram, _disk, _gpu;
    private double _healthScore = 100.0;
    private string _healthLabel = "Excellent";
    private GpuInfo? _gpuInfo;
    private string _osName = "";
    private string _cpuName = "";
    private double _ramTotalGb;
    private int _cortexDaysActive = 0;
    private bool _cortexOn = true;

    // Phase 3 Task 32: ripple state driven by ambient + settings
    private readonly ICortexAmbientService? _ambient;
    private readonly AppSettings? _settings;

    public event PropertyChangedEventHandler? PropertyChanged;

    // Delegates injected from the View (code-behind) to execute real module logic.
    // Null-safe: tiles are initialized with no-op stubs so the Dashboard renders
    // even in design-time or test contexts where modules are unavailable.
    private Func<Task>? _executeQuickCleanup;
    private Func<Task>? _executeOptimizeRam;
    private Func<Task>? _executeRemoveBloat;

    /// <summary>
    /// Data-bound Quick Action tiles. Initialized with stub delegates;
    /// call <see cref="InitQuickActions"/> once real module delegates are available.
    /// </summary>
    public IReadOnlyList<QuickActionTileVM> QuickActions { get; private set; }
        = System.Array.Empty<QuickActionTileVM>();

    /// <summary>
    /// Primary ctor. Both parameters optional so existing call sites
    /// (<c>new DashboardViewModel()</c> in DashboardView field init + tests)
    /// keep working without ambient wiring. When both are supplied, the VM
    /// subscribes to ambient events and fires PropertyChanged on ripple
    /// properties whenever a feature toggle flips.
    /// </summary>
    public DashboardViewModel(ICortexAmbientService? ambient = null, AppSettings? settings = null)
    {
        _ambient = ambient;
        _settings = settings;
        if (_ambient is not null)
        {
            _ambient.PropertyChanged += OnAmbientPropertyChanged;
        }
        // Initialize tiles with no-op stubs so bindings are never null.
        // DashboardView.Loaded calls InitQuickActions() with real delegates.
        QuickActions = QuickActionPresets.Windows(
            quickCleanup: () => Task.CompletedTask,
            optimizeRam:  () => Task.CompletedTask,
            removeBloat:  () => Task.CompletedTask);
    }

    /// <summary>
    /// Replaces the stub delegates with real module execution delegates.
    /// Called from <see cref="DashboardView"/> once modules are resolved from DI.
    /// </summary>
    public void InitQuickActions(Func<Task> quickCleanup, Func<Task> optimizeRam, Func<Task> removeBloat)
    {
        _executeQuickCleanup = quickCleanup;
        _executeOptimizeRam  = optimizeRam;
        _executeRemoveBloat  = removeBloat;
        Func<Task> noOp = () => Task.CompletedTask;
        QuickActions = QuickActionPresets.Windows(
            quickCleanup: () => (_executeQuickCleanup ?? noOp)(),
            optimizeRam:  () => (_executeOptimizeRam  ?? noOp)(),
            removeBloat:  () => (_executeRemoveBloat  ?? noOp)());
        OnChanged(nameof(QuickActions));
    }

    private void OnAmbientPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Fire all ripple-dependent properties. Bindings dedupe by value so
        // firing unconditionally is cheap and avoids missing transitive changes
        // (e.g., ambient only fires AnyFeatureEnabled but UI also needs
        // InsightsEnabled → ShowCortexInsightsCard).
        OnChanged(nameof(ShowCortexInsightsCard));
        OnChanged(nameof(ShowCortexSubtitle));
        OnChanged(nameof(CortexChipState));
        OnChanged(nameof(CortexChipLabel));
        OnChanged(nameof(SmartOptimizeEnabled));
    }

    public ObservableCollection<InsightRow> Insights { get; } = new()
    {
        new InsightRow
        {
            Title = "Cortex is Learning",
            Description = "Insights will appear after 60 seconds of monitoring.",
            TitleBrush = global::Avalonia.Media.Brushes.Violet,
        }
    };

    public double CpuPercent       { get => _cpu;   set => Set(ref _cpu, value); }
    public double RamPercent       { get => _ram;   set => Set(ref _ram, value); }
    public double DiskPercent      { get => _disk;  set => Set(ref _disk, value); }
    public double GpuPercent       { get => _gpu;   set => Set(ref _gpu, value); }
    public double HealthScore      { get => _healthScore; set => Set(ref _healthScore, value); }
    public string HealthLabel      { get => _healthLabel; set => Set(ref _healthLabel, value); }

    public GpuInfo? GpuInfo        { get => _gpuInfo; private set { _gpuInfo = value; OnChanged(nameof(GpuInfo)); OnChanged(nameof(GpuVisible)); OnChanged(nameof(GpuName)); } }
    public bool    GpuVisible      => _gpuInfo is not null;
    public string  GpuName         => _gpuInfo?.Name ?? "";

    public string OsName           { get => _osName;      set => Set(ref _osName, value); }
    public string CpuName          { get => _cpuName;     set => Set(ref _cpuName, value); }
    public double RamTotalGb       { get => _ramTotalGb;  set => Set(ref _ramTotalGb, value); }

    public int CortexDaysActive    { get => _cortexDaysActive; set => Set(ref _cortexDaysActive, value); }
    public bool CortexOn           { get => _cortexOn;         set => Set(ref _cortexOn, value); }

    public string SystemSummary =>
        $"{OsName} · {CpuName} · {RamTotalGb:0.#} GB";

    public string CortexStatusText =>
        $"Cortex · Learning your patterns (day {CortexDaysActive})";

    // ───────── Phase 3 Task 32: ripple properties ─────────

    /// <summary>
    /// Whether the Cortex Insights card renders on Dashboard. Tied specifically
    /// to AppSettings.InsightsEnabled — not AnyFeatureEnabled — so disabling
    /// Insights alone hides the card regardless of other features.
    /// Defaults to true when no settings wired (design time / legacy ctor).
    /// </summary>
    public bool ShowCortexInsightsCard => _settings?.InsightsEnabled ?? true;

    /// <summary>
    /// Whether the "Cortex is monitoring" subtitle renders. Same gate as
    /// ShowCortexInsightsCard for now; spec §5.3 may diverge in Phase 5.
    /// </summary>
    public bool ShowCortexSubtitle => _settings?.InsightsEnabled ?? true;

    /// <summary>
    /// Header chip short state — "ON" when any AI feature is enabled, else "OFF".
    /// When no ambient wired, defaults to "OFF" (safer signal than stale ON).
    /// </summary>
    public string CortexChipState => _ambient?.AnyFeatureEnabled == true ? "ON" : "OFF";

    /// <summary>Full chip label, e.g. "Cortex AI · ON". Convenience for binding.</summary>
    public string CortexChipLabel => $"Cortex AI · {CortexChipState}";

    /// <summary>
    /// Whether the Smart Optimize hero CTA is actionable. Disabled when
    /// Recommendations are off — the CTA depends on that engine's output.
    /// </summary>
    public bool SmartOptimizeEnabled => _settings?.RecommendationsEnabled ?? true;

    public void SetGpuInfo(GpuInfo? info) => GpuInfo = info;

    public void UpdateInsights(IEnumerable<InsightRow> fresh)
    {
        Insights.Clear();
        foreach (var r in fresh) Insights.Add(r);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? prop = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            OnChanged(prop);
            if (prop is nameof(OsName) or nameof(CpuName) or nameof(RamTotalGb))
                OnChanged(nameof(SystemSummary));
            if (prop is nameof(CortexDaysActive))
                OnChanged(nameof(CortexStatusText));
        }
    }

    private void OnChanged(string? p) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
