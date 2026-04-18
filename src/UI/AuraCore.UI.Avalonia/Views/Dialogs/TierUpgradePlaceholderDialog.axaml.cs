using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;
using AuraCore.UI.Avalonia.Services.AI;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class TierUpgradePlaceholderDialog : Window
{
    private string _requiredTier = string.Empty;

    public TierUpgradePlaceholderDialog()
    {
        InitializeComponent();
        ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    public TierUpgradePlaceholderDialog(string moduleKey, UserTier requiredTier) : this()
    {
        _requiredTier = requiredTier.ToString();
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.Get("tier.upgrade.dialog.title");

        var titleText = this.FindControl<TextBlock>("TitleText");
        if (titleText is not null)
            titleText.Text = LocalizationService.Get("tier.upgrade.dialog.title");

        var body = this.FindControl<TextBlock>("BodyText");
        if (body is not null)
            body.Text = string.Format(LocalizationService.Get("tier.upgrade.dialog.body"), _requiredTier);

        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn is not null)
            closeBtn.Content = LocalizationService.Get("common.close");
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
