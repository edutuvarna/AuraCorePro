using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AuraCore.Application;
using AuraCore.Module.PurgeableSpaceManager;
using AuraCore.Module.PurgeableSpaceManager.Models;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>
/// Slim adapter so the VM can be unit-tested without touching the sealed
/// <see cref="PurgeableSpaceManagerModule"/> (which shells out to diskutil
/// / tmutil / find / sudo on macOS). Mirrors the IDnsFlusherEngine /
/// IGrubManagerEngine / IKernelCleanerEngine patterns from 4.3.1-4.4.1.
/// </summary>
public interface IPurgeableSpaceManagerEngine
{
    string Id { get; }
    PurgeableReport? LastReport { get; }
    Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default);
    Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct);
}

/// <summary>Default production adapter over the sealed concrete module.</summary>
public sealed class PurgeableSpaceManagerEngineAdapter : IPurgeableSpaceManagerEngine
{
    private readonly PurgeableSpaceManagerModule _module;
    public PurgeableSpaceManagerEngineAdapter(PurgeableSpaceManagerModule module) =>
        _module = module ?? throw new ArgumentNullException(nameof(module));

    public string Id => _module.Id;
    public PurgeableReport? LastReport => _module.LastReport;

    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default) =>
        _module.ScanAsync(options, ct);

    public Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct) =>
        _module.OptimizeAsync(plan, progress, ct);
}

/// <summary>
/// Phase 4.4.2 Purgeable Space Manager view-model.
/// "Education-first + innovation": hero stacked-bar visualization shows
/// Used / Purgeable / Free proportions of the boot volume, an education
/// card explains the macOS "purgeable" concept, and three cleanup
/// actions are tiered by safety (Clean User Caches + Run Periodic are
/// safe, Thin Snapshots is the aggressive option). Auto-scans on the
/// View's Loaded event — no manual Scan button — and re-scans after
/// any successful action so the bar + stats reflect the new state.
/// </summary>
public sealed class PurgeableSpaceManagerViewModel : INotifyPropertyChanged
{
    private readonly IPurgeableSpaceManagerEngine _engine;
    private CancellationTokenSource? _cts;

    public PurgeableSpaceManagerViewModel(IPurgeableSpaceManagerEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        CleanUserCachesCommand = new AsyncDelegateCommand(
            execute: _ => RunActionAsync("clean-user-caches"),
            canExecute: _ => !IsBusy);
        RunPeriodicCommand = new AsyncDelegateCommand(
            execute: _ => RunActionAsync("run-periodic"),
            canExecute: _ => !IsBusy);
        ThinSnapshotsCommand = new AsyncDelegateCommand(
            execute: _ => RunActionAsync("thin-snapshots"),
            canExecute: _ => !IsBusy);
        CancelCommand = new DelegateCommand(
            execute: _ => { try { _cts?.Cancel(); } catch { /* disposed */ } },
            canExecute: _ => IsBusy);

        StatusText = LocalizationService._("purgeableSpace.status.idle");
    }

    /// <summary>Convenience ctor for DI — wraps the concrete module in the default adapter.</summary>
    public PurgeableSpaceManagerViewModel(PurgeableSpaceManagerModule module)
        : this(new PurgeableSpaceManagerEngineAdapter(module ?? throw new ArgumentNullException(nameof(module))))
    {
    }

    // ── Public surface: report + derived display props ──────────────

    private PurgeableReport? _report;
    public PurgeableReport? Report
    {
        get => _report;
        private set
        {
            if (ReferenceEquals(_report, value)) return;
            _report = value;
            OnPropertyChanged();
            // Derived-byte/percent displays + the stacked-bar column string.
            OnPropertyChanged(nameof(TotalCapacityBytes));
            OnPropertyChanged(nameof(UsedBytes));
            OnPropertyChanged(nameof(PurgeableBytes));
            OnPropertyChanged(nameof(FreeBytes));
            OnPropertyChanged(nameof(UsedPercent));
            OnPropertyChanged(nameof(PurgeablePercent));
            OnPropertyChanged(nameof(FreePercent));
            OnPropertyChanged(nameof(UsedDisplay));
            OnPropertyChanged(nameof(PurgeableDisplay));
            OnPropertyChanged(nameof(FreeDisplay));
            OnPropertyChanged(nameof(UsedPercentDisplay));
            OnPropertyChanged(nameof(PurgeablePercentDisplay));
            OnPropertyChanged(nameof(FreePercentDisplay));
            OnPropertyChanged(nameof(ColumnDefinitions));
            OnPropertyChanged(nameof(SnapshotCount));
            OnPropertyChanged(nameof(SnapshotCountDisplay));
        }
    }

    public long TotalCapacityBytes => _report?.TotalCapacityBytes ?? 0;

    /// <summary>Bytes consumed by the filesystem (capacity minus what the volume
    /// currently reports as free — which includes purgeable content).</summary>
    public long UsedBytes
    {
        get
        {
            if (_report is not { TotalCapacityBytes: > 0 } r) return 0;
            var used = r.TotalCapacityBytes - r.VolumeFreeBytes;
            return used > 0 ? used : 0;
        }
    }

    public long PurgeableBytes => _report?.PurgeableBytes ?? 0;

    /// <summary>Truly free bytes — matches what applications actually see.</summary>
    public long FreeBytes => _report?.ContainerFreeBytes ?? 0;

    public double UsedPercent => ComputePercent(UsedBytes);
    public double PurgeablePercent => ComputePercent(PurgeableBytes);
    public double FreePercent => ComputePercent(FreeBytes);

    private double ComputePercent(long part)
    {
        var total = TotalCapacityBytes;
        if (total <= 0) return 0;
        return 100.0 * part / total;
    }

    /// <summary>
    /// Stacked-bar proportion string consumed by the view. Format
    /// "<usedStars>*,<purgeableStars>*,<freeStars>*" — three star columns
    /// summing to roughly 100. Integer rounding keeps the string stable;
    /// the view re-parses it into <see cref="global::Avalonia.Controls.ColumnDefinitions"/>
    /// whenever this property changes. When <see cref="TotalCapacityBytes"/>
    /// is 0 (engine unavailable / never scanned) we degrade to "1*,0*,0*"
    /// so the bar renders as a single muted segment instead of throwing
    /// on a zero-width Grid.
    /// </summary>
    public string ColumnDefinitions
    {
        get
        {
            if (TotalCapacityBytes <= 0) return "1*,0*,0*";
            var u = Math.Max(0, (int)Math.Round(UsedPercent));
            var p = Math.Max(0, (int)Math.Round(PurgeablePercent));
            var f = Math.Max(0, (int)Math.Round(FreePercent));
            // Guarantee at least one non-zero star so the Grid doesn't collapse.
            if (u == 0 && p == 0 && f == 0) return "1*,0*,0*";
            return $"{u}*,{p}*,{f}*";
        }
    }

    public string UsedDisplay => FormatBytes(UsedBytes);
    public string PurgeableDisplay => FormatBytes(PurgeableBytes);
    public string FreeDisplay => FormatBytes(FreeBytes);

    public string UsedPercentDisplay => FormatPercent(UsedPercent);
    public string PurgeablePercentDisplay => FormatPercent(PurgeablePercent);
    public string FreePercentDisplay => FormatPercent(FreePercent);

    public int SnapshotCount => _report?.LocalSnapshotCount ?? 0;
    public string SnapshotCountDisplay =>
        _report is null ? "--" : SnapshotCount.ToString(CultureInfo.InvariantCulture);

    // ── State / busy / error / status ────────────────────────────────

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

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            (CleanUserCachesCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
            (RunPeriodicCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
            (ThinSnapshotsCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
            (CancelCommand as DelegateCommand)?.RaiseCanExecuteChanged();
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

    public ICommand CleanUserCachesCommand { get; }
    public ICommand RunPeriodicCommand { get; }
    public ICommand ThinSnapshotsCommand { get; }
    public ICommand CancelCommand { get; }

    // ── Operations ──────────────────────────────────────────────────

    /// <summary>
    /// Fires the initial scan. Called from the View's Loaded event.
    /// Matches the 4.3.6 GrubManager / 4.4.1 DnsFlusher pattern —
    /// no user-invoked Scan command since the module auto-scans on
    /// module load and re-scans after every successful action.
    /// </summary>
    public void TriggerInitialScan()
    {
        _ = ScanAsync();
    }

    public async Task ScanAsync()
    {
        if (IsBusy) return;
        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            StatusText = LocalizationService._("purgeableSpace.status.scanning");
            ProgressPercent = 0;

            var result = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            Report = _engine.LastReport;

            if (!result.Success)
            {
                if (Report is { IsAvailable: false })
                    StatusText = LocalizationService._("purgeableSpace.status.unavailable");
                else
                    StatusText = string.Format(
                        LocalizationService._("purgeableSpace.status.error"),
                        result.BlockedReason ?? "scan failed");
                ErrorMessage = result.BlockedReason;
                return;
            }

            StatusText = LocalizationService._("purgeableSpace.status.idle");
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("purgeableSpace.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("purgeableSpace.status.error"), ex.Message);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Invokes an optimization plan with a single item ID ("clean-user-caches",
    /// "run-periodic", "thin-snapshots"), then re-scans on success so the
    /// stacked bar and stats reflect the freed space. Exposed publicly so unit
    /// tests can await the operation without wrapping the async-void
    /// <see cref="AsyncDelegateCommand.Execute"/> pathway.
    /// </summary>
    public async Task RunActionAsync(string itemId)
    {
        if (IsBusy) return;
        _cts = new CancellationTokenSource();
        var beforeFree = FreeBytes;
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            StatusText = LocalizationService._("purgeableSpace.status.cleaning");
            ProgressPercent = 0;

            var plan = new OptimizationPlan(_engine.Id, new List<string> { itemId });
            var progress = new Progress<TaskProgress>(p => ProgressPercent = p.Percentage);
            var result = await _engine.OptimizeAsync(plan, progress, _cts.Token);

            if (!result.Success)
            {
                StatusText = string.Format(
                    LocalizationService._("purgeableSpace.status.error"),
                    "action failed");
                ErrorMessage = "Cleanup action failed.";
                return;
            }

            // Re-scan to refresh the stacked bar, purgeable total, and
            // snapshot count with the post-action state.
            var rescan = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            if (rescan.Success) Report = _engine.LastReport;

            // Completion message includes how much was reclaimed (byte delta
            // against the container-free sample captured before the action).
            var reclaimed = FreeBytes - beforeFree;
            StatusText = string.Format(
                LocalizationService._("purgeableSpace.status.done"),
                FormatBytes(reclaimed > 0 ? reclaimed : 0));
            ProgressPercent = 100;
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("purgeableSpace.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("purgeableSpace.status.error"), ex.Message);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    // ── Byte / percent formatting helpers ───────────────────────────

    /// <summary>
    /// Human-readable byte formatter. "--" when no scan has happened yet,
    /// otherwise chooses the largest binary unit that keeps the number
    /// under ~1000. Format: "12.3 GB" with one decimal for GB/MB, zero
    /// decimals for KB/bytes.
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "--";
        const double kb = 1024.0;
        const double mb = kb * 1024;
        const double gb = mb * 1024;
        const double tb = gb * 1024;

        if (bytes >= tb) return string.Format(CultureInfo.InvariantCulture, "{0:F1} TB", bytes / tb);
        if (bytes >= gb) return string.Format(CultureInfo.InvariantCulture, "{0:F1} GB", bytes / gb);
        if (bytes >= mb) return string.Format(CultureInfo.InvariantCulture, "{0:F1} MB", bytes / mb);
        if (bytes >= kb) return string.Format(CultureInfo.InvariantCulture, "{0:F0} KB", bytes / kb);
        return string.Format(CultureInfo.InvariantCulture, "{0} B", bytes);
    }

    private static string FormatPercent(double value) =>
        string.Format(CultureInfo.InvariantCulture, "{0:F0}%", value);

    // ── INPC ────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
