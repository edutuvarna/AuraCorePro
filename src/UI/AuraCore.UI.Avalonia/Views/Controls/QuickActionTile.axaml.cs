using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class QuickActionTile : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<QuickActionTile, string>(nameof(Title), string.Empty);
    public static readonly StyledProperty<string> SubLabelProperty =
        AvaloniaProperty.Register<QuickActionTile, string>(nameof(SubLabel), string.Empty);
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<QuickActionTile, Geometry?>(nameof(Icon));
    public static readonly StyledProperty<IBrush> TintBrushProperty =
        AvaloniaProperty.Register<QuickActionTile, IBrush>(nameof(TintBrush), Brushes.Teal);
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<QuickActionTile, ICommand?>(nameof(Command));

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string SubLabel { get => GetValue(SubLabelProperty); set => SetValue(SubLabelProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public IBrush TintBrush { get => GetValue(TintBrushProperty); set => SetValue(TintBrushProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public QuickActionTile() { InitializeComponent(); }
}
