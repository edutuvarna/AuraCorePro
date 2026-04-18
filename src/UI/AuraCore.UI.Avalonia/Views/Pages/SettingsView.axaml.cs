using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Threading;
using AuraCore.Application;
using AuraCore.UI.Avalonia.Views;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            RuntimeLabel.Text = $".NET {Environment.Version}";
            PlatformLabel.Text = OperatingSystem.IsWindows() ? "Windows"
                               : OperatingSystem.IsLinux() ? "Linux"
                               : OperatingSystem.IsMacOS() ? "macOS" : "Unknown";

            // Language label
            LangLabel.Text = LocalizationService.CurrentLanguage == "tr" ? "T\u00FCrk\u00E7e" : "English";

            // AI Telemetry toggle
            AiTelemetryToggle.IsChecked = AIConsentSettings.IsConsentGiven();

            // Account info — ApplyLocalization sets AccountEmail, AccountTier, LogoutBtn.Content
            LogoutBtn.IsVisible = SessionState.IsAuthenticated;

            // AI model management moved to AI Features → Chat (Task 14+).

            // Set initial theme radio state
            switch (ThemeService.CurrentTheme)
            {
                case ThemeService.AppTheme.System: ThemeSystemRb.IsChecked = true; break;
                case ThemeService.AppTheme.Light:  ThemeLightRb.IsChecked  = true; break;
                default:                           ThemeDarkRb.IsChecked   = true; break;
            }

            // Wire theme radio change handlers
            ThemeSystemRb.IsCheckedChanged += (_, _) => { if (ThemeSystemRb.IsChecked == true) ThemeService.SetTheme(ThemeService.AppTheme.System); };
            ThemeLightRb.IsCheckedChanged  += (_, _) => { if (ThemeLightRb.IsChecked  == true) ThemeService.SetTheme(ThemeService.AppTheme.Light);  };
            ThemeDarkRb.IsCheckedChanged   += (_, _) => { if (ThemeDarkRb.IsChecked   == true) ThemeService.SetTheme(ThemeService.AppTheme.Dark);   };

            ApplyLocalization();
            LocalizationService.LanguageChanged += () =>
                Dispatcher.UIThread.Post(ApplyLocalization);
        };
    }

    private void ApplyLocalization()
    {
        HeaderTitle.Text = LocalizationService._("set.title");
        HeaderSub.Text = LocalizationService._("set.subtitle");
        AppearanceLabel.Text = LocalizationService._("set.appearance");
        ThemeKeyLabel.Text = LocalizationService._("set.theme");
        ThemeDescLabel.Text = LocalizationService._("settings.chooseTheme");
        ThemeSystemRb.Content = LocalizationService._("settings.systemDefault");
        ThemeLightRb.Content  = LocalizationService._("settings.light");
        ThemeDarkRb.Content   = LocalizationService._("settings.dark");
        LangKeyLabel.Text = LocalizationService._("set.language");
        LangDescLabel.Text = LocalizationService._("set.langDesc");
        AiTelemetryLabel.Text = LocalizationService._("set.aiTelemetry");
        AiTelemetryDescLabel.Text = LocalizationService._("set.aiTelemetryDesc");
        AiTelemetryNoteLabel.Text = LocalizationService._("set.aiTelemetryNote");
        AiModelNoteLabel.Text = LocalizationService._("set.aiModelNote");
        AboutLabel.Text = LocalizationService._("set.about");
        // About section static labels
        VersionKeyLabel.Text = LocalizationService._("set.versionLabel");
        VersionValueLabel.Text = LocalizationService._("set.versionValue");
        RuntimeKeyLabel.Text = LocalizationService._("set.runtimeLabel");
        PlatformKeyLabel.Text = LocalizationService._("set.platformLabel");
        UiFrameworkKeyLabel.Text = LocalizationService._("set.uiFrameworkLabel");
        UiFrameworkValueLabel.Text = LocalizationService._("set.uiFrameworkValue");
        WebsiteLinkLabel.Text = LocalizationService._("set.websiteLink");
        AccountLabel.Text = LocalizationService._("set.account");
        LogoutBtn.Content = LocalizationService._("set.signOut");

        LangLabel.Text = LocalizationService.CurrentLanguage == "tr" ? "T\u00FCrk\u00E7e" : "English";

        var email = SessionState.UserEmail;
        AccountEmail.Text = string.IsNullOrEmpty(email) ? LocalizationService._("set.notSignedIn") : email;
        AccountTier.Text = string.IsNullOrEmpty(SessionState.UserTier) || SessionState.UserTier == "free"
            ? LocalizationService._("set.freeTier")
            : (SessionState.UserTier ?? "free").ToUpper();
    }

    private void LangToggle_Click(object? sender, RoutedEventArgs e)
    {
        var newLang = LocalizationService.CurrentLanguage == "en" ? "tr" : "en";
        LocalizationService.SetLanguage(newLang);
        // LanguageChanged event will trigger ApplyLocalization
    }

    private void AiTelemetryToggle_Changed(object? sender, RoutedEventArgs e)
    {
        if (AiTelemetryToggle.IsChecked is bool isOn)
            AIConsentSettings.Save(isOn);
    }

    private void Logout_Click(object? sender, RoutedEventArgs e)
    {
        SessionState.AccessToken = null;
        SessionState.UserEmail = null;
        SessionState.UserRole = null;
        SessionState.UserTier = "free";
        SessionState.UserId = null;

        LoginWindow.SaveSetting("AccessToken", "");
        LoginWindow.SaveSetting("RefreshToken", "");
        LoginWindow.SaveSetting("UserEmail", "");
        LoginWindow.SaveSetting("UserRole", "");
        LoginWindow.SaveSetting("UserTier", "");
        LoginWindow.SaveSetting("UserId", "");

        Dispatcher.UIThread.Post(() =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window currentWindow)
            {
                var login = new LoginWindow();
                login.Show();
                currentWindow.Close();
            }
        });
    }
}
