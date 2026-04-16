using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AuraCore.Application;
using AuraCore.Module.MacAppInstaller;
using AuraCore.Module.MacAppInstaller.Models;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>
/// Slim adapter so the VM can be unit-tested without touching the real
/// <see cref="MacAppInstallerModule"/> (which shells out to brew).
/// The production adapter <see cref="MacAppInstallerEngineAdapter"/> forwards
/// to the concrete module. Mirrors the ILinuxAppInstallerEngine pattern
/// from Phase 4.3.5.
/// </summary>
public interface IMacAppInstallerEngine
{
    string Id { get; }
    Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default);
    Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct);
}

/// <summary>Default production adapter over the sealed concrete module.</summary>
public sealed class MacAppInstallerEngineAdapter : IMacAppInstallerEngine
{
    private readonly MacAppInstallerModule _module;
    public MacAppInstallerEngineAdapter(MacAppInstallerModule module) =>
        _module = module ?? throw new ArgumentNullException(nameof(module));

    public string Id => _module.Id;

    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default) =>
        _module.ScanAsync(options, ct);

    public Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct) =>
        _module.OptimizeAsync(plan, progress, ct);
}

/// <summary>
/// Per-app row view-model. Wraps a <see cref="MacBundleApp"/> from the static
/// catalog. Mirrors <c>app.Source.IsInstalled</c> after scan and tracks user
/// checkbox selection.
/// </summary>
public sealed class MacAppVM : INotifyPropertyChanged
{
    public MacAppVM(MacBundleApp source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        _isInstalled = source.IsInstalled;
    }

    public MacBundleApp Source { get; }
    public string Id => Source.Id;
    public string Name => Source.Name;
    public string Description => Source.Description;
    public MacPackageSource PackageSource => Source.Source;

    public string SourceDisplay => PackageSource switch
    {
        MacPackageSource.BrewFormula => LocalizationService._("macAppInstaller.source.formula"),
        MacPackageSource.BrewCask => LocalizationService._("macAppInstaller.source.cask"),
        _ => "",
    };

    private bool _isInstalled;
    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            if (_isInstalled == value) return;
            _isInstalled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCheckboxVisible));
            OnPropertyChanged(nameof(IsSourcePillVisible));
            OnPropertyChanged(nameof(IsFormulaPillVisible));
            OnPropertyChanged(nameof(IsCaskPillVisible));
            // Selection only makes sense for non-installed apps.
            if (value && _isSelected)
            {
                _isSelected = false;
                OnPropertyChanged(nameof(IsSelected));
                SelectionChanged?.Invoke();
            }
            InstalledChanged?.Invoke();
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            // Installed apps cannot be selected for install.
            if (_isInstalled) value = false;
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
            SelectionChanged?.Invoke();
        }
    }

    /// <summary>Checkbox shows only for not-yet-installed apps.</summary>
    public bool IsCheckboxVisible => !_isInstalled;

    /// <summary>Source pill shows for not-installed apps (formula/cask chip).</summary>
    public bool IsSourcePillVisible => !_isInstalled;

    /// <summary>True if this is a not-installed BrewFormula app (muted pill).</summary>
    public bool IsFormulaPillVisible => !_isInstalled && PackageSource == MacPackageSource.BrewFormula;

    /// <summary>True if this is a not-installed BrewCask app (orange/warn pill).</summary>
    public bool IsCaskPillVisible => !_isInstalled && PackageSource == MacPackageSource.BrewCask;

    public event Action? SelectionChanged;
    public event Action? InstalledChanged;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Per-bundle wrapper around a <see cref="MacAppBundle"/>. Tracks expanded
/// state and exposes a filtered <see cref="VisibleApps"/> projection driven by
/// the parent VM's SearchText.
/// </summary>
public sealed class MacBundleVM : INotifyPropertyChanged
{
    private readonly List<MacAppVM> _apps;
    private string _searchText = string.Empty;

    public MacBundleVM(MacAppBundle source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        _apps = source.Apps.Select(a => new MacAppVM(a)).ToList();
        foreach (var a in _apps)
        {
            a.SelectionChanged += OnAppSelectionChanged;
            a.InstalledChanged += OnAppInstalledChanged;
        }
        ToggleExpandCommand = new DelegateCommand(_ => IsExpanded = !IsExpanded);
    }

    public MacAppBundle Source { get; }
    public string Name => Source.Name;
    public string Description => Source.Description;
    public string IconCode => Source.Icon;

    public IReadOnlyList<MacAppVM> Apps => _apps;

    /// <summary>Apps that pass the current parent-provided search filter.</summary>
    public IEnumerable<MacAppVM> VisibleApps =>
        string.IsNullOrEmpty(_searchText)
            ? _apps
            : _apps.Where(MatchesSearch);

    public int TotalCount => _apps.Count;
    public int InstalledCount => _apps.Count(a => a.IsInstalled);
    public int AvailableCount => TotalCount - InstalledCount;

    public string SummaryDisplay =>
        string.Format(
            CultureInfo.InvariantCulture,
            LocalizationService._("macAppInstaller.bundle.summary"),
            TotalCount,
            InstalledCount,
            AvailableCount);

    /// <summary>True if any app in this bundle matches the current search.</summary>
    public bool HasVisibleApps =>
        string.IsNullOrEmpty(_searchText) || _apps.Any(MatchesSearch);

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ChevronGlyph));
        }
    }

    /// <summary>Renders "▼" when expanded, "▶" when collapsed.</summary>
    public string ChevronGlyph => _isExpanded ? "\u25BC" : "\u25B6";

    public ICommand ToggleExpandCommand { get; }

    internal void ApplySearch(string searchText)
    {
        _searchText = searchText ?? string.Empty;
        OnPropertyChanged(nameof(VisibleApps));
        OnPropertyChanged(nameof(HasVisibleApps));
    }

    internal void RefreshFromSource()
    {
        foreach (var app in _apps)
            app.IsInstalled = app.Source.IsInstalled;
        OnPropertyChanged(nameof(InstalledCount));
        OnPropertyChanged(nameof(AvailableCount));
        OnPropertyChanged(nameof(SummaryDisplay));
    }

    private bool MatchesSearch(MacAppVM app)
    {
        if (string.IsNullOrEmpty(_searchText)) return true;
        return app.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || app.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void OnAppSelectionChanged() => SelectionChanged?.Invoke();

    private void OnAppInstalledChanged()
    {
        OnPropertyChanged(nameof(InstalledCount));
        OnPropertyChanged(nameof(AvailableCount));
        OnPropertyChanged(nameof(SummaryDisplay));
    }

    public event Action? SelectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Phase 4.4.5 Mac App Installer view-model.
/// Accordion UI: 10 curated bundles with ~125 Homebrew apps. Live search
/// TextBox filters apps by Name/Description substring; matching bundles
/// auto-expand. Empty search restores initial state (first bundle expanded,
/// rest collapsed). Engine Scan refreshes IsInstalled state across all apps.
/// Install Selected triggers formula/cask installs then auto-rescans.
/// </summary>
public sealed class MacAppInstallerViewModel : INotifyPropertyChanged
{
    private readonly IMacAppInstallerEngine _engine;
    private CancellationTokenSource? _cts;

    public MacAppInstallerViewModel(IMacAppInstallerEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        // Build bundle VMs from the static catalog.
        AllBundles = MacAppBundles.AllBundles.Select(b => new MacBundleVM(b)).ToList();
        foreach (var b in AllBundles)
        {
            b.SelectionChanged += OnBundleSelectionChanged;
            // Apply initial empty-search state: first bundle expanded, rest collapsed.
            b.ApplySearch(string.Empty);
        }
        ApplyInitialExpansion();

        ScanCommand = new AsyncDelegateCommand(
            execute: _ => ScanAsync(),
            canExecute: _ => !IsBusy);

        InstallSelectedCommand = new AsyncDelegateCommand(
            execute: _ => InstallSelectedAsync(),
            canExecute: _ => !IsBusy && SelectedCount > 0);

        CancelCommand = new DelegateCommand(
            execute: _ => Cancel(),
            canExecute: _ => IsBusy && _cts is not null);

        StatusText = LocalizationService._("macAppInstaller.status.idle");
    }

    /// <summary>Convenience ctor for DI — wraps the concrete module in the default adapter.</summary>
    public MacAppInstallerViewModel(MacAppInstallerModule module)
        : this(new MacAppInstallerEngineAdapter(module ?? throw new ArgumentNullException(nameof(module))))
    {
    }

    // ── Bundles ────────────────────────────────────────────────────

    public IReadOnlyList<MacBundleVM> AllBundles { get; }

    /// <summary>Bundles with at least one app matching the current search.</summary>
    public IEnumerable<MacBundleVM> VisibleBundles => AllBundles.Where(b => b.HasVisibleApps);

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            value ??= string.Empty;
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();

            // Propagate to each bundle so VisibleApps / HasVisibleApps re-project.
            foreach (var b in AllBundles)
                b.ApplySearch(_searchText);

            // Manage expanded state based on transitions:
            //  - empty → non-empty: auto-expand every matching bundle.
            //  - non-empty → empty: restore initial state (first expanded, rest collapsed).
            //  - non-empty → non-empty: re-expand matching bundles (user's prior collapse state
            //    is NOT preserved across search transitions — simplicity-first, mirrors 4.3.5).
            var isEmpty = _searchText.Length == 0;
            if (isEmpty)
                ApplyInitialExpansion();
            else
                AutoExpandMatching();

            OnPropertyChanged(nameof(VisibleBundles));
        }
    }

    // ── Stats ─────────────────────────────────────────────────────

    public int TotalAppsCount => AllBundles.Sum(b => b.TotalCount);

    public int InstalledCount => AllBundles.Sum(b => b.InstalledCount);

    public int AvailableCount => TotalAppsCount - InstalledCount;

    private bool _scanCompleted;

    public string TotalAppsDisplay =>
        string.Format(
            CultureInfo.InvariantCulture,
            LocalizationService._("macAppInstaller.stat.totalValue"),
            TotalAppsCount);

    public string InstalledDisplay =>
        _scanCompleted ? InstalledCount.ToString(CultureInfo.InvariantCulture) : "--";

    public string AvailableDisplay =>
        _scanCompleted ? AvailableCount.ToString(CultureInfo.InvariantCulture) : "--";

    // ── Selection ─────────────────────────────────────────────────

    public int SelectedCount =>
        AllBundles.Sum(b => b.Apps.Count(a => a.IsSelected && !a.IsInstalled));

    public bool HasSelection => SelectedCount > 0;

    public string SelectedSummaryDisplay
    {
        get
        {
            int formula = 0, cask = 0;
            foreach (var bundle in AllBundles)
                foreach (var app in bundle.Apps)
                    if (app.IsSelected && !app.IsInstalled)
                        switch (app.PackageSource)
                        {
                            case MacPackageSource.BrewFormula: formula++; break;
                            case MacPackageSource.BrewCask: cask++; break;
                        }
            var headline = string.Format(
                CultureInfo.InvariantCulture,
                LocalizationService._("macAppInstaller.selected.summary"),
                SelectedCount);
            var breakdown = string.Format(
                CultureInfo.InvariantCulture,
                LocalizationService._("macAppInstaller.selected.breakdown"),
                formula, cask);
            return headline + " \u00B7 " + breakdown;
        }
    }

    // ── Standard VM state ────────────────────────────────────────

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value) return;
            _statusText = value;
            OnPropertyChanged();
        }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            RaiseAllCanExecuteChanged();
        }
    }

    private double _progressPercent;
    public double ProgressPercent
    {
        get => _progressPercent;
        private set
        {
            if (Math.Abs(_progressPercent - value) < 0.01) return;
            _progressPercent = value;
            OnPropertyChanged();
        }
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value) return;
            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    public ICommand ScanCommand { get; }
    public ICommand InstallSelectedCommand { get; }
    public ICommand CancelCommand { get; }

    // ── Async operations ──────────────────────────────────────────

    public async Task ScanAsync()
    {
        if (IsBusy) return;
        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ProgressPercent = 0;
            StatusText = LocalizationService._("macAppInstaller.status.scanning");

            var result = await _engine.ScanAsync(new ScanOptions(), _cts.Token);

            // Engine mutates MacAppBundles static catalog — mirror into our VMs.
            foreach (var b in AllBundles)
                b.RefreshFromSource();
            _scanCompleted = true;
            RaiseStatsChanged();

            if (!result.Success)
            {
                var reason = result.BlockedReason ?? "scan failed";
                // Homebrew-missing gets its own friendly status; anything else is generic.
                var isBrewMissing = reason.Contains("Homebrew", StringComparison.OrdinalIgnoreCase);
                StatusText = isBrewMissing
                    ? LocalizationService._("macAppInstaller.status.brewMissing")
                    : string.Format(LocalizationService._("macAppInstaller.status.error"), reason);
                ErrorMessage = reason;
                return;
            }

            StatusText = LocalizationService._("macAppInstaller.status.idle");
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("macAppInstaller.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("macAppInstaller.status.error"), ex.Message);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            ProgressPercent = 0;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public async Task InstallSelectedAsync()
    {
        if (IsBusy) return;
        var selectedApps = AllBundles
            .SelectMany(b => b.Apps)
            .Where(a => a.IsSelected && !a.IsInstalled)
            .ToList();
        if (selectedApps.Count == 0) return;
        var ids = selectedApps.Select(a => $"install:{a.Id}").ToList();
        var attempted = selectedApps.Count;

        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ProgressPercent = 0;
            StatusText = string.Format(
                LocalizationService._("macAppInstaller.status.installing"),
                selectedApps[0].Name);

            var plan = new OptimizationPlan(_engine.Id, ids);
            var progress = new Progress<TaskProgress>(p => ProgressPercent = p.Percentage);

            var result = await _engine.OptimizeAsync(plan, progress, _cts.Token);

            if (!result.Success)
            {
                StatusText = string.Format(
                    LocalizationService._("macAppInstaller.status.error"),
                    "install failed");
                ErrorMessage = "Install operation failed. Check Homebrew installation or cask authorization.";
                return;
            }

            // Auto re-scan so IsInstalled state reflects what actually landed.
            var rescan = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            foreach (var b in AllBundles)
                b.RefreshFromSource();
            _scanCompleted = true;

            // Clear IsSelected on every row — "consumed" selection. Apps that landed
            // already auto-cleared via the IsInstalled setter, but we sweep the full
            // catalog so any install that failed silently also resets.
            foreach (var bundle in AllBundles)
                foreach (var app in bundle.Apps)
                    app.IsSelected = false;

            RaiseStatsChanged();
            StatusText = string.Format(
                LocalizationService._("macAppInstaller.status.done"),
                result.ItemsProcessed,
                attempted);
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("macAppInstaller.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("macAppInstaller.status.error"), ex.Message);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            ProgressPercent = 0;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public void Cancel()
    {
        try { _cts?.Cancel(); } catch { /* already disposed */ }
    }

    /// <summary>
    /// Fires an initial scan on view-load. Called from the view's Loaded handler.
    /// No-ops if a scan is already in-flight. Mirrors the auto-scan pattern used
    /// by Journal/Kernel/Docker cleaners.
    /// </summary>
    public void TriggerInitialScan()
    {
        if (IsBusy || _scanCompleted) return;
        _ = ScanAsync();
    }

    // ── State-machine helpers ─────────────────────────────────────

    /// <summary>Empty-search state: first bundle expanded, rest collapsed.</summary>
    private void ApplyInitialExpansion()
    {
        for (int i = 0; i < AllBundles.Count; i++)
            AllBundles[i].IsExpanded = (i == 0);
    }

    /// <summary>Non-empty search: every matching bundle is expanded.</summary>
    private void AutoExpandMatching()
    {
        foreach (var b in AllBundles)
            b.IsExpanded = b.HasVisibleApps;
    }

    // ── Recompute fan-out ─────────────────────────────────────────

    private void OnBundleSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedSummaryDisplay));
        (InstallSelectedCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
    }

    private void RaiseStatsChanged()
    {
        OnPropertyChanged(nameof(InstalledCount));
        OnPropertyChanged(nameof(AvailableCount));
        OnPropertyChanged(nameof(InstalledDisplay));
        OnPropertyChanged(nameof(AvailableDisplay));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedSummaryDisplay));
        (InstallSelectedCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
    }

    private void RaiseAllCanExecuteChanged()
    {
        (ScanCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (InstallSelectedCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as DelegateCommand)?.RaiseCanExecuteChanged();
    }

    // ── INPC ───────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
