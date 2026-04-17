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
using AuraCore.Module.KernelCleaner;
using AuraCore.Module.KernelCleaner.Models;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>
/// Slim adapter so the VM can be unit-tested without touching the real
/// <see cref="KernelCleanerModule"/> (which shells out to apt/dnf/uname).
/// The production adapter <see cref="KernelCleanerEngineAdapter"/> forwards
/// to the concrete module. Mirrors the IDockerCleanerEngine /
/// ISnapFlatpakCleanerEngine / IJournalCleanerEngine patterns from
/// 4.3.1 / 4.3.2 / 4.3.3.
/// </summary>
public interface IKernelCleanerEngine
{
    string Id { get; }
    KernelReport? LastReport { get; }
    Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default);
    Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct);
}

/// <summary>Default production adapter over the sealed concrete module.</summary>
public sealed class KernelCleanerEngineAdapter : IKernelCleanerEngine
{
    private readonly KernelCleanerModule _module;
    public KernelCleanerEngineAdapter(KernelCleanerModule module) =>
        _module = module ?? throw new ArgumentNullException(nameof(module));

    public string Id => _module.Id;
    public KernelReport? LastReport => _module.LastReport;

    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default) =>
        _module.ScanAsync(options, ct);

    public Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct) =>
        _module.OptimizeAsync(plan, progress, ct);
}

/// <summary>
/// Per-kernel row view-model used by the Manual Selection list.
/// Visual state (running / newest / removable) is derived via
/// <see cref="IsCheckboxVisible"/> / <see cref="IsCheckboxEnabled"/>
/// so the XAML can drive row appearance through DataTriggers without
/// the VM needing to resolve brushes directly (theme-friendly).
/// </summary>
public sealed class KernelItemVM : INotifyPropertyChanged
{
    public KernelItemVM(string version, string packageName, long sizeBytes, bool isRunning, bool isNewest)
    {
        Version = version ?? string.Empty;
        PackageName = packageName ?? string.Empty;
        SizeBytes = sizeBytes;
        IsRunning = isRunning;
        IsNewest = isNewest;
        SizeDisplay = KernelCleanerViewModel.FormatSize(sizeBytes);
    }

    public string Version { get; }
    public string PackageName { get; }
    public long SizeBytes { get; }
    public string SizeDisplay { get; }
    public bool IsRunning { get; }
    public bool IsNewest { get; }

    /// <summary>Running rows show a "--" placeholder instead of a checkbox.</summary>
    public bool IsCheckboxVisible => !IsRunning;

    /// <summary>Newest-non-running rows render a disabled checkbox so the user sees protection.</summary>
    public bool IsCheckboxEnabled => !IsRunning && !IsNewest;

    /// <summary>Only show the NEWEST badge when the row is newest AND not running (running takes precedence).</summary>
    public bool IsNewestBadgeVisible => IsNewest && !IsRunning;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
            SelectionChanged?.Invoke();
        }
    }

    /// <summary>Fires whenever <see cref="IsSelected"/> flips so the hosting VM can
    /// recompute HasSelection / SelectedCountDisplay.</summary>
    public event Action? SelectionChanged;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Phase 4.3.4 Kernel Cleaner view-model.
/// Layout B "Safety-tiered, kernel-list variant": hero Safe Cleanup card
/// (package-manager-decided via engine <c>remove-old</c>), a Manual Selection
/// card with a per-kernel checkbox list (running + newest are un-selectable),
/// and a gated Danger Zone for <c>remove-all-but-current</c>.
/// </summary>
public sealed class KernelCleanerViewModel : INotifyPropertyChanged
{
    private readonly IKernelCleanerEngine _engine;
    private CancellationTokenSource? _cts;

    public KernelCleanerViewModel(IKernelCleanerEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        KernelItems = new ObservableCollection<KernelItemVM>();

        ScanCommand = new AsyncDelegateCommand(
            execute: _ => ScanAsync(),
            canExecute: _ => !IsBusy);

        AutoRemoveOldCommand = new AsyncDelegateCommand(
            execute: _ => OptimizeAsync(new[] { "remove-old" }),
            canExecute: _ => !IsBusy && PackageManagerAvailable);

        RemoveSelectedCommand = new AsyncDelegateCommand(
            execute: _ => RemoveSelectedAsync(),
            canExecute: _ => !IsBusy && PackageManagerAvailable && HasSelection);

        RemoveAllButCurrentCommand = new AsyncDelegateCommand(
            execute: _ => OptimizeAsync(new[] { "remove-all-but-current" }),
            canExecute: _ => !IsBusy && PackageManagerAvailable && DangerAcknowledged);

        CancelCommand = new DelegateCommand(
            execute: _ => Cancel(),
            canExecute: _ => IsBusy && _cts is not null);

        StatusText = LocalizationService._("kernelCleaner.status.idle");
    }

    /// <summary>Convenience ctor for DI — wraps the concrete module in the default adapter.</summary>
    public KernelCleanerViewModel(KernelCleanerModule module)
        : this(new KernelCleanerEngineAdapter(module ?? throw new ArgumentNullException(nameof(module))))
    {
    }

    // ── Public surface ─────────────────────────────────────────────

    private KernelReport? _report;
    public KernelReport? Report
    {
        get => _report;
        private set
        {
            if (ReferenceEquals(_report, value)) return;
            _report = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveKernelDisplay));
            OnPropertyChanged(nameof(RemovableDisplay));
            OnPropertyChanged(nameof(SafeDescriptionText));
        }
    }

    public string ActiveKernelDisplay =>
        string.IsNullOrEmpty(_report?.CurrentKernel) ? "--" : _report!.CurrentKernel;

    public string RemovableDisplay
    {
        get
        {
            if (_report is null) return "--";
            var removable = _report.Kernels.Count(k => !k.IsCurrent && !k.IsLatest);
            if (removable == 0) return "--";
            return string.Format(
                CultureInfo.InvariantCulture,
                LocalizationService._("kernelCleaner.removable.summary"),
                removable,
                FormatSize(_report.TotalRemovableBytes));
        }
    }

    /// <summary>Pre-formatted description string for the Safe Cleanup card — picks up Report changes.</summary>
    public string SafeDescriptionText =>
        string.Format(
            LocalizationService._("kernelCleaner.safe.description"),
            _report is null ? "--" : FormatSize(_report.TotalRemovableBytes));

    public ObservableCollection<KernelItemVM> KernelItems { get; }

    public int SelectedCount =>
        KernelItems.Count(i => i.IsSelected && !i.IsRunning && !i.IsNewest);

    public long SelectedBytes =>
        KernelItems.Where(i => i.IsSelected && !i.IsRunning && !i.IsNewest).Sum(i => i.SizeBytes);

    public bool HasSelection => SelectedCount > 0;

    public string SelectedCountDisplay
    {
        get
        {
            if (!HasSelection) return LocalizationService._("kernelCleaner.selected.none");
            return string.Format(
                CultureInfo.InvariantCulture,
                LocalizationService._("kernelCleaner.selected.summary"),
                SelectedCount,
                FormatSize(SelectedBytes));
        }
    }

    private bool _packageManagerAvailable;
    public bool PackageManagerAvailable
    {
        get => _packageManagerAvailable;
        private set
        {
            if (_packageManagerAvailable == value) return;
            _packageManagerAvailable = value;
            OnPropertyChanged();
            RaiseAllCanExecuteChanged();
        }
    }

    private bool _dangerAcknowledged;
    /// <summary>Gated by the Danger Zone checkbox. Resets on every scan + after a remove-all-but-current.</summary>
    public bool DangerAcknowledged
    {
        get => _dangerAcknowledged;
        set
        {
            if (_dangerAcknowledged == value) return;
            _dangerAcknowledged = value;
            OnPropertyChanged();
            (RemoveAllButCurrentCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        }
    }

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
    public ICommand AutoRemoveOldCommand { get; }
    public ICommand RemoveSelectedCommand { get; }
    public ICommand RemoveAllButCurrentCommand { get; }
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
            StatusText = LocalizationService._("kernelCleaner.status.scanning");

            var result = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            Report = _engine.LastReport;
            PackageManagerAvailable = Report?.IsAvailable ?? false;

            // Reset acknowledgement and selection-state on every scan
            DangerAcknowledged = false;
            RebuildKernelItems();

            if (!result.Success)
            {
                if (!PackageManagerAvailable)
                    StatusText = LocalizationService._("kernelCleaner.status.unavailable");
                else
                    StatusText = string.Format(
                        LocalizationService._("kernelCleaner.status.error"),
                        result.BlockedReason ?? "scan failed");
                ErrorMessage = result.BlockedReason;
                return;
            }

            StatusText = LocalizationService._("kernelCleaner.status.idle");
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("kernelCleaner.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("kernelCleaner.status.error"), ex.Message);
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

    /// <summary>
    /// Populate <see cref="KernelItems"/> from the current <see cref="Report"/>,
    /// sorted ascending (oldest first). Pre-checks all-but-the-two-newest
    /// removable kernels. Only pre-checks when there are &gt;2 removable;
    /// if &le;2 removable, leaves them unchecked so the user must opt-in.
    /// </summary>
    private void RebuildKernelItems()
    {
        // Detach existing handlers + clear
        foreach (var old in KernelItems)
            old.SelectionChanged -= OnItemSelectionChanged;
        KernelItems.Clear();

        if (_report is null || _report.Kernels.Count == 0)
        {
            RaiseSelectionChanged();
            return;
        }

        // Oldest → newest. (Engine already sorts ordinal; re-apply defensively.)
        var sorted = _report.Kernels.OrderBy(k => k.Version, StringComparer.Ordinal).ToList();

        // Track removable (non-running, non-newest) in sorted order so we can
        // leave the two newest removable unchecked.
        var removable = sorted.Where(k => !k.IsCurrent && !k.IsLatest).ToList();
        int skipFromTail = Math.Min(2, removable.Count);
        // Pre-check only when there are >2 removable. If <=2, skip all.
        bool preCheckEnabled = removable.Count > 2;
        var toPreCheck = preCheckEnabled
            ? new HashSet<string>(removable.Take(removable.Count - skipFromTail).Select(k => k.Version), StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        foreach (var info in sorted)
        {
            var item = new KernelItemVM(
                version: info.Version,
                packageName: info.PackageName,
                sizeBytes: info.SizeBytes,
                isRunning: info.IsCurrent,
                isNewest: info.IsLatest);

            if (item.IsCheckboxEnabled && toPreCheck.Contains(info.Version))
                item.IsSelected = true;

            item.SelectionChanged += OnItemSelectionChanged;
            KernelItems.Add(item);
        }

        RaiseSelectionChanged();
    }

    private void OnItemSelectionChanged() => RaiseSelectionChanged();

    private void RaiseSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedCountDisplay));
        (RemoveSelectedCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
    }

    public async Task RemoveSelectedAsync()
    {
        if (!HasSelection) return;
        // Defensive filter: skip running / newest even if IsSelected was set externally.
        var ids = KernelItems
            .Where(i => i.IsSelected && !i.IsRunning && !i.IsNewest)
            .Select(i => $"remove:{i.Version}")
            .ToList();

        if (ids.Count == 0) return;
        await OptimizeAsync(ids, isRemoveAllButCurrent: false);
    }

    public Task OptimizeAsync(IReadOnlyList<string> itemIds) =>
        OptimizeAsync(itemIds, isRemoveAllButCurrent: itemIds.Count == 1 && itemIds[0] == "remove-all-but-current");

    private async Task OptimizeAsync(IReadOnlyList<string> itemIds, bool isRemoveAllButCurrent)
    {
        if (itemIds is null || itemIds.Count == 0) return;
        if (IsBusy) return;
        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ProgressPercent = 0;
            StatusText = LocalizationService._("kernelCleaner.status.removing");

            var plan = new OptimizationPlan(_engine.Id, itemIds);
            var progress = new Progress<TaskProgress>(p => ProgressPercent = p.Percentage);

            var result = await _engine.OptimizeAsync(plan, progress, _cts.Token);

            if (!result.Success)
            {
                StatusText = string.Format(
                    LocalizationService._("kernelCleaner.status.error"),
                    "removal failed");
                ErrorMessage = "Kernel removal operation failed. Check privileges or package manager state.";
                return;
            }

            // Re-scan to refresh list after removal
            var rescan = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            if (rescan.Success)
            {
                Report = _engine.LastReport;
                PackageManagerAvailable = Report?.IsAvailable ?? false;
                RebuildKernelItems();
            }

            StatusText = string.Format(
                LocalizationService._("kernelCleaner.status.done"),
                FormatSize(result.BytesFreed));

            // Reset acknowledgement after a Danger Zone run — single-use consent.
            if (isRemoveAllButCurrent) DangerAcknowledged = false;
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("kernelCleaner.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("kernelCleaner.status.error"), ex.Message);
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

    // ── Helpers ────────────────────────────────────────────────────

    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "--";
        const double KB = 1024.0;
        const double MB = KB * 1024;
        const double GB = MB * 1024;

        if (bytes >= GB) return (bytes / GB).ToString("F2", CultureInfo.InvariantCulture) + " GB";
        if (bytes >= MB) return ((long)(bytes / MB)).ToString(CultureInfo.InvariantCulture) + " MB";
        if (bytes >= KB) return ((long)(bytes / KB)).ToString(CultureInfo.InvariantCulture) + " KB";
        return bytes.ToString(CultureInfo.InvariantCulture) + " B";
    }

    private void RaiseAllCanExecuteChanged()
    {
        (ScanCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (AutoRemoveOldCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (RemoveSelectedCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (RemoveAllButCurrentCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as DelegateCommand)?.RaiseCanExecuteChanged();
    }

    // ── INPC ───────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
