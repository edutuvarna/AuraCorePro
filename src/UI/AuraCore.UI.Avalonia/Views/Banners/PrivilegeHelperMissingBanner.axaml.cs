using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;

namespace AuraCore.UI.Avalonia.Views.Banners;

/// <summary>
/// Top-of-shell banner shown when the privilege helper binary is not installed.
/// Exposes <see cref="InstallNowClicked"/> and <see cref="DismissClicked"/> events.
/// Visibility is controlled by the parent via IHelperAvailabilityService.IsBannerVisible —
/// this control does not manage its own visibility.
/// Task 11 (shell wiring) depends on button x:Name values and event names being stable.
/// </summary>
public partial class PrivilegeHelperMissingBanner : UserControl
{
    /// <summary>Fired when the user clicks the Install Now button.</summary>
    public event EventHandler<EventArgs>? InstallNowClicked;

    /// <summary>Fired when the user clicks the Dismiss button.</summary>
    public event EventHandler<EventArgs>? DismissClicked;

    public PrivilegeHelperMissingBanner()
    {
        InitializeComponent();
        ApplyLocalization();

        // Re-apply strings if the user switches language while the banner is visible.
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);

        this.FindControl<Button>("InstallNowButton")!.Click += (_, _) =>
            InstallNowClicked?.Invoke(this, EventArgs.Empty);

        this.FindControl<Button>("DismissButton")!.Click += (_, _) =>
            DismissClicked?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyLocalization()
    {
        var body    = this.FindControl<TextBlock>("BodyText");
        var install = this.FindControl<Button>("InstallNowButton");
        var dismiss = this.FindControl<Button>("DismissButton");

        if (body is not null)
            body.Text = LocalizationService.Get("privilege.missing.banner_text");

        if (install is not null)
            install.Content = LocalizationService.Get("privilege.missing.btn_install_now");

        if (dismiss is not null)
            dismiss.Content = LocalizationService.Get("privilege.missing.btn_dismiss");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
