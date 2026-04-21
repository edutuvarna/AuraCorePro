using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

/// <summary>
/// Hard-modal dialog shown when a mandatory update is available.
/// Cannot be dismissed by the user — must install or wait.
/// Shown via ShowDialog(owner) in MainWindow.ShowMandatoryDialog().
/// </summary>
public partial class MandatoryUpdateDialog : Window
{
    public MandatoryUpdateDialog()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        var title   = this.FindControl<TextBlock>("TitleText");
        var update  = this.FindControl<Button>("UpdateBtn");

        if (title is not null)
            title.Text = LocalizationService.Get("UpdateBanner_Mandatory_Title");
        if (update is not null)
            update.Content = LocalizationService.Get("UpdateBanner_UpdateNow");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
