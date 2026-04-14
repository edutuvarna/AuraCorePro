using Avalonia;
using Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class UserChip : UserControl
{
    public static readonly StyledProperty<string> EmailProperty =
        AvaloniaProperty.Register<UserChip, string>(nameof(Email), string.Empty);
    public static readonly StyledProperty<string> RoleProperty =
        AvaloniaProperty.Register<UserChip, string>(nameof(Role), string.Empty);
    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<UserChip, string>(nameof(StatusText), "Signed in");
    public static readonly StyledProperty<string> AvatarInitialProperty =
        AvaloniaProperty.Register<UserChip, string>(nameof(AvatarInitial), "?");

    public string Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }
    public string Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }
    public string StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
    public string AvatarInitial { get => GetValue(AvatarInitialProperty); set => SetValue(AvatarInitialProperty, value); }

    public UserChip() { InitializeComponent(); }
}
