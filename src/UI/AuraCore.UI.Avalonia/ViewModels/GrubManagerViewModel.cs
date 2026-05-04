using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AuraCore.Application;
using AuraCore.Module.GrubManager;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>
/// Slim adapter so the VM can be unit-tested without touching the real
/// <see cref="GrubManagerModule"/> (which touches /etc/default/grub +
/// sudo). Mirrors the IKernelCleanerEngine / IDockerCleanerEngine /
/// IJournalCleanerEngine patterns from 4.3.1-4.3.5.
/// </summary>
public interface IGrubManagerEngine
{
    string Id { get; }
    GrubSettings? LastSettings { get; }
    Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default);
    Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct);
    Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default);
    Task RollbackAsync(string operationId, CancellationToken ct = default);
}

/// <summary>Default production adapter over the sealed concrete module.</summary>
[SupportedOSPlatform("linux")]
public sealed class GrubManagerEngineAdapter : IGrubManagerEngine
{
    private readonly GrubManagerModule _module;
    public GrubManagerEngineAdapter(GrubManagerModule module) =>
        _module = module ?? throw new ArgumentNullException(nameof(module));

    public string Id => _module.Id;
    public GrubSettings? LastSettings => _module.LastSettings;

    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default) =>
        _module.ScanAsync(options, ct);

    public Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct) =>
        _module.OptimizeAsync(plan, progress, ct);

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default) =>
        _module.CanRollbackAsync(operationId, ct);

    public Task RollbackAsync(string operationId, CancellationToken ct = default) =>
        _module.RollbackAsync(operationId, ct);
}

/// <summary>
/// Phase 4.3.6 GRUB Manager view-model.
/// Form-based layout with three editable controls (Slider for timeout,
/// ComboBox for GRUB_DEFAULT, ToggleSwitch for os-prober) each binding to
/// Pending* values. HasPendingChanges flips when Pending diverges from
/// Current. Apply is gated by BackupAcknowledged. No manual Scan button —
/// the View fires <see cref="TriggerInitialScan"/> via its Loaded event,
/// and Apply/Rollback auto-rescan on success.
/// </summary>
public sealed class GrubManagerViewModel : INotifyPropertyChanged
{
    private readonly IGrubManagerEngine _engine;
    private CancellationTokenSource? _cts;
    private bool _suppressDirtyRaise;

    private const string BackupPath = "/etc/default/grub.bak.auracore";

    public GrubManagerViewModel(IGrubManagerEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        ApplyChangesCommand = new AsyncDelegateCommand(
            execute: _ => ApplyAsync(),
            canExecute: _ => !IsBusy && HasPendingChanges && BackupAcknowledged);

        ResetCommand = new DelegateCommand(
            execute: _ => Reset(),
            canExecute: _ => !IsBusy && HasPendingChanges);

        RollbackCommand = new AsyncDelegateCommand(
            execute: _ => RollbackAsync(),
            canExecute: _ => !IsBusy && HasBackup);

        CancelCommand = new DelegateCommand(
            execute: _ => Cancel(),
            canExecute: _ => IsBusy && _cts is not null);

        StatusText = LocalizationService._("grubManager.status.idle");

        // Populate combo options with a sensible default list (0..10 + "saved").
        // The view binds ComboBox.Items to this collection.
        DefaultEntryOptions = BuildDefaultEntryOptions(installedKernelCount: 0);
    }

    /// <summary>Convenience ctor for DI — wraps the concrete module in the default adapter.</summary>
    [SupportedOSPlatform("linux")]
    public GrubManagerViewModel(GrubManagerModule module)
        : this(new GrubManagerEngineAdapter(module ?? throw new ArgumentNullException(nameof(module))))
    {
    }

    // ── Current (last-scanned) values ──────────────────────────────

    private int _currentTimeout;
    public int CurrentTimeout
    {
        get => _currentTimeout;
        private set
        {
            if (_currentTimeout == value) return;
            _currentTimeout = value;
            OnPropertyChanged();
            RaisePendingChanged();
        }
    }

    private string _currentGrubDefault = "0";
    public string CurrentGrubDefault
    {
        get => _currentGrubDefault;
        private set
        {
            if (_currentGrubDefault == value) return;
            _currentGrubDefault = value;
            OnPropertyChanged();
            RaisePendingChanged();
        }
    }

    private bool _currentOsProberEnabled;
    public bool CurrentOsProberEnabled
    {
        get => _currentOsProberEnabled;
        private set
        {
            if (_currentOsProberEnabled == value) return;
            _currentOsProberEnabled = value;
            OnPropertyChanged();
            RaisePendingChanged();
        }
    }

    private string _currentCmdlineLinuxDefault = string.Empty;
    /// <summary>Display-only — not editable in the UI.</summary>
    public string CurrentCmdlineLinuxDefault
    {
        get => _currentCmdlineLinuxDefault;
        private set
        {
            if (_currentCmdlineLinuxDefault == value) return;
            _currentCmdlineLinuxDefault = value;
            OnPropertyChanged();
        }
    }

    // ── Pending (user-edited) values bound to Slider / ComboBox / Toggle ──

    private int _pendingTimeout;
    public int PendingTimeout
    {
        get => _pendingTimeout;
        set
        {
            if (_pendingTimeout == value) return;
            _pendingTimeout = value;
            OnPropertyChanged();
            RaisePendingChanged();
        }
    }

    private string _pendingGrubDefault = "0";
    public string PendingGrubDefault
    {
        get => _pendingGrubDefault;
        set
        {
            if (_pendingGrubDefault == value) return;
            _pendingGrubDefault = value;
            OnPropertyChanged();
            RaisePendingChanged();
        }
    }

    private bool _pendingOsProberEnabled;
    public bool PendingOsProberEnabled
    {
        get => _pendingOsProberEnabled;
        set
        {
            if (_pendingOsProberEnabled == value) return;
            _pendingOsProberEnabled = value;
            OnPropertyChanged();
            RaisePendingChanged();
        }
    }

    // ── Derived ────────────────────────────────────────────────────

    public bool HasPendingChanges =>
        _pendingTimeout != _currentTimeout ||
        !string.Equals(_pendingGrubDefault, _currentGrubDefault, StringComparison.Ordinal) ||
        _pendingOsProberEnabled != _currentOsProberEnabled;

    public IEnumerable<string> PendingChangeDescriptions
    {
        get
        {
            var list = new List<string>();
            if (_pendingTimeout != _currentTimeout)
            {
                list.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    LocalizationService._("grubManager.change.timeout"),
                    _currentTimeout, _pendingTimeout));
            }
            if (!string.Equals(_pendingGrubDefault, _currentGrubDefault, StringComparison.Ordinal))
            {
                list.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    LocalizationService._("grubManager.change.default"),
                    _currentGrubDefault, _pendingGrubDefault));
            }
            if (_pendingOsProberEnabled != _currentOsProberEnabled)
            {
                var curLabel = LocalizationService._(_currentOsProberEnabled
                    ? "grubManager.osProber.enabled"
                    : "grubManager.osProber.disabled");
                var newLabel = LocalizationService._(_pendingOsProberEnabled
                    ? "grubManager.osProber.enabled"
                    : "grubManager.osProber.disabled");
                list.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    LocalizationService._("grubManager.change.osProber"),
                    curLabel, newLabel));
            }
            return list;
        }
    }

    public int PendingChangeCount => PendingChangeDescriptions.Count();

    public string PendingKickerText =>
        string.Format(CultureInfo.InvariantCulture, LocalizationService._("grubManager.pending.kicker"), PendingChangeCount);

    // ── ComboBox options for GRUB_DEFAULT ──────────────────────────

    private IReadOnlyList<GrubDefaultOption> _defaultEntryOptions = Array.Empty<GrubDefaultOption>();
    public IReadOnlyList<GrubDefaultOption> DefaultEntryOptions
    {
        get => _defaultEntryOptions;
        private set
        {
            _defaultEntryOptions = value;
            OnPropertyChanged();
        }
    }

    // ── Kernel list (read-only) ────────────────────────────────────

    private IReadOnlyList<string> _kernelList = Array.Empty<string>();
    public IReadOnlyList<string> KernelList
    {
        get => _kernelList;
        private set
        {
            _kernelList = value;
            OnPropertyChanged();
        }
    }

    // ── Backup state ───────────────────────────────────────────────

    private bool _hasBackup;
    public bool HasBackup
    {
        get => _hasBackup;
        private set
        {
            if (_hasBackup == value) return;
            _hasBackup = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BackupStatusText));
            (RollbackCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string BackupStatusText =>
        _hasBackup
            ? string.Format(CultureInfo.InvariantCulture, LocalizationService._("grubManager.backup.exists"), BackupPath)
            : LocalizationService._("grubManager.backup.missing");

    public string BackupPathDisplay => BackupPath;

    private bool _backupAcknowledged;
    public bool BackupAcknowledged
    {
        get => _backupAcknowledged;
        set
        {
            if (_backupAcknowledged == value) return;
            _backupAcknowledged = value;
            OnPropertyChanged();
            (ApplyChangesCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        }
    }

    // ── Status / busy / error ──────────────────────────────────────

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

    public ICommand ApplyChangesCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand RollbackCommand { get; }
    public ICommand CancelCommand { get; }

    // ── Operations ─────────────────────────────────────────────────

    /// <summary>
    /// Fires the initial scan. Called from the View's <c>Loaded</c> event.
    /// Distinguishes "wire-up auto-scan" from "user-invoked command" (there is
    /// no user-invoked scan command in this module).
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
            ProgressPercent = 0;
            StatusText = LocalizationService._("grubManager.status.scanning");

            var result = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            var settings = _engine.LastSettings;

            if (!result.Success || settings is null)
            {
                StatusText = string.IsNullOrEmpty(result.BlockedReason)
                    ? LocalizationService._("grubManager.status.unavailable")
                    : string.Format(LocalizationService._("grubManager.status.error"), result.BlockedReason);
                ErrorMessage = result.BlockedReason;
                HasBackup = false;
                return;
            }

            ApplySettings(settings);
            HasBackup = await _engine.CanRollbackAsync(string.Empty, _cts.Token);
            BackupAcknowledged = false;

            StatusText = LocalizationService._("grubManager.status.idle");
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("grubManager.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("grubManager.status.error"), ex.Message);
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

    /// <summary>Mirror the engine-reported <see cref="GrubSettings"/> into Current* and Pending*.</summary>
    private void ApplySettings(GrubSettings settings)
    {
        _suppressDirtyRaise = true;
        try
        {
            CurrentTimeout = settings.Timeout;
            CurrentGrubDefault = settings.GrubDefault ?? "0";
            CurrentOsProberEnabled = !settings.OsProberDisabled;
            CurrentCmdlineLinuxDefault = settings.CmdlineLinuxDefault ?? string.Empty;

            // Refresh ComboBox options to match kernel count (capped at 10).
            DefaultEntryOptions = BuildDefaultEntryOptions(settings.InstalledKernels?.Count ?? 0);
            KernelList = settings.InstalledKernels is null
                ? Array.Empty<string>()
                : settings.InstalledKernels.ToList();

            // Mirror current → pending so HasPendingChanges is false after a fresh scan.
            PendingTimeout = CurrentTimeout;
            PendingGrubDefault = CurrentGrubDefault;
            PendingOsProberEnabled = CurrentOsProberEnabled;
        }
        finally
        {
            _suppressDirtyRaise = false;
            RaisePendingChanged();
        }
    }

    public async Task ApplyAsync()
    {
        if (!HasPendingChanges || !BackupAcknowledged) return;
        if (IsBusy) return;

        var items = new List<string>();
        if (_pendingTimeout != _currentTimeout)
            items.Add($"set-timeout:{_pendingTimeout.ToString(CultureInfo.InvariantCulture)}");
        if (!string.Equals(_pendingGrubDefault, _currentGrubDefault, StringComparison.Ordinal))
            items.Add($"set-default:{_pendingGrubDefault}");
        if (_pendingOsProberEnabled != _currentOsProberEnabled)
            items.Add(_pendingOsProberEnabled ? "enable-os-prober" : "disable-os-prober");

        if (items.Count == 0) return;

        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ProgressPercent = 0;
            StatusText = LocalizationService._("grubManager.status.applying");

            var plan = new OptimizationPlan(_engine.Id, items);
            var progress = new Progress<TaskProgress>(p => ProgressPercent = p.Percentage);
            var result = await _engine.OptimizeAsync(plan, progress, _cts.Token);

            if (!result.Success)
            {
                StatusText = string.Format(LocalizationService._("grubManager.status.error"), "apply failed");
                ErrorMessage = "GRUB apply operation failed. Check privileges or config state.";
                return;
            }

            // Re-scan to pick up the now-written values and confirm backup exists.
            var rescan = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            if (rescan.Success && _engine.LastSettings is not null)
                ApplySettings(_engine.LastSettings);
            HasBackup = await _engine.CanRollbackAsync(string.Empty, _cts.Token);

            BackupAcknowledged = false;
            StatusText = LocalizationService._("grubManager.status.done");
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("grubManager.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("grubManager.status.error"), ex.Message);
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

    public async Task RollbackAsync()
    {
        if (!HasBackup || IsBusy) return;

        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ProgressPercent = 0;
            StatusText = LocalizationService._("grubManager.status.rollback");

            await _engine.RollbackAsync(string.Empty, _cts.Token);

            var rescan = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            if (rescan.Success && _engine.LastSettings is not null)
                ApplySettings(_engine.LastSettings);
            HasBackup = await _engine.CanRollbackAsync(string.Empty, _cts.Token);

            BackupAcknowledged = false;
            StatusText = LocalizationService._("grubManager.status.done");
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService._("grubManager.status.idle");
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService._("grubManager.status.error"), ex.Message);
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

    /// <summary>Reverts pending values to last-scanned current values and clears the ack.</summary>
    public void Reset()
    {
        _suppressDirtyRaise = true;
        try
        {
            PendingTimeout = CurrentTimeout;
            PendingGrubDefault = CurrentGrubDefault;
            PendingOsProberEnabled = CurrentOsProberEnabled;
        }
        finally
        {
            _suppressDirtyRaise = false;
            RaisePendingChanged();
        }
        BackupAcknowledged = false;
    }

    public void Cancel()
    {
        try { _cts?.Cancel(); } catch { /* already disposed */ }
    }

    // ── Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Build the combo-box options: one "Entry N" per installed kernel (capped at 10)
    /// plus the literal "saved" option. Always at least 2 entries so the combo isn't empty.
    /// </summary>
    private static IReadOnlyList<GrubDefaultOption> BuildDefaultEntryOptions(int installedKernelCount)
    {
        int count = Math.Clamp(installedKernelCount, 1, 10);
        var list = new List<GrubDefaultOption>(count + 1);
        for (int i = 0; i < count; i++)
        {
            var label = string.Format(
                CultureInfo.InvariantCulture,
                LocalizationService._("grubManager.default.entry"),
                i);
            list.Add(new GrubDefaultOption(
                Value: i.ToString(CultureInfo.InvariantCulture),
                Display: $"{i} \u2014 {label}"));
        }
        list.Add(new GrubDefaultOption(
            Value: "saved",
            Display: LocalizationService._("grubManager.default.saved")));
        return list;
    }

    private void RaisePendingChanged()
    {
        if (_suppressDirtyRaise) return;
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(PendingChangeDescriptions));
        OnPropertyChanged(nameof(PendingChangeCount));
        OnPropertyChanged(nameof(PendingKickerText));
        (ApplyChangesCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (ResetCommand as DelegateCommand)?.RaiseCanExecuteChanged();
    }

    private void RaiseAllCanExecuteChanged()
    {
        (ApplyChangesCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (ResetCommand as DelegateCommand)?.RaiseCanExecuteChanged();
        (RollbackCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as DelegateCommand)?.RaiseCanExecuteChanged();
    }

    // ── INPC ───────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Row for the <c>GRUB_DEFAULT</c> combobox: engine-value + UI-display strings.</summary>
public sealed record GrubDefaultOption(string Value, string Display);
