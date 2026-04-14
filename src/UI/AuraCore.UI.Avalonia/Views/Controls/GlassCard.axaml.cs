using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class GlassCard : UserControl
{
    public static readonly StyledProperty<object?> CardContentProperty =
        AvaloniaProperty.Register<GlassCard, object?>(nameof(CardContent));

    public static readonly StyledProperty<CornerRadius> CardCornerRadiusProperty =
        AvaloniaProperty.Register<GlassCard, CornerRadius>(nameof(CardCornerRadius), new CornerRadius(12));

    public static readonly StyledProperty<BoxShadows> CardGlowProperty =
        AvaloniaProperty.Register<GlassCard, BoxShadows>(nameof(CardGlow), default);

    public object? CardContent { get => GetValue(CardContentProperty); set => SetValue(CardContentProperty, value); }
    public CornerRadius CardCornerRadius { get => GetValue(CardCornerRadiusProperty); set => SetValue(CardCornerRadiusProperty, value); }
    public BoxShadows CardGlow { get => GetValue(CardGlowProperty); set => SetValue(CardGlowProperty, value); }

    public GlassCard()
    {
        InitializeComponent();
    }
}
