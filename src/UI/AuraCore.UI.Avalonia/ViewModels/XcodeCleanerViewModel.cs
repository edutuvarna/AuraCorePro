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
using AuraCore.Module.XcodeCleaner;
using AuraCore.Module.XcodeCleaner.Models;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>
/// Slim adapter so the VM can be unit-tested without touching the sealed
/// <see cref="XcodeCleanerModule"/> (which shells out to du / xcrun simctl
/// on macOS). Mirrors the IDnsFlusherEngine / IPurgeableSpaceManagerEngine
/// / ISpotlightEngine pattern from 4.4.1-4.4.3.
/// </summary>
public interface IXcodeCleanerEngine
{
    string Id { get; }
    XcodeCleanerReport? LastReport { get; }
    Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default);
    Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct);
}

/// <summary>Default production adapter over the sealed concrete module.</summary>
public sealed class XcodeCleanerEngineAdapter : IXcodeCleanerEngine
{
    private readonly XcodeCleanerModule _module;
    public XcodeCleanerEngineAdapter(XcodeCleanerModule module) =>
        _module = module ?? throw new ArgumentNullException(nameof(module));

    public string Id => _module.Id;
    public XcodeCleanerReport? LastReport => _module.LastReport;

    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default) =>
        _module.ScanAsync(options, ct);

    public Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct) =>
        _module.OptimizeAsync(plan, progress, ct);
}

/// <summary>
/// Per-category row view-model. The VM composes 3 risk buckets
/// (safe/granular/danger) of <see cref="CategoryItemVM"/> instances.
/// <see cref="IsPseudo"/> is true for the <c>unavailable-simulators</c>
/// row — an action-only entry that has no measurable pre-execution size
/// (the engine can't enumerate stale simulators without running simctl).
/// </summary>
public sealed class CategoryItemVM : INotifyPropertyChanged
{
    public CategoryItemVM(string id, string name, long sizeBytes, DateTime? oldestItem, bool exists, bool isPseudo = false)
    {
        Id = id ?? string.Empty;
        Name = name ?? string.Empty;
        SizeBytes = sizeBytes;
        OldestItem = oldestItem;
        Exists = exists;
        IsPseudo = isPseudo;
        SizeDisplay = isPseudo
            ? "--"
            : XcodeCleanerViewModel.FormatSize(sizeBytes);
        AgeHint = oldestItem is null
            ? null
            : XcodeCleanerViewModel.HumanizeAge(oldestItem.Value);
    }

    public string Id { get; }
    public string Name { get; }
    public long SizeBytes { get; }
    public DateTime? OldestItem { get; }
    public bool Exists { get; }
    public bool IsPseudo { get; }

    public string SizeDisplay { get; }
    public string? AgeHint { get; }

    /// <summary>Rows that don't exist OR pseudo-rows that always allow action still use Exists to gate the Prune button.</summary>
    public bool IsEnabled => Exists || IsPseudo;

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

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Phase 4.4.4 Xcode Cleaner view-model.
/// "Layout A Safety-tiered" (Docker 4.3.3 clone, macOS-specialized): three
/// risk buckets as separate GlassCards. Safe Cleanup (hero) wraps the
/// engine's <c>all</c> itemId which bundles derived-data + simulator-caches
/// + xcode-cache + unavailable-simulators; Granular Control exposes
/// simulator-devices + stale simulators; Danger Zone gates archives +
/// iOS/watchOS/tvOS device-support behind an acknowledgement checkbox
/// which resets on every scan and every successful prune.
/// Auto-scans on the View's Loaded event — no manual Scan button.
/// </summary>
public sealed class XcodeCleanerViewModel : INotifyPropertyChanged
{
    // Category IDs matching the engine's CategoryDefs
    internal const string IdDerivedData = "derived-data";
    internal const string IdArchives = "archives";
    internal const string IdSimulatorCaches = "simulator-caches";
    internal const string IdSimulatorDevices = "simulator-devices";
    internal const string IdXcodeCache = "xcode-cache";
    internal const string IdIosDeviceSupport = "ios-device-support";
    internal const string IdWatchosDeviceSupport = "watchos-device-support";
    internal const string IdTvosDeviceSupport = "tvos-device-support";
    internal const string IdUnavailableSimulators = "unavailable-simulators";
    internal const string IdAll = "all";

    private const int TotalCategoryCount = 8;

    private readonly IXcodeCleanerEngine _engine;
    private CancellationTokenSource? _cts;

    public XcodeCleanerViewModel(IXcodeCleanerEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        SafeCategoriesItems = new ObservableCollection<CategoryItemVM>();
        GranularCategoriesItems = new ObservableCollection<CategoryItemVM>();
        DangerCategoriesItems = new ObservableCollection<CategoryItemVM>();

        PruneSafeCommand = new AsyncDelegateCommand(
            execute: _ => PruneItemAsync(IdAll),
            canExecute: _ => !IsBusy);

        PruneCategoryCommand = new AsyncDelegateCommand(
            execute: p => PruneCategoryAsync(p as CategoryItemVM),
            canExecute: p => !IsBusy && p is CategoryItemVM c && c.IsEnabled);

        PruneDangerAllCommand = new AsyncDelegateCommand(
            execute: _ => PruneDangerAllAsync(),
            canExecute: _ => !IsBusy && DangerAcknowledged);

        CancelCommand = new DelegateCommand(
            execute: _ => Cancel(),
            canExecute: _ => IsBusy && _cts is not null);

        StatusText = LocalizationService._("xcodeCleaner.status.idle");
    }

    /// <summary>Convenience ctor for DI — wraps the concrete module in the default adapter.</summary>
    public XcodeCleanerViewModel(XcodeCleanerModule module)
        : this(new XcodeCleanerEngineAdapter(module ?? throw new ArgumentNullException(nameof(module))))
    {
    }

    // ── Public surface ──────────────────────────────────────────────

    private XcodeCleanerReport? _report;
    public XcodeCleanerReport? Report
    {
        get => _report;
        private set
        {
            if (ReferenceEquals(_report, value)) return;
            _report = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(XcodeInstalled));
            OnPropertyChanged(nameof(TotalDisplay));
            OnPropertyChanged(nameof(CategoriesPresentDisplay));
            OnPropertyChanged(nameof(OldestItemDisplay));
            OnPropertyChanged(nameof(SafeReclaimableDisplay));
            OnPropertyChanged(nameof(SafeDescriptionText));
        }
    }

    public bool XcodeInstalled => _report?.XcodeInstalled ?? false;

    public string TotalDisplay =>
        _report is null ? "--" : FormatSize(_report.TotalBytes);

    public string CategoriesPresentDisplay
    {
        get
        {
            if (_report is null) return "--";
            var existing = _report.Categories?.Count(c => c.Exists) ?? 0;
            return string.Format(
                LocalizationService._("xcodeCleaner.stat.categoriesValue"),
                existing,
                TotalCategoryCount);
        }
    }

    public string OldestItemDisplay
    {
        get
        {
            if (_report?.Categories is null) return "--";
            DateTime? oldest = null;
            foreach (var c in _report.Categories)
            {
                if (!c.Exists || c.OldestItem is null) continue;
                if (oldest is null || c.OldestItem.Value < oldest.Value)
                    oldest = c.OldestItem;
            }
            return oldest is null ? "--" : HumanizeAge(oldest.Value);
        }
    }

    /// <summary>Sum of the three always-safe categories used by the Safe Cleanup hero.</summary>
    public long SafeReclaimableBytes
    {
        get
        {
            if (_report?.Categories is null) return 0;
            long sum = 0;
            foreach (var c in _report.Categories)
            {
                if (!c.Exists) continue;
                if (c.Id == IdDerivedData || c.Id == IdSimulatorCaches || c.Id == IdXcodeCache)
                    sum += c.SizeBytes;
            }
            return sum;
        }
    }

    public string SafeReclaimableDisplay => FormatSize(SafeReclaimableBytes);

    public string SafeDescriptionText =>
        string.Format(LocalizationService._("xcodeCleaner.safe.description"), SafeReclaimableDisplay);

    public ObservableCollection<CategoryItemVM> SafeCategoriesItems { get; }
    public ObservableCollection<CategoryItemVM> GranularCategoriesItems { get; }
    public ObservableCollection<CategoryItemVM> DangerCategoriesItems { get; }

    private bool _dangerAcknowledged;
    /// <summary>Gated by the Danger Zone checkbox. Resets on every scan + after a danger-zone prune.</summary>
    public bool DangerAcknowledged
    {
        get => _dangerAcknowledged;
        set
        {
            if (_dangerAcknowledged == value) return;
            _dangerAcknowledged = value;
            OnPropertyChanged();
            (PruneDangerAllCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        }
    }

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

    public ICommand PruneSafeCommand { get; }
    public ICommand PruneCategoryCommand { get; }
    public ICommand PruneDangerAllCommand { get; }
    public ICommand CancelCommand { get; }

    // ── Operations ──────────────────────────────────────────────────

    /// <summary>Fires the initial scan. Called from the View's Loaded event.</summary>
    public void TriggerInitialScan() => _ = ScanAsync();

    public async Task ScanAsync()
    {
        if (IsBusy) return;
        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ProgressPercent = 0;
            StatusText = LocalizationService._("xcodeCleaner.status.scanning");

            var result = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            Report = _engine.LastReport;
            RebuildCategoryBuckets();

            // Reset acknowledgement whenever a fresh scan lands.
            DangerAcknowledged = false;

            if (!result.Success)
            {
                if (Report is { XcodeInstalled: false })
                    StatusText = LocalizationService._("xcodeCleaner.status.unavailable");
                else
                    StatusText = string.Format(
                        LocalizationService._("xcodeCleaner.status.error"),
                        result.BlockedReason ?? "scan failed");
                ErrorMessage = result.BlockedReason;
                return;
            }

            if (!XcodeInstalled)
            {
                StatusText = LocalizationService._("xcodeCleaner.status.unavailable");
                return;
            }

            StatusText = LocalizationService._("xcodeCleaner.status.idle");
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("xcodeCleaner.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("xcodeCleaner.status.error"), ex.Message);
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

    private void RebuildCategoryBuckets()
    {
        SafeCategoriesItems.Clear();
        GranularCategoriesItems.Clear();
        DangerCategoriesItems.Clear();

        if (_report?.Categories is null) return;

        CategoryItemVM MakeFromCategory(string id)
        {
            var cat = _report.Categories.FirstOrDefault(c => c.Id == id);
            if (cat is null)
                return new CategoryItemVM(id, LocalizedName(id), 0, null, exists: false);
            return new CategoryItemVM(id, LocalizedName(id), cat.SizeBytes, cat.OldestItem, cat.Exists);
        }

        // Safe bucket: derived-data + simulator-caches + xcode-cache (always safe rebuild)
        SafeCategoriesItems.Add(MakeFromCategory(IdDerivedData));
        SafeCategoriesItems.Add(MakeFromCategory(IdSimulatorCaches));
        SafeCategoriesItems.Add(MakeFromCategory(IdXcodeCache));

        // Granular bucket: simulator-devices + unavailable-simulators (pseudo)
        GranularCategoriesItems.Add(MakeFromCategory(IdSimulatorDevices));
        GranularCategoriesItems.Add(new CategoryItemVM(
            IdUnavailableSimulators,
            LocalizedName(IdUnavailableSimulators),
            sizeBytes: 0,
            oldestItem: null,
            exists: true,
            isPseudo: true));

        // Danger bucket: archives + 3 device-support variants
        DangerCategoriesItems.Add(MakeFromCategory(IdArchives));
        DangerCategoriesItems.Add(MakeFromCategory(IdIosDeviceSupport));
        DangerCategoriesItems.Add(MakeFromCategory(IdWatchosDeviceSupport));
        DangerCategoriesItems.Add(MakeFromCategory(IdTvosDeviceSupport));
    }

    private static string LocalizedName(string id) => id switch
    {
        IdDerivedData => LocalizationService._("xcodeCleaner.category.derivedData"),
        IdArchives => LocalizationService._("xcodeCleaner.category.archives"),
        IdSimulatorCaches => LocalizationService._("xcodeCleaner.category.simulatorCaches"),
        IdSimulatorDevices => LocalizationService._("xcodeCleaner.category.simulatorDevices"),
        IdXcodeCache => LocalizationService._("xcodeCleaner.category.xcodeCache"),
        IdIosDeviceSupport => LocalizationService._("xcodeCleaner.category.iosDeviceSupport"),
        IdWatchosDeviceSupport => LocalizationService._("xcodeCleaner.category.watchosDeviceSupport"),
        IdTvosDeviceSupport => LocalizationService._("xcodeCleaner.category.tvosDeviceSupport"),
        IdUnavailableSimulators => LocalizationService._("xcodeCleaner.category.unavailableSimulators"),
        _ => id,
    };

    /// <summary>Dispatches the engine with a single item id. Exposed for tests.</summary>
    public async Task PruneItemAsync(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        await ExecuteOptimizeAsync(new[] { itemId }, itemId);
    }

    /// <summary>Dispatches a per-category prune (safe / granular / danger category row).</summary>
    public async Task PruneCategoryAsync(CategoryItemVM? category)
    {
        if (category is null) return;
        if (!category.IsEnabled) return;
        category.IsBusy = true;
        try
        {
            await ExecuteOptimizeAsync(new[] { category.Id }, category.Id);
        }
        finally
        {
            category.IsBusy = false;
        }
    }

    /// <summary>Dispatches the danger-zone combined sequence (archives + 3 device-support).</summary>
    public async Task PruneDangerAllAsync()
    {
        if (!DangerAcknowledged) return;
        var items = new List<string>
        {
            IdArchives,
            IdIosDeviceSupport,
            IdWatchosDeviceSupport,
            IdTvosDeviceSupport,
        };
        await ExecuteOptimizeAsync(items, "danger-all");
    }

    private async Task ExecuteOptimizeAsync(IReadOnlyList<string> itemIds, string statusLabel)
    {
        if (IsBusy) return;
        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ProgressPercent = 0;
            StatusText = string.Format(
                LocalizationService._("xcodeCleaner.status.pruning"),
                statusLabel);

            var plan = new OptimizationPlan(_engine.Id, itemIds);
            var progress = new Progress<TaskProgress>(p => ProgressPercent = p.Percentage);

            var result = await _engine.OptimizeAsync(plan, progress, _cts.Token);

            if (!result.Success)
            {
                StatusText = string.Format(
                    LocalizationService._("xcodeCleaner.status.error"),
                    "prune failed");
                ErrorMessage = "Prune operation failed. Check disk permissions.";
                return;
            }

            // Re-scan to refresh sizes + re-render buckets
            var rescan = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            if (rescan.Success)
            {
                Report = _engine.LastReport;
                RebuildCategoryBuckets();
            }

            // Reset acknowledgement after every prune so future danger-zone actions
            // require a fresh tick.
            DangerAcknowledged = false;

            StatusText = string.Format(
                LocalizationService._("xcodeCleaner.status.done"),
                FormatSize(result.BytesFreed));
            ProgressPercent = 100;
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("xcodeCleaner.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("xcodeCleaner.status.error"), ex.Message);
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

    /// <summary>Humanize an age relative to UTC now: "3 days" / "8 months" / "2 years".</summary>
    public static string HumanizeAge(DateTime pastUtc)
    {
        var now = DateTime.UtcNow;
        if (pastUtc > now) return "0 days";
        var delta = now - pastUtc;
        int days = (int)delta.TotalDays;
        if (days < 1) return "<1 day";
        if (days < 30) return $"{days} day{(days == 1 ? "" : "s")}";
        int months = days / 30;
        if (months < 12) return $"{months} month{(months == 1 ? "" : "s")}";
        int years = days / 365;
        return $"{years} year{(years == 1 ? "" : "s")}";
    }

    private void RaiseAllCanExecuteChanged()
    {
        (PruneSafeCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (PruneCategoryCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (PruneDangerAllCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as DelegateCommand)?.RaiseCanExecuteChanged();
    }

    // ── INPC ───────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
