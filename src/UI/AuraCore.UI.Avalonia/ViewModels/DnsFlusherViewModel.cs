using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AuraCore.Application;
using AuraCore.Module.DnsFlusher;
using AuraCore.Module.DnsFlusher.Models;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>
/// Slim adapter so the VM can be unit-tested without touching the sealed
/// <see cref="DnsFlusherModule"/> (which shells out to sudo + dscacheutil
/// + killall on macOS). Mirrors the IGrubManagerEngine / IKernelCleanerEngine /
/// IDockerCleanerEngine / IJournalCleanerEngine patterns from 4.3.1-4.3.6.
/// </summary>
public interface IDnsFlusherEngine
{
    string Id { get; }
    DnsFlusherReport? LastReport { get; }
    Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default);
    Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct);
}

/// <summary>Default production adapter over the sealed concrete module.</summary>
public sealed class DnsFlusherEngineAdapter : IDnsFlusherEngine
{
    private readonly DnsFlusherModule _module;
    public DnsFlusherEngineAdapter(DnsFlusherModule module) =>
        _module = module ?? throw new ArgumentNullException(nameof(module));

    public string Id => _module.Id;
    public DnsFlusherReport? LastReport => _module.LastReport;

    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default) =>
        _module.ScanAsync(options, ct);

    public Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress, CancellationToken ct) =>
        _module.OptimizeAsync(plan, progress, ct);
}

/// <summary>
/// Different feedback modes for the StatusText / state line. Idle shows the
/// muted last-flush display; Success briefly shows the "Cache flushed just now"
/// message after a successful flush before transitioning back to Idle;
/// Error surfaces the blocked-reason / exception message.
/// </summary>
public enum DnsFlusherFeedbackState
{
    Idle,
    Scanning,
    Flushing,
    Success,
    Error,
}

/// <summary>
/// Phase 4.4.1 DNS Flusher view-model.
/// "Trivial case deserves polished UI": one hero button that calls
/// <c>sudo -n dscacheutil -flushcache</c> + <c>killall -HUP mDNSResponder</c>.
/// Auto-scans on Loaded (no manual Scan button). Re-scans after successful
/// flush to refresh <see cref="LastFlush"/> display. Shows a 3-second
/// transient success message, then reverts to the muted last-flush state line.
/// </summary>
public sealed class DnsFlusherViewModel : INotifyPropertyChanged
{
    private readonly IDnsFlusherEngine _engine;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _transientFeedbackCts;

    /// <summary>How long the "✓ Cache flushed just now" state persists after a successful flush.</summary>
    internal const int SuccessFeedbackMs = 3000;

    public DnsFlusherViewModel(IDnsFlusherEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        FlushCommand = new AsyncDelegateCommand(
            execute: _ => FlushAsync(),
            canExecute: _ => !IsBusy && DscacheutilAvailable);

        StatusText = LocalizationService._("dnsFlusher.status.idle");
        FeedbackState = DnsFlusherFeedbackState.Idle;
    }

    /// <summary>Convenience ctor for DI — wraps the concrete module in the default adapter.</summary>
    public DnsFlusherViewModel(DnsFlusherModule module)
        : this(new DnsFlusherEngineAdapter(module ?? throw new ArgumentNullException(nameof(module))))
    {
    }

    // ── Public surface ─────────────────────────────────────────────

    private DnsFlusherReport? _report;
    public DnsFlusherReport? Report
    {
        get => _report;
        private set
        {
            if (ReferenceEquals(_report, value)) return;
            _report = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DscacheutilAvailable));
            OnPropertyChanged(nameof(LastFlush));
            OnPropertyChanged(nameof(LastFlushDisplay));
            (FlushCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool DscacheutilAvailable => _report?.DscacheutilAvailable ?? false;

    public DateTime? LastFlush => _report?.LastFlush;

    /// <summary>
    /// Human-readable summary of the last-flush timestamp. Uses
    /// <see cref="RelativeTimeFormatter"/> to localize the fragment
    /// (e.g. "Just now", "2 minutes ago"), then wraps it in the
    /// "Last flushed: {0}" prefix. Falls back to the "never" string
    /// when no flush has been recorded.
    /// </summary>
    public string LastFlushDisplay
    {
        get
        {
            if (_report?.LastFlush is not { } dt)
                return LocalizationService._("dnsFlusher.lastFlush.never");
            var fragment = RelativeTimeFormatter.Format(dt, DateTime.UtcNow);
            return string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService._("dnsFlusher.lastFlush.prefix"),
                fragment);
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

    private DnsFlusherFeedbackState _feedbackState;
    public DnsFlusherFeedbackState FeedbackState
    {
        get => _feedbackState;
        private set
        {
            if (_feedbackState == value) return;
            _feedbackState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSuccess));
        }
    }

    /// <summary>View binding: true while the "✓ Cache flushed just now" transient is active.</summary>
    public bool IsSuccess => _feedbackState == DnsFlusherFeedbackState.Success;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            (FlushCommand as AsyncDelegateCommand)?.RaiseCanExecuteChanged();
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

    public ICommand FlushCommand { get; }

    // ── Operations ─────────────────────────────────────────────────

    /// <summary>
    /// Fires the initial scan. Called from the View's <c>Loaded</c> event.
    /// Distinguishes "wire-up auto-scan" from "user-invoked command"
    /// (there is no user-invoked scan command in this module — the UX is
    /// "one hero button, instant feedback").
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
            FeedbackState = DnsFlusherFeedbackState.Scanning;
            StatusText = LocalizationService._("dnsFlusher.status.scanning");

            var result = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            Report = _engine.LastReport;

            if (!result.Success)
            {
                if (Report is { DscacheutilAvailable: false })
                    StatusText = LocalizationService._("dnsFlusher.status.unavailable");
                else
                    StatusText = string.Format(
                        LocalizationService._("dnsFlusher.status.error"),
                        result.BlockedReason ?? "scan failed");
                ErrorMessage = result.BlockedReason;
                FeedbackState = DnsFlusherFeedbackState.Error;
                return;
            }

            FeedbackState = DnsFlusherFeedbackState.Idle;
            StatusText = LocalizationService._("dnsFlusher.status.idle");
        }
        catch (OperationCanceledException)
        {
            FeedbackState = DnsFlusherFeedbackState.Idle;
            StatusText = LocalizationService._("dnsFlusher.status.idle");
        }
        catch (Exception ex)
        {
            FeedbackState = DnsFlusherFeedbackState.Error;
            StatusText = string.Format(LocalizationService._("dnsFlusher.status.error"), ex.Message);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public async Task FlushAsync()
    {
        if (IsBusy) return;

        // Cancel any in-flight transient success-feedback timer — the user
        // just hit flush again, so the old "just flushed" state is stale.
        try { _transientFeedbackCts?.Cancel(); } catch { /* disposed */ }

        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            FeedbackState = DnsFlusherFeedbackState.Flushing;
            StatusText = LocalizationService._("dnsFlusher.hero.flushing");

            var plan = new OptimizationPlan(_engine.Id, new List<string> { "flush" });
            var progress = new Progress<TaskProgress>(_ => { /* no progress UI in this module */ });
            var result = await _engine.OptimizeAsync(plan, progress, _cts.Token);

            if (!result.Success)
            {
                FeedbackState = DnsFlusherFeedbackState.Error;
                StatusText = string.Format(
                    LocalizationService._("dnsFlusher.status.error"),
                    "flush failed");
                ErrorMessage = "Flush operation failed. Check privileges (sudo).";
                return;
            }

            // Re-scan to refresh LastFlush timestamp from the engine-persisted state file.
            var rescan = await _engine.ScanAsync(new ScanOptions(), _cts.Token);
            if (rescan.Success) Report = _engine.LastReport;

            // Enter transient Success state, then schedule a 3-second revert.
            FeedbackState = DnsFlusherFeedbackState.Success;
            StatusText = LocalizationService._("dnsFlusher.success");

            StartTransientSuccessRevert();
        }
        catch (OperationCanceledException)
        {
            FeedbackState = DnsFlusherFeedbackState.Idle;
            StatusText = LocalizationService._("dnsFlusher.status.idle");
        }
        catch (Exception ex)
        {
            FeedbackState = DnsFlusherFeedbackState.Error;
            StatusText = string.Format(LocalizationService._("dnsFlusher.status.error"), ex.Message);
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
    /// Schedules a 3-second task that flips FeedbackState from Success back
    /// to Idle (and refreshes StatusText). Cancellation-aware: if another
    /// flush starts while the timer is running, the new flush cancels us.
    /// </summary>
    private void StartTransientSuccessRevert()
    {
        _transientFeedbackCts?.Dispose();
        _transientFeedbackCts = new CancellationTokenSource();
        var token = _transientFeedbackCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SuccessFeedbackMs, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                // Only flip if we're still in Success state (not in error / re-flushing).
                if (FeedbackState == DnsFlusherFeedbackState.Success)
                {
                    FeedbackState = DnsFlusherFeedbackState.Idle;
                    StatusText = LocalizationService._("dnsFlusher.status.idle");
                }
            }
            catch (OperationCanceledException) { /* expected on re-flush */ }
            catch { /* last-resort safety net for the fire-and-forget task */ }
        });
    }

    // ── INPC ───────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Inline relative-time helper used by <see cref="DnsFlusherViewModel.LastFlushDisplay"/>.
/// Uses localization keys so "Just now" / "Az önce", "N minutes ago" / "N dakika önce"
/// etc. come from <see cref="LocalizationService"/>. Falls back to absolute
/// <c>yyyy-MM-dd HH:mm</c> for dates more than a day old.
/// Exposed as <c>public</c> (not <c>internal</c>) so unit tests can exercise
/// the exact-threshold boundaries without needing <c>InternalsVisibleTo</c>.
/// </summary>
public static class RelativeTimeFormatter
{
    /// <summary>Boundaries:
    /// [0, 10s) → justNow; [10s, 60s) → secondsAgo; [60s, 1h) → minutesAgo;
    /// [1h, 24h) → hoursAgo; ≥24h → absolute date.</summary>
    public static string Format(DateTime when, DateTime nowUtc)
    {
        var delta = nowUtc - when.ToUniversalTime();
        if (delta.TotalSeconds < 0) delta = TimeSpan.Zero; // clock-skew safety

        if (delta.TotalSeconds < 10)
        {
            return LocalizationService._("dnsFlusher.relative.justNow");
        }
        if (delta.TotalSeconds < 60)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService._("dnsFlusher.relative.secondsAgo"),
                (int)delta.TotalSeconds);
        }
        if (delta.TotalHours < 1)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService._("dnsFlusher.relative.minutesAgo"),
                (int)delta.TotalMinutes);
        }
        if (delta.TotalHours < 24)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService._("dnsFlusher.relative.hoursAgo"),
                (int)delta.TotalHours);
        }
        return when.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
