using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AuraCore.Application;
using AuraCore.Module.SnapFlatpakCleaner;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>
/// Slim adapter so the VM can be unit-tested without touching the real
/// <see cref="SnapFlatpakCleanerModule"/> (which shells out to snap/flatpak).
/// The production adapter <see cref="SnapFlatpakCleanerEngineAdapter"/> forwards
/// to the concrete module. Mirrors the IJournalCleanerEngine pattern from 4.3.1.
/// </summary>
public interface ISnapFlatpakCleanerEngine
{
    string Id { get; }
    int LastDisabledSnapCount { get; }
    int LastUnusedFlatpakCount { get; }
    bool LastSnapAvailable { get; }
    bool LastFlatpakAvailable { get; }
    Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default);
    Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct);
}

/// <summary>Default production adapter over the sealed concrete module.</summary>
public sealed class SnapFlatpakCleanerEngineAdapter : ISnapFlatpakCleanerEngine
{
    private readonly SnapFlatpakCleanerModule _module;
    public SnapFlatpakCleanerEngineAdapter(SnapFlatpakCleanerModule module) =>
        _module = module ?? throw new ArgumentNullException(nameof(module));

    public string Id => _module.Id;
    public int LastDisabledSnapCount => _module.LastDisabledSnapCount;
    public int LastUnusedFlatpakCount => _module.LastUnusedFlatpakCount;
    public bool LastSnapAvailable => _module.LastSnapAvailable;
    public bool LastFlatpakAvailable => _module.LastFlatpakAvailable;

    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default) =>
        _module.ScanAsync(options, ct);

    public Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct) =>
        _module.OptimizeAsync(plan, progress, ct);
}

/// <summary>
/// Phase 4.3.2 Snap/Flatpak Cleaner view-model.
/// Exposes two per-tool counts (+ availability flags), three cleanup commands
/// (snap-only / flatpak-only / both), progress, cancellation, and localized
/// status copy. Same Phase 4.0 shell as JournalCleanerViewModel.
/// </summary>
public sealed class SnapFlatpakCleanerViewModel : INotifyPropertyChanged
{
    private readonly ISnapFlatpakCleanerEngine _engine;
    private CancellationTokenSource? _cts;
    private bool _hasScanned;

    public SnapFlatpakCleanerViewModel(ISnapFlatpakCleanerEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        ScanCommand = new AsyncDelegateCommand(
            execute: _ => ScanAsync(),
            canExecute: _ => !IsBusy);

        CleanSnapCommand = new AsyncDelegateCommand(
            execute: _ => CleanAsync("snap-clean-all"),
            canExecute: _ => !IsBusy && (_hasScanned ? SnapAvailable : true));

        CleanFlatpakCommand = new AsyncDelegateCommand(
            execute: _ => CleanAsync("flatpak-clean-all"),
            canExecute: _ => !IsBusy && (_hasScanned ? FlatpakAvailable : true));

        CleanBothCommand = new AsyncDelegateCommand(
            execute: _ => CleanBothAsync(),
            canExecute: _ => !IsBusy && (_hasScanned ? (SnapAvailable || FlatpakAvailable) : true));

        CancelCommand = new DelegateCommand(
            execute: _ => Cancel(),
            canExecute: _ => IsBusy && _cts is not null);

        StatusText = LocalizationService._("snapFlatpakCleaner.status.idle");
    }

    /// <summary>Convenience ctor for DI — wraps the concrete module in the default adapter.</summary>
    public SnapFlatpakCleanerViewModel(SnapFlatpakCleanerModule module)
        : this(new SnapFlatpakCleanerEngineAdapter(module ?? throw new ArgumentNullException(nameof(module))))
    {
    }

    // ── Public surface ─────────────────────────────────────────────

    private int _snapDisabledCount;
    public int SnapDisabledCount
    {
        get => _snapDisabledCount;
        private set
        {
            if (_snapDisabledCount == value) return;
            _snapDisabledCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SnapDisplay));
        }
    }

    private int _flatpakUnusedCount;
    public int FlatpakUnusedCount
    {
        get => _flatpakUnusedCount;
        private set
        {
            if (_flatpakUnusedCount == value) return;
            _flatpakUnusedCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FlatpakDisplay));
        }
    }

    private bool _snapAvailable;
    public bool SnapAvailable
    {
        get => _snapAvailable;
        private set
        {
            if (_snapAvailable == value) return;
            _snapAvailable = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SnapDisplay));
            RaiseAllCanExecuteChanged();
        }
    }

    private bool _flatpakAvailable;
    public bool FlatpakAvailable
    {
        get => _flatpakAvailable;
        private set
        {
            if (_flatpakAvailable == value) return;
            _flatpakAvailable = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FlatpakDisplay));
            RaiseAllCanExecuteChanged();
        }
    }

    /// <summary>"--" when snap tooling is missing after a scan, else the count.</summary>
    public string SnapDisplay =>
        !_hasScanned ? "--" :
        SnapAvailable ? SnapDisabledCount.ToString(CultureInfo.InvariantCulture) : "--";

    /// <summary>"--" when flatpak tooling is missing after a scan, else the count.</summary>
    public string FlatpakDisplay =>
        !_hasScanned ? "--" :
        FlatpakAvailable ? FlatpakUnusedCount.ToString(CultureInfo.InvariantCulture) : "--";

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
    public ICommand CleanSnapCommand { get; }
    public ICommand CleanFlatpakCommand { get; }
    public ICommand CleanBothCommand { get; }
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
            StatusText = LocalizationService._("snapFlatpakCleaner.status.scanning");

            var result = await _engine.ScanAsync(new ScanOptions(), _cts.Token);

            // Always refresh the stat surface from whatever the engine recorded,
            // so UI transitions from "--" to real numbers (or to "--" for missing tools).
            SnapAvailable = _engine.LastSnapAvailable;
            FlatpakAvailable = _engine.LastFlatpakAvailable;
            SnapDisabledCount = _engine.LastDisabledSnapCount;
            FlatpakUnusedCount = _engine.LastUnusedFlatpakCount;
            _hasScanned = true;
            OnPropertyChanged(nameof(SnapDisplay));
            OnPropertyChanged(nameof(FlatpakDisplay));

            if (!result.Success)
            {
                if (!SnapAvailable && !FlatpakAvailable)
                    StatusText = LocalizationService._("snapFlatpakCleaner.status.unavailable");
                else
                    StatusText = string.Format(
                        LocalizationService._("snapFlatpakCleaner.status.error"),
                        result.BlockedReason ?? "scan failed");
                ErrorMessage = result.BlockedReason;
                return;
            }

            StatusText = LocalizationService._("snapFlatpakCleaner.status.idle");
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("snapFlatpakCleaner.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("snapFlatpakCleaner.status.error"), ex.Message);
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

    public async Task CleanAsync(string? itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        if (IsBusy) return;
        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ProgressPercent = 0;
            StatusText = LocalizationService._("snapFlatpakCleaner.status.cleaning");

            var plan = new OptimizationPlan(_engine.Id, new List<string> { itemId });
            var progress = new Progress<TaskProgress>(p => ProgressPercent = p.Percentage);

            var result = await _engine.OptimizeAsync(plan, progress, _cts.Token);

            if (!result.Success)
            {
                StatusText = string.Format(
                    LocalizationService._("snapFlatpakCleaner.status.error"),
                    "cleanup failed");
                ErrorMessage = "Cleanup operation failed. Check privileges.";
                return;
            }

            // Re-scan to refresh counts after cleanup
            var rescan = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            if (rescan.Success)
            {
                SnapAvailable = _engine.LastSnapAvailable;
                FlatpakAvailable = _engine.LastFlatpakAvailable;
                SnapDisabledCount = _engine.LastDisabledSnapCount;
                FlatpakUnusedCount = _engine.LastUnusedFlatpakCount;
            }

            StatusText = string.Format(
                LocalizationService._("snapFlatpakCleaner.status.done"),
                result.ItemsProcessed.ToString(CultureInfo.InvariantCulture));
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("snapFlatpakCleaner.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("snapFlatpakCleaner.status.error"), ex.Message);
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
    /// Snap clean-all then flatpak clean-all, sequentially within a single command.
    /// Stops on first failure with an error message.
    /// </summary>
    public async Task CleanBothAsync()
    {
        if (IsBusy) return;
        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ProgressPercent = 0;
            StatusText = LocalizationService._("snapFlatpakCleaner.status.cleaning");

            int totalProcessed = 0;
            var progress = new Progress<TaskProgress>(p => ProgressPercent = p.Percentage);

            // Step 1: snap clean (only if available)
            if (_hasScanned && SnapAvailable || !_hasScanned)
            {
                var snapPlan = new OptimizationPlan(_engine.Id, new List<string> { "snap-clean-all" });
                var snapResult = await _engine.OptimizeAsync(snapPlan, progress, _cts.Token);
                if (!snapResult.Success)
                {
                    StatusText = string.Format(
                        LocalizationService._("snapFlatpakCleaner.status.error"),
                        "snap cleanup failed");
                    ErrorMessage = "Snap cleanup operation failed. Check privileges.";
                    return;
                }
                totalProcessed += snapResult.ItemsProcessed;
            }

            // Step 2: flatpak clean (only if available)
            if (_hasScanned && FlatpakAvailable || !_hasScanned)
            {
                var flatpakPlan = new OptimizationPlan(_engine.Id, new List<string> { "flatpak-clean-all" });
                var flatpakResult = await _engine.OptimizeAsync(flatpakPlan, progress, _cts.Token);
                if (!flatpakResult.Success)
                {
                    StatusText = string.Format(
                        LocalizationService._("snapFlatpakCleaner.status.error"),
                        "flatpak cleanup failed");
                    ErrorMessage = "Flatpak cleanup operation failed. Check privileges.";
                    return;
                }
                totalProcessed += flatpakResult.ItemsProcessed;
            }

            // Re-scan to refresh counts
            var rescan = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            if (rescan.Success)
            {
                SnapAvailable = _engine.LastSnapAvailable;
                FlatpakAvailable = _engine.LastFlatpakAvailable;
                SnapDisabledCount = _engine.LastDisabledSnapCount;
                FlatpakUnusedCount = _engine.LastUnusedFlatpakCount;
            }

            StatusText = string.Format(
                LocalizationService._("snapFlatpakCleaner.status.done"),
                totalProcessed.ToString(CultureInfo.InvariantCulture));
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("snapFlatpakCleaner.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("snapFlatpakCleaner.status.error"), ex.Message);
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

    private void RaiseAllCanExecuteChanged()
    {
        (ScanCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (CleanSnapCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (CleanFlatpakCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (CleanBothCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as DelegateCommand)?.RaiseCanExecuteChanged();
    }

    // ── INPC ───────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
