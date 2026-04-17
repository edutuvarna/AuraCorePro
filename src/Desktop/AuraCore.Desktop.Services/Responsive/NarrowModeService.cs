using System.ComponentModel;
using System.Runtime.CompilerServices;
using AuraCore.Application.Interfaces.Platform;

namespace AuraCore.Desktop.Services.Responsive;

/// <summary>
/// Default INarrowModeService implementation. Width is pushed in by the
/// host (MainWindow subscribes its Bounds.PropertyChanged and calls
/// <see cref="UpdateWidth(double)"/>).
/// </summary>
public sealed class NarrowModeService : INarrowModeService
{
    private const double NarrowThreshold = 1000;
    private const double VeryNarrowThreshold = 900;

    private double _currentWidth;
    private bool _cachedIsNarrow;
    private bool _cachedIsVeryNarrow;
#if DEBUG
    private bool? _forceOverride;
#endif

    public double CurrentWidth
    {
        get => _currentWidth;
        private set
        {
            if (Set(ref _currentWidth, value, nameof(CurrentWidth)))
                RefreshDerivedFlags();
        }
    }

    public bool IsNarrow
    {
        get
        {
#if DEBUG
            if (_forceOverride.HasValue) return _forceOverride.Value;
#endif
            return _currentWidth > 0 && _currentWidth < NarrowThreshold;
        }
    }

    public bool IsVeryNarrow
    {
        get
        {
#if DEBUG
            if (_forceOverride.HasValue) return _forceOverride.Value;
#endif
            return _currentWidth > 0 && _currentWidth < VeryNarrowThreshold;
        }
    }

#if DEBUG
    public bool? ForceNarrowOverride
    {
        get => _forceOverride;
        set
        {
            if (_forceOverride == value) return;
            _forceOverride = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNarrow)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVeryNarrow)));
        }
    }
#endif

    /// <summary>
    /// Called by the host (MainWindow code-behind) when window width changes.
    /// </summary>
    public void UpdateWidth(double width) => CurrentWidth = width;

    private void RefreshDerivedFlags()
    {
        // Track the actual previous values so we only raise INPC on real changes.
        // IsNarrow / IsVeryNarrow are computed properties; we recalculate and compare.
        var newIsNarrow = IsNarrow;
        var newIsVeryNarrow = IsVeryNarrow;

        if (_cachedIsNarrow != newIsNarrow)
        {
            _cachedIsNarrow = newIsNarrow;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNarrow)));
        }
        if (_cachedIsVeryNarrow != newIsVeryNarrow)
        {
            _cachedIsVeryNarrow = newIsVeryNarrow;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVeryNarrow)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
