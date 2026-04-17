using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AuraCore.UI.Avalonia.ViewModels.Dashboard;

/// <summary>
/// View model for a single Quick Action tile on the Dashboard.
/// Immutable after construction — all fields set in ctor.
/// Uses the project's <see cref="AsyncDelegateCommand"/> pattern from DelegateCommands.cs.
/// </summary>
public sealed class QuickActionTileVM
{
    public string Id { get; }
    public string Label { get; }
    public string SubLabel { get; }
    public string IconGlyph { get; }
    public string AccentToken { get; }
    public ICommand Command { get; }

    public QuickActionTileVM(
        string id,
        string label,
        string subLabel,
        string iconGlyph,
        string accentToken,
        Func<Task> execute)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Label = label ?? throw new ArgumentNullException(nameof(label));
        SubLabel = subLabel ?? string.Empty;
        IconGlyph = iconGlyph ?? string.Empty;
        AccentToken = accentToken ?? string.Empty;
        // AsyncDelegateCommand is defined in ViewModels/Shared/DelegateCommands.cs
        Command = new AsyncDelegateCommand(_ => execute());
    }
}
