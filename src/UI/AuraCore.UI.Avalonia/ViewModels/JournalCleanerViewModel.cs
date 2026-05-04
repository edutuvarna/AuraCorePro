using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.JournalCleaner;
using AuraCore.Module.JournalCleaner.Models;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>
/// Slim adapter so the VM can be exercised in unit tests without touching
/// <see cref="JournalCleanerModule"/>'s shell-out code paths. The production
/// adapter <see cref="JournalCleanerEngineAdapter"/> forwards to the real module.
/// </summary>
public interface IJournalCleanerEngine
{
    string Id { get; }
    JournalReport? LastReport { get; }
    Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default);
    Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct);

    /// <summary>Phase 6.17 Wave F — rich-result vacuum operation with privilege guard.</summary>
    Task<OperationResult> RunOperationAsync(OptimizationPlan plan, IPrivilegedActionGuard guard, IProgress<TaskProgress>? progress, CancellationToken ct);
}

/// <summary>Default production adapter over the sealed concrete module.</summary>
[SupportedOSPlatform("linux")]
public sealed class JournalCleanerEngineAdapter : IJournalCleanerEngine
{
    private readonly JournalCleanerModule _module;
    public JournalCleanerEngineAdapter(JournalCleanerModule module) => _module = module ?? throw new ArgumentNullException(nameof(module));
    public string Id => _module.Id;
    public JournalReport? LastReport => _module.LastReport;
    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default) => _module.ScanAsync(options, ct);
    public Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct) =>
        _module.OptimizeAsync(plan, progress, ct);
    public Task<OperationResult> RunOperationAsync(OptimizationPlan plan, IPrivilegedActionGuard guard, IProgress<TaskProgress>? progress, CancellationToken ct) =>
        _module.RunOperationAsync(plan, guard, progress, ct);
}

/// <summary>
/// Phase 4.3.1 Journal Cleaner view-model. Thin MVVM wrapper over
/// <see cref="JournalCleanerModule"/> — exposes the scan report as display strings,
/// four vacuum commands (500m / 1g / 7days / 30days), progress, cancellation,
/// and localized status copy.
/// </summary>
public sealed class JournalCleanerViewModel : INotifyPropertyChanged
{
    private readonly IJournalCleanerEngine _engine;
    private readonly IPrivilegedActionGuard? _guard;
    private CancellationTokenSource? _cts;

    public JournalCleanerViewModel(IJournalCleanerEngine engine, IPrivilegedActionGuard? guard = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _guard = guard;

        ScanCommand = new AsyncDelegateCommand(
            execute: _ => ScanAsync(),
            canExecute: _ => !IsBusy);

        VacuumCommand = new AsyncDelegateCommand(
            execute: arg => VacuumAsync(arg as string),
            canExecute: _ => !IsBusy);

        CancelCommand = new DelegateCommand(
            execute: _ => Cancel(),
            canExecute: _ => IsBusy && _cts is not null);

        StatusText = LocalizationService._("journalCleaner.status.idle");
    }

    /// <summary>Convenience ctor for DI — wraps the concrete module in the default adapter.</summary>
    [SupportedOSPlatform("linux")]
    public JournalCleanerViewModel(JournalCleanerModule module, IPrivilegedActionGuard? guard = null)
        : this(new JournalCleanerEngineAdapter(module ?? throw new ArgumentNullException(nameof(module))), guard)
    {
    }

    // ── Public surface ─────────────────────────────────────────────

    private JournalReport? _report;
    public JournalReport? Report
    {
        get => _report;
        private set
        {
            if (ReferenceEquals(_report, value)) return;
            _report = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentUsageDisplay));
            OnPropertyChanged(nameof(FileCountDisplay));
            OnPropertyChanged(nameof(OldestEntryDisplay));
        }
    }

    public string CurrentUsageDisplay =>
        _report is null ? "--" : FormatSize(_report.CurrentBytes);

    public string FileCountDisplay
    {
        get
        {
            if (_report is null) return "--";
            return _report.JournalFileCount == 1
                ? "1 file"
                : $"{_report.JournalFileCount} files";
        }
    }

    public string OldestEntryDisplay =>
        _report?.OldestEntry is { } dt
            ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : "--";

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
            (ScanCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
            (VacuumCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
            (CancelCommand as DelegateCommand)?.RaiseCanExecuteChanged();
        }
    }

    private double _progressPercent;
    public double ProgressPercent
    {
        get => _progressPercent;
        private set
        {
            // Avoid fighting floating-point jitter from IProgress<T> callbacks
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

    // ── Phase 6.17 Wave F: post-action banner (VM-bound) ──
    private OperationResult? _lastOperationResult;
    public OperationResult? LastOperationResult
    {
        get => _lastOperationResult;
        private set
        {
            if (ReferenceEquals(_lastOperationResult, value)) return;
            _lastOperationResult = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowPostActionBanner));
            OnPropertyChanged(nameof(PostActionBannerText));
            OnPropertyChanged(nameof(PostActionBannerForeground));
        }
    }

    public bool ShowPostActionBanner => _lastOperationResult is not null;

    public string PostActionBannerText => _lastOperationResult is null ? string.Empty : _lastOperationResult.Status switch
    {
        OperationStatus.Success => string.Format(LocalizationService._("op.result.success"),
            FormatSize(_lastOperationResult.BytesFreed), _lastOperationResult.ItemsAffected, _lastOperationResult.Duration.TotalSeconds),
        OperationStatus.Skipped => string.Format(LocalizationService._("op.result.skipped"), _lastOperationResult.Reason ?? string.Empty),
        OperationStatus.Failed  => string.Format(LocalizationService._("op.result.failed"), _lastOperationResult.Reason ?? string.Empty),
        _                       => string.Empty,
    };

    public IBrush PostActionBannerForeground => _lastOperationResult is null
        ? new SolidColorBrush(Color.Parse("#9CA3AF"))
        : _lastOperationResult.Status switch
        {
            OperationStatus.Success => new SolidColorBrush(Color.Parse("#10B981")),
            OperationStatus.Skipped => new SolidColorBrush(Color.Parse("#F59E0B")),
            OperationStatus.Failed  => new SolidColorBrush(Color.Parse("#EF4444")),
            _                       => new SolidColorBrush(Color.Parse("#9CA3AF")),
        };

    public ICommand ScanCommand { get; }
    public ICommand VacuumCommand { get; }
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
            StatusText = LocalizationService._("journalCleaner.status.scanning");

            var result = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            Report = _engine.LastReport;

            if (!result.Success)
            {
                if (Report is { IsAvailable: false })
                    StatusText = LocalizationService._("journalCleaner.status.unavailable");
                else
                    StatusText = string.Format(
                        LocalizationService._("journalCleaner.status.error"),
                        result.BlockedReason ?? "scan failed");
                ErrorMessage = result.BlockedReason;
                return;
            }

            StatusText = LocalizationService._("journalCleaner.status.idle");
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("journalCleaner.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("journalCleaner.status.error"), ex.Message);
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

    public async Task VacuumAsync(string? itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        if (IsBusy) return;
        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ProgressPercent = 0;
            StatusText = LocalizationService._("journalCleaner.status.vacuuming");

            var plan = new OptimizationPlan(_engine.Id, new List<string> { itemId });
            var progress = new Progress<TaskProgress>(p => ProgressPercent = p.Percentage);

            // Phase 6.17 Wave F — when guard is wired, route via RunOperationAsync to surface banner.
            if (_guard is not null)
            {
                var opResult = await _engine.RunOperationAsync(plan, _guard, progress, _cts.Token);
                LastOperationResult = opResult;

                if (opResult.Status != OperationStatus.Success)
                {
                    StatusText = string.Format(
                        LocalizationService._("journalCleaner.status.error"),
                        opResult.Reason ?? "vacuum failed");
                    ErrorMessage = opResult.Reason;
                    return;
                }

                // Re-scan to refresh stats after vacuum
                var rescan = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
                if (rescan.Success) Report = _engine.LastReport;

                StatusText = string.Format(
                    LocalizationService._("journalCleaner.status.done"),
                    FormatSize(opResult.BytesFreed));
                return;
            }

            // Legacy path (no guard wired — used in unit tests)
            var result = await _engine.OptimizeAsync(plan, progress, _cts.Token);

            if (!result.Success)
            {
                StatusText = string.Format(
                    LocalizationService._("journalCleaner.status.error"),
                    "vacuum failed");
                ErrorMessage = "Vacuum operation failed. Check privileges.";
                return;
            }

            // Re-scan to refresh stats after vacuum
            var rescan2 = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            if (rescan2.Success) Report = _engine.LastReport;

            StatusText = string.Format(
                LocalizationService._("journalCleaner.status.done"),
                FormatSize(result.BytesFreed));
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("journalCleaner.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("journalCleaner.status.error"), ex.Message);
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

    // ── INPC ───────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
