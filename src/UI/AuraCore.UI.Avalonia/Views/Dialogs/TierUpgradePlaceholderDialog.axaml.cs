using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;
using AuraCore.UI.Avalonia.Services.AI;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class TierUpgradePlaceholderDialog : Window
{
    public TierUpgradePlaceholderDialog()
    {
        InitializeComponent();
    }

    public TierUpgradePlaceholderDialog(string moduleKey, UserTier requiredTier) : this()
    {
        var body = this.FindControl<TextBlock>("BodyText");
        if (body is not null)
        {
            body.Text = $"This feature requires {requiredTier} tier. Contact admin to upgrade.";
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
