using System.Runtime.InteropServices;
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

            // Theme label
            ThemeLabel.Text = ThemeService.IsDarkMode
                ? LocalizationService._("set.dark")
                : LocalizationService._("set.light");

            // Language label
            LangLabel.Text = LocalizationService.CurrentLanguage == "tr" ? "T\u00FCrk\u00E7e" : "English";

            // AI Telemetry toggle
            AiTelemetryToggle.IsChecked = AIConsentSettings.IsConsentGiven();

            // Account info
            var email = SessionState.UserEmail;
            AccountEmail.Text = string.IsNullOrEmpty(email) ? LocalizationService._("set.notSignedIn") : email;
            AccountTier.Text = (SessionState.UserTier ?? "free").ToUpper();
            LogoutBtn.IsVisible = SessionState.IsAuthenticated;

            // AI Model selector
            AIModelSettings.Load();
            PopulateAiModelCombo();

            ApplyLocalization();
            LocalizationService.LanguageChanged += () =>
                Dispatcher.UIThread.Post(ApplyLocalization);
        };
    }

    private void PopulateAiModelCombo()
    {
        AiModelCombo.Items.Clear();
        int selectedIndex = 0;
        for (int i = 0; i < AIModelSettings.AvailableModels.Length; i++)
        {
            var m = AIModelSettings.AvailableModels[i];
            AiModelCombo.Items.Add($"{m.DisplayName} \u2014 {m.RamUsage} ({m.Recommended})");
            if (m.Key == AIModelSettings.SelectedModel)
                selectedIndex = i;
        }
        AiModelCombo.SelectedIndex = selectedIndex;

        // Show system RAM
        try
        {
            var info = GC.GetGCMemoryInfo();
            var totalGb = info.TotalAvailableMemoryBytes / (1024.0 * 1024.0 * 1024.0);
            AiModelRamLabel.Text = string.Format(
                LocalizationService._("set.aiModelRam"),
                totalGb.ToString("F1"));
        }
        catch
        {
            AiModelRamLabel.Text = "";
        }
    }

    private void ApplyLocalization()
    {
        HeaderTitle.Text = LocalizationService._("set.title");
        HeaderSub.Text = LocalizationService._("set.subtitle");
        AppearanceLabel.Text = LocalizationService._("set.appearance");
        ThemeKeyLabel.Text = LocalizationService._("set.theme");
        ThemeDescLabel.Text = LocalizationService._("set.themeDesc");
        LangKeyLabel.Text = LocalizationService._("set.language");
        LangDescLabel.Text = LocalizationService._("set.langDesc");
        AiTelemetryLabel.Text = LocalizationService._("set.aiTelemetry");
        AiTelemetryDescLabel.Text = LocalizationService._("set.aiTelemetryDesc");
        AiTelemetryNoteLabel.Text = LocalizationService._("set.aiTelemetryNote");
        AboutLabel.Text = LocalizationService._("set.about");
        AccountLabel.Text = LocalizationService._("set.account");
        LogoutBtn.Content = LocalizationService._("set.signOut");
        AiModelLabel.Text = LocalizationService._("set.aiModel");
        AiModelRestartLabel.Text = LocalizationService._("set.aiModelRestart");

        ThemeLabel.Text = ThemeService.IsDarkMode
            ? LocalizationService._("set.dark")
            : LocalizationService._("set.light");
        LangLabel.Text = LocalizationService.CurrentLanguage == "tr" ? "T\u00FCrk\u00E7e" : "English";

        var email = SessionState.UserEmail;
        AccountEmail.Text = string.IsNullOrEmpty(email) ? LocalizationService._("set.notSignedIn") : email;
    }

    private void ThemeToggle_Click(object? sender, RoutedEventArgs e)
    {
        ThemeService.Toggle();
        ThemeLabel.Text = ThemeService.IsDarkMode
            ? LocalizationService._("set.dark")
            : LocalizationService._("set.light");
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

    private void AiModelCombo_Changed(object? sender, SelectionChangedEventArgs e)
    {
        var idx = AiModelCombo.SelectedIndex;
        if (idx >= 0 && idx < AIModelSettings.AvailableModels.Length)
        {
            var model = AIModelSettings.AvailableModels[idx];
            if (model.Key != AIModelSettings.SelectedModel)
            {
                AIModelSettings.Save(model.Key);
                AiModelRestartLabel.IsVisible = true;
            }
        }
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
