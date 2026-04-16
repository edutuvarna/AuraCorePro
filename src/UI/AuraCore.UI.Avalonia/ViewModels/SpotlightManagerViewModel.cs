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
using AuraCore.Module.SpotlightManager;
using AuraCore.Module.SpotlightManager.Models;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>
/// Slim adapter so the VM can be unit-tested without touching the sealed
/// <see cref="SpotlightManagerModule"/> (which shells out to mdutil/sudo on
/// macOS). Mirrors the IDnsFlusherEngine / IPurgeableSpaceManagerEngine
/// pattern from 4.4.1-4.4.2.
/// </summary>
public interface ISpotlightEngine
{
    string Id { get; }
    SpotlightReport? LastReport { get; }
    Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default);
    Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct);
}

/// <summary>Default production adapter over the sealed concrete module.</summary>
public sealed class SpotlightEngineAdapter : ISpotlightEngine
{
    private readonly SpotlightManagerModule _module;
    public SpotlightEngineAdapter(SpotlightManagerModule module) =>
        _module = module ?? throw new ArgumentNullException(nameof(module));

    public string Id => _module.Id;
    public SpotlightReport? LastReport => _module.LastReport;

    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default) =>
        _module.ScanAsync(options, ct);

    public Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct) =>
        _module.OptimizeAsync(plan, progress, ct);
}

/// <summary>
/// Per-volume row view-model. <see cref="IsIndexed"/> is the user's intended
/// state and is two-way-bound to the ToggleSwitch; <see cref="ActualIndexingEnabled"/>
/// is the ground truth from the last scan. When the engine call fails, the
/// parent VM calls <see cref="ApplyFromEngine"/> to revert IsIndexed to the
/// actual value with the <c>_isUpdatingFromEngine</c> guard in place so the
/// setter does NOT re-raise <see cref="Requested"/> (which would re-dispatch
/// the engine in a loop).
/// </summary>
public sealed class VolumeItemVM : INotifyPropertyChanged
{
    public VolumeItemVM(string mountPoint, bool indexingEnabled)
    {
        MountPoint = mountPoint ?? string.Empty;
        ActualIndexingEnabled = indexingEnabled;
        _isIndexed = indexingEnabled;
    }

    public string MountPoint { get; }

    private bool _isIndexed;
    private bool _isUpdatingFromEngine;

    public bool IsIndexed
    {
        get => _isIndexed;
        set
        {
            if (_isIndexed == value) return;
            _isIndexed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(BadgeText));
            if (!_isUpdatingFromEngine)
                Requested?.Invoke(this, value);
        }
    }

    public bool ActualIndexingEnabled { get; private set; }

    public string StatusDisplay => IsIndexed
        ? LocalizationService._("spotlight.row.indexing.enabled")
        : LocalizationService._("spotlight.row.indexing.disabled");

    public string BadgeText => IsIndexed
        ? LocalizationService._("spotlight.badge.indexed")
        : LocalizationService._("spotlight.badge.off");

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        internal set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Fires whenever the user flips the toggle. The host VM
    /// dispatches the engine call in response.</summary>
    public event EventHandler<bool>? Requested;

    /// <summary>
    /// Update both IsIndexed + ActualIndexingEnabled from the engine without
    /// re-raising <see cref="Requested"/>. Used by the host VM for
    /// (a) initial population from a scan result and (b) reverting a failed
    /// toggle back to the real engine value.
    /// </summary>
    public void ApplyFromEngine(bool enabled)
    {
        _isUpdatingFromEngine = true;
        try
        {
            ActualIndexingEnabled = enabled;
            if (_isIndexed != enabled)
            {
                _isIndexed = enabled;
                OnPropertyChanged(nameof(IsIndexed));
            }
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(BadgeText));
        }
        finally
        {
            _isUpdatingFromEngine = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Phase 4.4.3 Spotlight Manager view-model.
/// "Per-volume inline control" UX (user-directed top-0.1% designer pattern):
/// toggling a row immediately dispatches enable/disable (cheap, reversible);
/// Rebuild gets a single-open inline confirmation (expensive: minutes-hours
/// of disk I/O). Auto-scans on Loaded — no Scan button. On engine failure,
/// the toggle snaps back to <see cref="VolumeItemVM.ActualIndexingEnabled"/>
/// with the INPC guard in place so the revert does not re-dispatch.
/// </summary>
public sealed class SpotlightManagerViewModel : INotifyPropertyChanged
{
    private readonly ISpotlightEngine _engine;
    private CancellationTokenSource? _cts;

    public SpotlightManagerViewModel(ISpotlightEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        VolumeItems = new ObservableCollection<VolumeItemVM>();

        BeginRebuildCommand = new DelegateCommand(
            execute: p => { if (p is VolumeItemVM vm) PendingRebuildVolume = vm; },
            canExecute: p => p is VolumeItemVM && !IsBusy);

        CancelRebuildCommand = new DelegateCommand(
            execute: _ => { PendingRebuildVolume = null; },
            canExecute: _ => PendingRebuildVolume is not null);

        ConfirmRebuildCommand = new AsyncDelegateCommand(
            execute: p => ConfirmRebuildAsync(p as VolumeItemVM ?? PendingRebuildVolume),
            canExecute: _ => !IsBusy && PendingRebuildVolume is not null);

        ToggleIndexingCommand = new AsyncDelegateCommand(
            execute: p => DispatchToggleAsync(p as VolumeItemVM, null),
            canExecute: p => p is VolumeItemVM && !IsBusy);

        CancelCommand = new DelegateCommand(
            execute: _ => { try { _cts?.Cancel(); } catch { /* disposed */ } },
            canExecute: _ => IsBusy);

        StatusText = LocalizationService._("spotlight.status.idle");
    }

    /// <summary>Convenience ctor for DI — wraps the concrete module in the default adapter.</summary>
    public SpotlightManagerViewModel(SpotlightManagerModule module)
        : this(new SpotlightEngineAdapter(module ?? throw new ArgumentNullException(nameof(module))))
    {
    }

    // ── Public surface ──────────────────────────────────────────────

    private SpotlightReport? _report;
    public SpotlightReport? Report
    {
        get => _report;
        private set
        {
            if (ReferenceEquals(_report, value)) return;
            _report = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TotalVolumes));
            OnPropertyChanged(nameof(EnabledCount));
            OnPropertyChanged(nameof(DisabledCount));
            OnPropertyChanged(nameof(TotalVolumesDisplay));
            OnPropertyChanged(nameof(EnabledCountDisplay));
            OnPropertyChanged(nameof(DisabledCountDisplay));
            OnPropertyChanged(nameof(HasVolumes));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public bool MdutilAvailable => _report?.IsAvailable ?? false;

    public int TotalVolumes => _report?.TotalVolumes ?? 0;
    public int EnabledCount => _report?.EnabledCount ?? 0;
    public int DisabledCount => _report?.DisabledCount ?? 0;

    public string TotalVolumesDisplay =>
        _report is null ? "--" : TotalVolumes.ToString(CultureInfo.InvariantCulture);
    public string EnabledCountDisplay =>
        _report is null ? "--" : EnabledCount.ToString(CultureInfo.InvariantCulture);
    public string DisabledCountDisplay =>
        _report is null ? "--" : DisabledCount.ToString(CultureInfo.InvariantCulture);

    public ObservableCollection<VolumeItemVM> VolumeItems { get; }

    public bool HasVolumes => VolumeItems.Count > 0;
    public bool IsEmpty => _report is not null && VolumeItems.Count == 0 && !IsBusy;

    private VolumeItemVM? _pendingRebuildVolume;
    public VolumeItemVM? PendingRebuildVolume
    {
        get => _pendingRebuildVolume;
        private set
        {
            if (ReferenceEquals(_pendingRebuildVolume, value)) return;
            _pendingRebuildVolume = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPendingRebuild));
            OnPropertyChanged(nameof(RebuildConfirmationBody));
            (CancelRebuildCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            (ConfirmRebuildCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool HasPendingRebuild => _pendingRebuildVolume is not null;

    public string RebuildConfirmationBody => _pendingRebuildVolume is null
        ? string.Empty
        : string.Format(
            LocalizationService._("spotlight.rebuild.confirm.body"),
            _pendingRebuildVolume.MountPoint);

    // ── Busy / status / error ───────────────────────────────────────

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEmpty));
            (BeginRebuildCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            (ConfirmRebuildCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
            (ToggleIndexingCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
            (CancelCommand as DelegateCommand)?.RaiseCanExecuteChanged();
        }
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        private set { if (_statusText != value) { _statusText = value; OnPropertyChanged(); } }
    }

    private double _progressPercent;
    public double ProgressPercent
    {
        get => _progressPercent;
        private set { if (Math.Abs(_progressPercent - value) > 0.01) { _progressPercent = value; OnPropertyChanged(); } }
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

    // ── Commands ────────────────────────────────────────────────────

    public ICommand ToggleIndexingCommand { get; }
    public ICommand BeginRebuildCommand { get; }
    public ICommand ConfirmRebuildCommand { get; }
    public ICommand CancelRebuildCommand { get; }
    public ICommand CancelCommand { get; }

    // ── Operations ──────────────────────────────────────────────────

    /// <summary>Fires the initial scan. Called from the View's Loaded event
    /// (see <see cref="SpotlightManagerView"/>).</summary>
    public void TriggerInitialScan() => _ = ScanAsync();

    public async Task ScanAsync()
    {
        if (IsBusy) return;
        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            StatusText = LocalizationService._("spotlight.status.scanning");
            ProgressPercent = 0;

            var result = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            Report = _engine.LastReport;
            RebuildVolumeItems();

            if (!result.Success)
            {
                if (Report is { IsAvailable: false })
                    StatusText = LocalizationService._("spotlight.status.unavailable");
                else
                    StatusText = string.Format(
                        LocalizationService._("spotlight.status.error"),
                        result.BlockedReason ?? "scan failed");
                ErrorMessage = result.BlockedReason;
                return;
            }

            StatusText = LocalizationService._("spotlight.status.idle");
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("spotlight.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("spotlight.status.error"), ex.Message);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void RebuildVolumeItems()
    {
        foreach (var old in VolumeItems)
            old.Requested -= OnVolumeToggleRequested;
        VolumeItems.Clear();

        if (_report is null || _report.Volumes is null) { RaiseVolumeCollectionChanged(); return; }

        foreach (var info in _report.Volumes)
        {
            var item = new VolumeItemVM(info.MountPoint, info.IndexingEnabled);
            item.Requested += OnVolumeToggleRequested;
            VolumeItems.Add(item);
        }

        RaiseVolumeCollectionChanged();
    }

    private void RaiseVolumeCollectionChanged()
    {
        OnPropertyChanged(nameof(HasVolumes));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void OnVolumeToggleRequested(object? sender, bool requestedEnabled)
    {
        if (sender is not VolumeItemVM vm) return;
        // Fire-and-forget — AsyncDelegateCommand semantics.
        _ = DispatchToggleAsync(vm, requestedEnabled);
    }

    /// <summary>
    /// Dispatches the engine enable/disable for the given volume. Exposed
    /// publicly so the <see cref="ToggleIndexingCommand"/> CommandParameter
    /// path (and unit tests) can drive it without going through the
    /// <see cref="VolumeItemVM.Requested"/> event; in that case the
    /// <paramref name="requestedEnabled"/> argument is null and the current
    /// VolumeItemVM.IsIndexed value is used.
    /// </summary>
    public async Task DispatchToggleAsync(VolumeItemVM? volume, bool? requestedEnabled)
    {
        if (volume is null) return;

        // Reject volumes not in LastReport — engine validates too but the VM
        // should never issue an itemId for an unknown volume.
        var known = _report?.Volumes?.Any(v =>
            string.Equals(v.MountPoint, volume.MountPoint, StringComparison.Ordinal)) ?? false;
        if (!known) return;

        if (IsBusy) return;
        var desired = requestedEnabled ?? volume.IsIndexed;
        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            volume.IsBusy = true;
            ErrorMessage = null;
            StatusText = string.Format(
                LocalizationService._("spotlight.status.toggling"),
                volume.MountPoint);
            ProgressPercent = 0;

            var action = desired ? "enable" : "disable";
            var itemId = $"{action}:{volume.MountPoint}";
            var plan = new OptimizationPlan(_engine.Id, new List<string> { itemId });
            var progress = new Progress<TaskProgress>(p => ProgressPercent = p.Percentage);
            var result = await _engine.OptimizeAsync(plan, progress, _cts.Token);

            if (!result.Success || result.ItemsProcessed == 0)
            {
                // Revert toggle to actual value. ApplyFromEngine uses the
                // _isUpdatingFromEngine guard so the revert will NOT re-raise
                // the Requested event (no re-dispatch loop).
                volume.ApplyFromEngine(volume.ActualIndexingEnabled);
                StatusText = string.Format(
                    LocalizationService._("spotlight.status.error"),
                    "toggle failed");
                ErrorMessage = "Spotlight toggle did not succeed. Check privileges.";
                return;
            }

            volume.ApplyFromEngine(desired);
            OnPropertyChanged(nameof(EnabledCount));
            OnPropertyChanged(nameof(DisabledCount));
            OnPropertyChanged(nameof(EnabledCountDisplay));
            OnPropertyChanged(nameof(DisabledCountDisplay));
            RecomputeReportCounts();
            StatusText = LocalizationService._("spotlight.status.done");
            ProgressPercent = 100;
        }
        catch (OperationCanceledException)
        {
            volume.ApplyFromEngine(volume.ActualIndexingEnabled);
            StatusText = LocalizationService._("spotlight.status.idle");
        }
        catch (Exception ex)
        {
            volume.ApplyFromEngine(volume.ActualIndexingEnabled);
            StatusText = string.Format(LocalizationService._("spotlight.status.error"), ex.Message);
            ErrorMessage = ex.Message;
        }
        finally
        {
            volume.IsBusy = false;
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Confirmation step of the rebuild flow. Called from
    /// <see cref="ConfirmRebuildCommand"/>. Clears <see cref="PendingRebuildVolume"/>
    /// on both success and failure so stale confirmation state does not linger.
    /// </summary>
    public async Task ConfirmRebuildAsync(VolumeItemVM? volume)
    {
        volume ??= PendingRebuildVolume;
        if (volume is null) return;
        if (IsBusy) return;

        var known = _report?.Volumes?.Any(v =>
            string.Equals(v.MountPoint, volume.MountPoint, StringComparison.Ordinal)) ?? false;
        if (!known) { PendingRebuildVolume = null; return; }

        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            volume.IsBusy = true;
            ErrorMessage = null;
            StatusText = string.Format(
                LocalizationService._("spotlight.status.rebuilding"),
                volume.MountPoint);
            ProgressPercent = 0;

            var itemId = $"rebuild:{volume.MountPoint}";
            var plan = new OptimizationPlan(_engine.Id, new List<string> { itemId });
            var progress = new Progress<TaskProgress>(p => ProgressPercent = p.Percentage);
            var result = await _engine.OptimizeAsync(plan, progress, _cts.Token);

            if (!result.Success || result.ItemsProcessed == 0)
            {
                StatusText = string.Format(
                    LocalizationService._("spotlight.status.error"),
                    "rebuild failed");
                ErrorMessage = "Spotlight rebuild did not succeed. Check privileges.";
                return;
            }

            StatusText = LocalizationService._("spotlight.status.done");
            ProgressPercent = 100;
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("spotlight.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("spotlight.status.error"), ex.Message);
            ErrorMessage = ex.Message;
        }
        finally
        {
            volume.IsBusy = false;
            IsBusy = false;
            // Always clear the pending state — success, failure, or exception —
            // so the inline confirmation row does not leak across runs.
            PendingRebuildVolume = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Refresh the <see cref="EnabledCount"/>/<see cref="DisabledCount"/>
    /// derived properties after a local toggle so the stat cards match the
    /// visible state without requiring a full re-scan. The stored Report is
    /// immutable (record), so we rebuild it with the new counts.
    /// </summary>
    private void RecomputeReportCounts()
    {
        if (_report is null) return;
        var enabled = VolumeItems.Count(v => v.IsIndexed);
        var disabled = VolumeItems.Count - enabled;
        Report = new SpotlightReport(
            Volumes: _report.Volumes,
            TotalVolumes: _report.TotalVolumes,
            EnabledCount: enabled,
            DisabledCount: disabled,
            IsAvailable: _report.IsAvailable);
    }

    // ── INPC ────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
