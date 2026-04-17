using System.ComponentModel;
using AuraCore.Application.Interfaces.Platform;
using global::Avalonia;
using global::Avalonia.Controls.Primitives;

namespace AuraCore.UI.Avalonia.Views.Controls;

/// <summary>
/// Phase 4.0 Module Page stat row — layout-only primitive.
/// Subclasses <see cref="UniformGrid"/> with <c>Rows=1</c> pre-set so
/// child <see cref="StatCard"/>s lay out equal-width side by side.
/// No XAML file needed — this is a trivial subclass (see plan §Plan-level
/// decisions #3). Children flow into <see cref="UniformGrid.Children"/>
/// via the standard Panel semantics.
/// Spec §4.3.
///
/// Phase 5.3 Task 9: columns reflow 4→2→1 based on <see cref="INarrowModeService"/>.
/// Subscribes to <see cref="App.NarrowMode"/> INPC (null-safe; falls back to 4 cols
/// when the service is unavailable, e.g. in the test harness without full DI).
/// </summary>
public class StatRow : UniformGrid
{
    private const int ColsWide       = 4;
    private const int ColsNarrow     = 2;
    private const int ColsVeryNarrow = 1;

    private readonly INarrowModeService? _narrowMode;

    public StatRow()
    {
        Rows = 1;

        // Resolve from the App-level singleton; null when running in tests
        // that don't spin up the full DI container.
        _narrowMode = App.NarrowMode;

        if (_narrowMode is not null)
        {
            _narrowMode.PropertyChanged += OnNarrowModeChanged;
            // Apply initial state immediately — before the first layout pass.
            ApplyColumns();
        }
        else
        {
            // Fallback: 4-column wide layout (pre-refactor behaviour).
            Columns = ColsWide;
        }
    }

    /// <summary>
    /// Unsubscribe from the service when the control is detached from the
    /// visual tree to avoid keeping the control alive via the event handler.
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_narrowMode is not null)
            _narrowMode.PropertyChanged -= OnNarrowModeChanged;

        base.OnDetachedFromVisualTree(e);
    }

    private void OnNarrowModeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(INarrowModeService.IsNarrow)
                           or nameof(INarrowModeService.IsVeryNarrow))
        {
            ApplyColumns();
        }
    }

    private void ApplyColumns()
    {
        Columns = _narrowMode switch
        {
            { IsVeryNarrow: true } => ColsVeryNarrow,
            { IsNarrow:     true } => ColsNarrow,
            _                      => ColsWide,
        };
    }
}
