using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class SidebarNavItem : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SidebarNavItem, string>(nameof(Label), string.Empty);
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SidebarNavItem, Geometry?>(nameof(Icon));
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<SidebarNavItem, bool>(nameof(IsActive), false);
    public static readonly StyledProperty<string> TrailingChipTextProperty =
        AvaloniaProperty.Register<SidebarNavItem, string>(nameof(TrailingChipText), string.Empty);
    public static readonly StyledProperty<IBrush> AccentBrushProperty =
        AvaloniaProperty.Register<SidebarNavItem, IBrush>(nameof(AccentBrush), Brushes.Teal);
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<SidebarNavItem, ICommand?>(nameof(Command));
    public static readonly global::Avalonia.StyledProperty<bool> IsLockedProperty =
        global::Avalonia.AvaloniaProperty.Register<SidebarNavItem, bool>(nameof(IsLocked));

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public string TrailingChipText { get => GetValue(TrailingChipTextProperty); set => SetValue(TrailingChipTextProperty, value); }
    public IBrush AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public bool IsLocked { get => GetValue(IsLockedProperty); set => SetValue(IsLockedProperty, value); }

    static SidebarNavItem()
    {
        IsLockedProperty.Changed.AddClassHandler<SidebarNavItem>((ctrl, e) =>
        {
            ctrl.PseudoClasses.Set(":locked", e.NewValue is true);
        });
    }

    public SidebarNavItem() { InitializeComponent(); }
}
