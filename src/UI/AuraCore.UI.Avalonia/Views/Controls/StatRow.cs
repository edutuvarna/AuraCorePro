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
/// </summary>
public class StatRow : UniformGrid
{
    public StatRow()
    {
        Rows = 1;
    }
}
