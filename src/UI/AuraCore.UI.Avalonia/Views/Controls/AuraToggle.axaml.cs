using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class AuraToggle : UserControl
{
    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<AuraToggle, bool>(nameof(IsOn), false, defaultBindingMode: BindingMode.TwoWay);

    public bool IsOn { get => GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }

    public AuraToggle() { InitializeComponent(); }
}
