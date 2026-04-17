using Avalonia;
using Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class AppLogoBadge : UserControl
{
    public static readonly StyledProperty<string> ProductNameProperty =
        AvaloniaProperty.Register<AppLogoBadge, string>(nameof(ProductName), "AuraCore");
    public static readonly StyledProperty<string> TaglineProperty =
        AvaloniaProperty.Register<AppLogoBadge, string>(nameof(Tagline), "PRO • CORTEX");

    public string ProductName { get => GetValue(ProductNameProperty); set => SetValue(ProductNameProperty, value); }
    public string Tagline { get => GetValue(TaglineProperty); set => SetValue(TaglineProperty, value); }

    public AppLogoBadge() { InitializeComponent(); }
}
