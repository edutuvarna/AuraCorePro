using AuraCore.Application;
using AuraCore.Desktop;
using AuraCore.Desktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AuraCore.Desktop.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        AccountEmail.Text = LoginWindow.UserEmail ?? "Offline";
        AccountTier.Text = (LoginWindow.UserTier ?? "free").ToUpper();
        AccountRole.Text = LoginWindow.UserRole ?? "user";

        // Set current theme radio
        ThemeSelector.SelectedIndex = ThemeService.CurrentTheme switch
        {
            ThemeService.AppTheme.System => 0,
            ThemeService.AppTheme.Light => 1,
            ThemeService.AppTheme.Dark => 2,
            _ => 0
        };

        // Set current language radio
        LanguageSelector.SelectedIndex = S.Current switch
        {
            S.Lang.EN => 0,
            S.Lang.TR => 1,
            _ => 0
        };

        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        PageTitle.Text = S._("settings.title");
        AccountTitle.Text = S._("settings.account");
        LblEmail.Text = S._("settings.email");
        LblSubscription.Text = S._("settings.subscription");
        LblRole.Text = S._("settings.role");
        LogOutBtn.Content = S._("settings.logOut");
        AppearanceTitle.Text = S._("settings.appearance");
        ThemeDesc.Text = S._("settings.chooseTheme");
        ThemeSystem.Content = S._("settings.systemDefault");
        ThemeLight.Content = S._("settings.light");
        ThemeDark.Content = S._("settings.dark");
        LanguageTitle.Text = S._("settings.language");
        LanguageDesc.Text = S._("settings.chooseLang");
        AboutTitle.Text = S._("settings.about");
        VersionText.Text = S._("settings.version");
        TaglineText.Text = S._("settings.tagline");
        DescriptionText.Text = S._("settings.description");
    }

    private void ThemeSelector_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeSelector.SelectedItem is not RadioButton selected) return;
        var theme = selected.Tag?.ToString() switch
        {
            "Light" => ThemeService.AppTheme.Light,
            "Dark" => ThemeService.AppTheme.Dark,
            _ => ThemeService.AppTheme.System
        };
        if (App.MainWindow?.Content is FrameworkElement root)
            ThemeService.SetTheme(root, theme);
    }

    private void LanguageSelector_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageSelector.SelectedItem is not RadioButton selected) return;
        var lang = selected.Tag?.ToString() switch
        {
            "TR" => S.Lang.TR,
            _ => S.Lang.EN
        };

        if (lang == S.Current) return;
        S.SetLanguage(lang);
        ApplyLocalization();
    }

    private async void LogOut_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = S._("settings.logOutTitle"),
            Content = S._("settings.logOutMsg"),
            PrimaryButtonText = S._("settings.logOut"),
            CloseButtonText = S._("common.cancel"),
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        // Clear all auth state
        LoginWindow.AccessToken = null;
        LoginWindow.RefreshToken = null;
        LoginWindow.UserEmail = null;
        LoginWindow.UserRole = null;
        LoginWindow.UserTier = "free";
        LoginWindow.UserId = null;
        SessionState.AccessToken = null;
        SessionState.UserTier = "free";
        SessionState.UserRole = null;
        SessionState.UserEmail = null;
        SessionState.UserId = null;

        // Clear saved session so auto-login won't trigger
        LoginWindow.ClearSavedSession();

        // Open fresh LoginWindow and close current MainWindow
        var loginWindow = new LoginWindow();
        loginWindow.Activate();
        App.MainWindow?.Close();
    }
}
