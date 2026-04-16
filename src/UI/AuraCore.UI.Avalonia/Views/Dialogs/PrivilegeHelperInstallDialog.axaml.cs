using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

/// <summary>
/// Dialog shown when the privilege helper binary is missing.
/// Presents localized title + body, then fires <see cref="InstallRequested"/> or
/// <see cref="Cancelled"/> when the user clicks a button.
/// Task 11 (HelperMissingBanner wiring) depends on these event names and the
/// button x:Name values ("InstallButton", "CancelButton") being stable.
/// </summary>
public partial class PrivilegeHelperInstallDialog : UserControl
{
    /// <summary>Fired when the user clicks the Install button.</summary>
    public event EventHandler<EventArgs>? InstallRequested;

    /// <summary>Fired when the user clicks the Cancel button.</summary>
    public event EventHandler<EventArgs>? Cancelled;

    public PrivilegeHelperInstallDialog()
    {
        InitializeComponent();
        ApplyLocalization();

        // Re-apply strings if the user switches language while the dialog is open.
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);

        this.FindControl<Button>("InstallButton")!.Click += (_, _) =>
            InstallRequested?.Invoke(this, EventArgs.Empty);

        this.FindControl<Button>("CancelButton")!.Click += (_, _) =>
            Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyLocalization()
    {
        var title   = this.FindControl<TextBlock>("TitleText");
        var body    = this.FindControl<TextBlock>("BodyText");
        var install = this.FindControl<Button>("InstallButton");
        var cancel  = this.FindControl<Button>("CancelButton");

        if (title is not null)
            title.Text = LocalizationService.Get("privilege.install.dialog_title");

        if (body is not null)
            body.Text = LocalizationService.Get("privilege.install.dialog_body")
                .Replace("{helperName}", "auracore-privhelper");

        if (install is not null)
            install.Content = LocalizationService.Get("privilege.install.btn_install");

        if (cancel is not null)
            cancel.Content = LocalizationService.Get("privilege.install.btn_cancel");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
