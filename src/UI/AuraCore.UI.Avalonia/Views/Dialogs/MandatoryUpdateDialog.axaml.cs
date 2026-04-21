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
    private bool _installing;

    public MandatoryUpdateDialog()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    /// <summary>
    /// Call this when the user has accepted the mandatory update and download has started.
    /// After this, the dialog is allowed to close naturally (e.g. after InstallAndExit).
    /// </summary>
    public void MarkInstalling() => _installing = true;

    protected override void OnClosing(global::Avalonia.Controls.WindowClosingEventArgs e)
    {
        if (!_installing)
            e.Cancel = true;
        base.OnClosing(e);
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
