using Avalonia;
using Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class SidebarSectionDivider : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SidebarSectionDivider, string>(nameof(Label), string.Empty);

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public SidebarSectionDivider() { InitializeComponent(); }
}
