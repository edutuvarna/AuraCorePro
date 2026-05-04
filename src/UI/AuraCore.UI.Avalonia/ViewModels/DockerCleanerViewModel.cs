using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AuraCore.Application;
using AuraCore.Module.DockerCleaner;
using AuraCore.Module.DockerCleaner.Models;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>
/// Slim adapter so the VM can be unit-tested without touching the real
/// <see cref="DockerCleanerModule"/> (which shells out to docker CLI).
/// The production adapter <see cref="DockerCleanerEngineAdapter"/> forwards
/// to the concrete module. Mirrors the IJournalCleanerEngine / ISnapFlatpakCleanerEngine
/// patterns from 4.3.1 / 4.3.2.
/// </summary>
public interface IDockerCleanerEngine
{
    string Id { get; }
    DockerReport? LastReport { get; }
    Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default);
    Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct);
}

/// <summary>Default production adapter over the sealed concrete module.</summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class DockerCleanerEngineAdapter : IDockerCleanerEngine
{
    private readonly DockerCleanerModule _module;
    public DockerCleanerEngineAdapter(DockerCleanerModule module) =>
        _module = module ?? throw new ArgumentNullException(nameof(module));

    public string Id => _module.Id;
    public DockerReport? LastReport => _module.LastReport;

    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default) =>
        _module.ScanAsync(options, ct);

    public Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct) =>
        _module.OptimizeAsync(plan, progress, ct);
}

/// <summary>
/// Phase 4.3.3 Docker Cleaner view-model.
/// Layout A "Safety-tiered": hero Safe Cleanup card (images + containers + build cache
/// via engine <c>prune-system</c>), 3 granular prune commands, and a gated Danger Zone
/// for volume prune (requires VolumeRiskAcknowledged checkbox).
/// </summary>
public sealed class DockerCleanerViewModel : INotifyPropertyChanged
{
    private readonly IDockerCleanerEngine _engine;
    private CancellationTokenSource? _cts;

    public DockerCleanerViewModel(IDockerCleanerEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        ScanCommand = new AsyncDelegateCommand(
            execute: _ => ScanAsync(),
            canExecute: _ => !IsBusy);

        PruneSafeCommand = new AsyncDelegateCommand(
            execute: _ => PruneAsync("prune-system"),
            canExecute: _ => !IsBusy && DockerAvailable);

        PruneImagesCommand = new AsyncDelegateCommand(
            execute: _ => PruneAsync("prune-dangling-images"),
            canExecute: _ => !IsBusy && DockerAvailable);

        PruneContainersCommand = new AsyncDelegateCommand(
            execute: _ => PruneAsync("prune-containers"),
            canExecute: _ => !IsBusy && DockerAvailable);

        PruneBuildCacheCommand = new AsyncDelegateCommand(
            execute: _ => PruneAsync("prune-build-cache"),
            canExecute: _ => !IsBusy && DockerAvailable);

        PruneVolumesCommand = new AsyncDelegateCommand(
            execute: _ => PruneAsync("prune-volumes"),
            canExecute: _ => !IsBusy && DockerAvailable && VolumeRiskAcknowledged);

        CancelCommand = new DelegateCommand(
            execute: _ => Cancel(),
            canExecute: _ => IsBusy && _cts is not null);

        StatusText = LocalizationService._("dockerCleaner.status.idle");
    }

    /// <summary>Convenience ctor for DI — wraps the concrete module in the default adapter.</summary>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public DockerCleanerViewModel(DockerCleanerModule module)
        : this(new DockerCleanerEngineAdapter(module ?? throw new ArgumentNullException(nameof(module))))
    {
    }

    // ── Public surface ─────────────────────────────────────────────

    private DockerReport? _report;
    public DockerReport? Report
    {
        get => _report;
        private set
        {
            if (ReferenceEquals(_report, value)) return;
            _report = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ImagesDisplay));
            OnPropertyChanged(nameof(ContainersDisplay));
            OnPropertyChanged(nameof(VolumesDisplay));
            OnPropertyChanged(nameof(BuildCacheDisplay));
            OnPropertyChanged(nameof(ReclaimableSafeDisplay));
            OnPropertyChanged(nameof(SafeDescriptionText));
        }
    }

    public string ImagesDisplay =>
        _report is null ? "--" : FormatSize(_report.ImagesTotalBytes);

    public string ContainersDisplay =>
        _report is null
            ? "--"
            : string.Format(
                CultureInfo.InvariantCulture,
                LocalizationService._("dockerCleaner.containers.summary"),
                _report.TotalContainers,
                _report.StoppedContainers);

    public string VolumesDisplay =>
        _report is null ? "--" : FormatSize(_report.VolumesTotalBytes);

    public string BuildCacheDisplay =>
        _report is null ? "--" : FormatSize(_report.BuildCacheBytes);

    /// <summary>Sum of images + containers + build cache reclaimable (excludes volumes — the "safe" savings).</summary>
    public string ReclaimableSafeDisplay =>
        _report is null
            ? "--"
            : FormatSize(_report.ImagesReclaimableBytes
                + _report.ContainersReclaimableBytes
                + _report.BuildCacheReclaimableBytes);

    /// <summary>Pre-formatted description string for the Safe Cleanup card — picks up Report changes.</summary>
    public string SafeDescriptionText =>
        string.Format(LocalizationService._("dockerCleaner.safe.description"), ReclaimableSafeDisplay);

    private bool _dockerAvailable;
    public bool DockerAvailable
    {
        get => _dockerAvailable;
        private set
        {
            if (_dockerAvailable == value) return;
            _dockerAvailable = value;
            OnPropertyChanged();
            RaiseAllCanExecuteChanged();
        }
    }

    private bool _volumeRiskAcknowledged;
    /// <summary>Gated by the Danger Zone checkbox. Resets on every scan + after a volume prune.</summary>
    public bool VolumeRiskAcknowledged
    {
        get => _volumeRiskAcknowledged;
        set
        {
            if (_volumeRiskAcknowledged == value) return;
            _volumeRiskAcknowledged = value;
            OnPropertyChanged();
            (PruneVolumesCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
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
    public ICommand PruneSafeCommand { get; }
    public ICommand PruneImagesCommand { get; }
    public ICommand PruneContainersCommand { get; }
    public ICommand PruneBuildCacheCommand { get; }
    public ICommand PruneVolumesCommand { get; }
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
            StatusText = LocalizationService._("dockerCleaner.status.scanning");

            var result = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            Report = _engine.LastReport;
            DockerAvailable = Report?.DockerAvailable ?? false;
            // Reset acknowledgement whenever a fresh scan lands — prevents accidental re-click on stale confirm.
            VolumeRiskAcknowledged = false;

            if (!result.Success)
            {
                if (!DockerAvailable)
                    StatusText = LocalizationService._("dockerCleaner.status.unavailable");
                else
                    StatusText = string.Format(
                        LocalizationService._("dockerCleaner.status.error"),
                        result.BlockedReason ?? "scan failed");
                ErrorMessage = result.BlockedReason;
                return;
            }

            StatusText = LocalizationService._("dockerCleaner.status.idle");
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("dockerCleaner.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("dockerCleaner.status.error"), ex.Message);
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

    public async Task PruneAsync(string? itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        if (IsBusy) return;
        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ProgressPercent = 0;
            StatusText = LocalizationService._("dockerCleaner.status.pruning");

            var plan = new OptimizationPlan(_engine.Id, new List<string> { itemId });
            var progress = new Progress<TaskProgress>(p => ProgressPercent = p.Percentage);

            var result = await _engine.OptimizeAsync(plan, progress, _cts.Token);

            if (!result.Success)
            {
                StatusText = string.Format(
                    LocalizationService._("dockerCleaner.status.error"),
                    "prune failed");
                ErrorMessage = "Prune operation failed. Check privileges or docker daemon state.";
                return;
            }

            // Re-scan to refresh stats after prune
            var rescan = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            if (rescan.Success)
            {
                Report = _engine.LastReport;
                DockerAvailable = Report?.DockerAvailable ?? false;
            }

            StatusText = string.Format(
                LocalizationService._("dockerCleaner.status.done"),
                FormatSize(result.BytesFreed));

            // Reset acknowledgement after every prune — single-use consent (including volume prune).
            VolumeRiskAcknowledged = false;
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("dockerCleaner.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("dockerCleaner.status.error"), ex.Message);
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
        (PruneSafeCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (PruneImagesCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (PruneContainersCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (PruneBuildCacheCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (PruneVolumesCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as DelegateCommand)?.RaiseCanExecuteChanged();
    }

    // ── INPC ───────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
