using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class HeroCTA : UserControl
{
    public static readonly StyledProperty<string> KickerProperty =
        AvaloniaProperty.Register<HeroCTA, string>(nameof(Kicker), string.Empty);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<HeroCTA, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> BodyProperty =
        AvaloniaProperty.Register<HeroCTA, string>(nameof(Body), string.Empty);

    public static readonly StyledProperty<string> PrimaryButtonTextProperty =
        AvaloniaProperty.Register<HeroCTA, string>(nameof(PrimaryButtonText), "Go");

    public static readonly StyledProperty<string> SecondaryButtonTextProperty =
        AvaloniaProperty.Register<HeroCTA, string>(nameof(SecondaryButtonText), string.Empty);

    public static readonly StyledProperty<ICommand?> PrimaryCommandProperty =
        AvaloniaProperty.Register<HeroCTA, ICommand?>(nameof(PrimaryCommand));

    public static readonly StyledProperty<ICommand?> SecondaryCommandProperty =
        AvaloniaProperty.Register<HeroCTA, ICommand?>(nameof(SecondaryCommand));

    public string Kicker { get => GetValue(KickerProperty); set => SetValue(KickerProperty, value); }
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Body { get => GetValue(BodyProperty); set => SetValue(BodyProperty, value); }
    public string PrimaryButtonText { get => GetValue(PrimaryButtonTextProperty); set => SetValue(PrimaryButtonTextProperty, value); }
    public string SecondaryButtonText { get => GetValue(SecondaryButtonTextProperty); set => SetValue(SecondaryButtonTextProperty, value); }
    public ICommand? PrimaryCommand { get => GetValue(PrimaryCommandProperty); set => SetValue(PrimaryCommandProperty, value); }
    public ICommand? SecondaryCommand { get => GetValue(SecondaryCommandProperty); set => SetValue(SecondaryCommandProperty, value); }

    public HeroCTA()
    {
        InitializeComponent();
    }
}
