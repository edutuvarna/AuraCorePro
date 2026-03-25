using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AuraCore.Application;
using AuraCore.Desktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AuraCore.Desktop;

public sealed partial class LoginWindow : Microsoft.UI.Xaml.Window
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static string? AccessToken { get; set; }
    public static string? RefreshToken { get; set; }
    public static string? UserEmail { get; set; }
    public static string? UserRole { get; set; }
    public static string? UserTier { get; set; } = "free";
    public static string? UserId { get; set; }
    public static string ApiBaseUrl { get; set; } = "https://api.auracore.pro";

    public LoginWindow()
    {
        InitializeComponent();
        Title = "Aura Core Pro";
        ExtendsContentIntoTitleBar = true;
        var saved = LoadSetting("ApiBaseUrl");
        if (!string.IsNullOrEmpty(saved)) ApiBaseUrl = saved;

        ApplyLocalization();

        // Try auto-login with saved session
        _ = TryAutoLoginAsync();
    }

    // ── AUTO LOGIN ───────────────────────────────────────────

    private async Task TryAutoLoginAsync()
    {
        var savedToken = LoadSetting("RefreshToken");
        var savedEmail = LoadSetting("UserEmail");
        if (string.IsNullOrEmpty(savedToken) || string.IsNullOrEmpty(savedEmail)) return;

        SetLoading(true);
        StatusText.Text = S._("login.autoLogin") ?? "Signing in...";

        try
        {
            var response = await Http.PostAsJsonAsync($"{ApiBaseUrl}/api/auth/refresh", new { refreshToken = savedToken });
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);

                AccessToken = doc.RootElement.GetProperty("accessToken").GetString();
                RefreshToken = doc.RootElement.GetProperty("refreshToken").GetString();

                var user = doc.RootElement.GetProperty("user");
                UserEmail = user.GetProperty("email").GetString();
                UserRole = user.GetProperty("role").GetString();
                UserId = user.GetProperty("id").GetString();
                UserTier = user.TryGetProperty("tier", out var tierProp) ? tierProp.GetString() ?? "free" : "free";
                if (UserRole == "admin") UserTier = "admin";

                SaveSession();
                SyncSessionState();
                OpenMainWindow();
                return;
            }
        }
        catch { }

        // Auto-login failed - clear saved session and show login form
        ClearSavedSession();
        SetLoading(false);
        StatusText.Text = "";
    }

    // ── SESSION PERSISTENCE ──────────────────────────────────

    private static void SaveSession()
    {
        SaveSetting("AccessToken", AccessToken ?? "");
        SaveSetting("RefreshToken", RefreshToken ?? "");
        SaveSetting("UserEmail", UserEmail ?? "");
        SaveSetting("UserRole", UserRole ?? "");
        SaveSetting("UserTier", UserTier ?? "free");
        SaveSetting("UserId", UserId ?? "");
    }

    public static void ClearSavedSession()
    {
        SaveSetting("AccessToken", "");
        SaveSetting("RefreshToken", "");
        SaveSetting("UserEmail", "");
        SaveSetting("UserRole", "");
        SaveSetting("UserTier", "");
        SaveSetting("UserId", "");
    }

    // ── LANGUAGE ──────────────────────────────────────────────

    private void ApplyLocalization()
    {
        TitleText.Text = S._("login.title");
        SubtitleText.Text = S._("login.subtitle");
        LoginTab.Content = S._("login.signIn");
        RegisterTab.Content = S._("login.createAccount");
        LoginEmail.Header = S._("login.email");
        LoginEmail.PlaceholderText = S._("login.emailPlaceholder");
        LoginPassword.Header = S._("login.password");
        LoginPassword.PlaceholderText = S._("login.passwordPlaceholder");
        LoginBtn.Content = S._("login.signIn");
        RegEmail.Header = S._("login.email");
        RegEmail.PlaceholderText = S._("login.emailPlaceholder");
        RegPassword.Header = S._("login.password");
        RegPassword.PlaceholderText = S._("login.minChars");
        RegConfirm.Header = S._("login.confirmPassword");
        RegConfirm.PlaceholderText = S._("login.repeatPassword");
        RegisterBtn.Content = S._("login.createAccount");
        OfflineBtn.Content = S._("login.offline");

        var isEN = S.Current == S.Lang.EN;
        LangEN.Style = isEN ? (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"] : null;
        LangTR.Style = !isEN ? (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"] : null;
    }

    private void LangEN_Click(object sender, RoutedEventArgs e)
    {
        S.SetLanguage(S.Lang.EN);
        ApplyLocalization();
    }

    private void LangTR_Click(object sender, RoutedEventArgs e)
    {
        S.SetLanguage(S.Lang.TR);
        ApplyLocalization();
    }

    private void LoginTab_Click(object sender, RoutedEventArgs e)
    {
        ShowForm("login");
        LoginTab.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"];
        RegisterTab.Style = null;
    }

    private void RegisterTab_Click(object sender, RoutedEventArgs e)
    {
        ShowForm("register");
        RegisterTab.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"];
        LoginTab.Style = null;
    }

    private async void LoginBtn_Click(object sender, RoutedEventArgs e)
    {
        var email = LoginEmail.Text.Trim();
        var password = LoginPassword.Password;
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        { StatusText.Text = S._("login.invalidEmail"); return; }
        await AuthenticateAsync("login", email, password);
    }

    private async void RegisterBtn_Click(object sender, RoutedEventArgs e)
    {
        var email = RegEmail.Text.Trim();
        var password = RegPassword.Password;
        var confirm = RegConfirm.Password;
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        { StatusText.Text = S._("login.invalidEmail"); return; }
        if (password.Length < 8)
        { StatusText.Text = S._("login.passwordTooShort"); return; }
        if (password != confirm)
        { StatusText.Text = S._("login.passwordMismatch"); return; }
        await AuthenticateAsync("register", email, password);
    }

    private async Task AuthenticateAsync(string action, string email, string password, string? totpCode = null)
    {
        SetLoading(true);
        try
        {
            var endpoint = action == "login" ? "api/auth/login" : "api/auth/register";

            // Build request body - include TOTP code if provided
            object body = totpCode != null
                ? new { email, password, totpCode }
                : new { email, password };

            var response = await Http.PostAsJsonAsync($"{ApiBaseUrl}/{endpoint}", body);
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (response.IsSuccessStatusCode)
            {
                // Check if 2FA is required
                if (doc.RootElement.TryGetProperty("requires2fa", out var r2fa) && r2fa.GetBoolean())
                {
                    SetLoading(false);
                    var code = await Show2faDialogAsync();
                    if (!string.IsNullOrEmpty(code))
                    {
                        // Retry login with TOTP code
                        await AuthenticateAsync("login", email, password, code);
                    }
                    else
                    {
                        StatusText.Text = S.Current == S.Lang.EN
                            ? "2FA code required to sign in"
                            : "Giris icin 2FA kodu gerekli";
                    }
                    return;
                }

                AccessToken = doc.RootElement.GetProperty("accessToken").GetString();
                RefreshToken = doc.RootElement.GetProperty("refreshToken").GetString();
                var user = doc.RootElement.GetProperty("user");
                UserEmail = user.GetProperty("email").GetString();
                UserRole = user.GetProperty("role").GetString();
                UserId = user.GetProperty("id").GetString();

                UserTier = user.TryGetProperty("tier", out var tierProp) ? tierProp.GetString() ?? "free" : "free";
                if (UserRole == "admin") UserTier = "admin";

                SaveSession();
                SyncSessionState();
                OpenMainWindow();
            }
            else
            {
                var error = doc.RootElement.TryGetProperty("error", out var errProp) ? errProp.GetString() : "Authentication failed";
                StatusText.Text = error;
            }
        }
        catch (HttpRequestException) { StatusText.Text = S._("login.cannotConnect"); }
        catch (TaskCanceledException) { StatusText.Text = S._("login.timeout"); }
        catch (Exception ex) { StatusText.Text = $"{S._("common.error")}: {ex.Message}"; }
        finally { SetLoading(false); }
    }

    private async Task<string?> Show2faDialogAsync()
    {
        var input = new TextBox
        {
            PlaceholderText = "000000",
            MaxLength = 6,
            TextAlignment = Microsoft.UI.Xaml.TextAlignment.Center,
            FontSize = 24,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = S.Current == S.Lang.EN
                ? "Enter the 6-digit code from your authenticator app"
                : "Dogrulama uygulamanizdan 6 haneli kodu girin",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
            FontSize = 13
        });
        panel.Children.Add(input);

        var dialog = new ContentDialog
        {
            Title = S.Current == S.Lang.EN ? "Two-Factor Authentication" : "Iki Adimli Dogrulama",
            Content = panel,
            PrimaryButtonText = S.Current == S.Lang.EN ? "Verify" : "Dogrula",
            CloseButtonText = S.Current == S.Lang.EN ? "Cancel" : "Iptal",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && input.Text.Length == 6)
            return input.Text.Trim();

        return null;
    }

    private async Task FetchUserTierAsync()
    {
        try
        {
            Http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
            var resp = await Http.GetAsync($"{ApiBaseUrl}/api/license/validate?key=self&device=self");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("tier", out var tierProp))
                    UserTier = tierProp.GetString();
            }
        }
        catch { }
    }

    private void OfflineBtn_Click(object sender, RoutedEventArgs e)
    {
        AccessToken = null; UserEmail = "offline@local";
        UserRole = "user"; UserTier = "free"; UserId = null;
        SyncSessionState();
        OpenMainWindow();
    }

    // ── FORGOT PASSWORD ─────────────────────────────────────

    private void ShowForm(string form)
    {
        LoginForm.Visibility = form == "login" ? Visibility.Visible : Visibility.Collapsed;
        RegisterForm.Visibility = form == "register" ? Visibility.Visible : Visibility.Collapsed;
        ForgotForm.Visibility = form == "forgot" ? Visibility.Visible : Visibility.Collapsed;
        ResetForm.Visibility = form == "reset" ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = "";
    }

    private void ForgotLink_Click(object sender, RoutedEventArgs e)
    {
        ForgotEmail.Text = LoginEmail.Text.Trim();
        ShowForm("forgot");
    }

    private void BackToLogin_Click(object sender, RoutedEventArgs e)
    {
        ShowForm("login");
    }

    private async void ForgotBtn_Click(object sender, RoutedEventArgs e)
    {
        var email = ForgotEmail.Text.Trim();
        if (string.IsNullOrEmpty(email))
        { StatusText.Text = S._("login.invalidEmail"); return; }

        SetLoading(true);
        ForgotBtn.IsEnabled = false;
        try
        {
            var response = await Http.PostAsJsonAsync($"{ApiBaseUrl}/api/auth/password/forgot", new { email });
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (response.IsSuccessStatusCode)
            {
                StatusText.Text = S.Current == S.Lang.EN
                    ? "Reset code sent! Check your email."
                    : "Sıfırlama kodu gönderildi! E-postanızı kontrol edin.";
                ResetEmail.Text = email;
                await Task.Delay(1500);
                ShowForm("reset");
            }
            else
            {
                var error = doc.RootElement.TryGetProperty("error", out var errProp) ? errProp.GetString() : "Failed";
                StatusText.Text = error;
            }
        }
        catch (HttpRequestException) { StatusText.Text = S._("login.cannotConnect"); }
        catch (TaskCanceledException) { StatusText.Text = S._("login.timeout"); }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally { SetLoading(false); ForgotBtn.IsEnabled = true; }
    }

    private async void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        var email = ResetEmail.Text.Trim();
        var code = ResetCode.Text.Trim();
        var pass = ResetNewPass.Password;
        var confirm = ResetConfirmPass.Password;

        if (string.IsNullOrEmpty(code) || code.Length != 6)
        { StatusText.Text = S.Current == S.Lang.EN ? "Enter the 6-digit code" : "6 haneli kodu girin"; return; }
        if (string.IsNullOrEmpty(pass) || pass.Length < 8)
        { StatusText.Text = S._("login.passwordTooShort"); return; }
        if (pass != confirm)
        { StatusText.Text = S._("login.passwordMismatch"); return; }

        SetLoading(true);
        ResetBtn.IsEnabled = false;
        try
        {
            var response = await Http.PostAsJsonAsync($"{ApiBaseUrl}/api/auth/password/reset",
                new { email, code, newPassword = pass });
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (response.IsSuccessStatusCode)
            {
                StatusText.Text = S.Current == S.Lang.EN
                    ? "Password reset! You can now sign in."
                    : "Şifre sıfırlandı! Artık giriş yapabilirsiniz.";
                LoginEmail.Text = email;
                await Task.Delay(2000);
                ShowForm("login");
            }
            else
            {
                var error = doc.RootElement.TryGetProperty("error", out var errProp) ? errProp.GetString() : "Failed";
                StatusText.Text = error;
            }
        }
        catch (HttpRequestException) { StatusText.Text = S._("login.cannotConnect"); }
        catch (TaskCanceledException) { StatusText.Text = S._("login.timeout"); }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally { SetLoading(false); ResetBtn.IsEnabled = true; }
    }

    private static void SyncSessionState()
    {
        SessionState.AccessToken = AccessToken;
        SessionState.ApiBaseUrl = ApiBaseUrl;
        SessionState.UserEmail = UserEmail;
        SessionState.UserRole = UserRole;
        SessionState.UserTier = UserTier;
        SessionState.UserId = UserId;
    }

    private void OpenMainWindow()
    {
        var main = new MainWindow();
        App.MainWindow = main;
        main.Activate();
        this.Close();
    }

    private void SetLoading(bool loading)
    {
        Progress.IsActive = loading;
        Progress.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        LoginBtn.IsEnabled = !loading; RegisterBtn.IsEnabled = !loading;
        ForgotBtn.IsEnabled = !loading; ResetBtn.IsEnabled = !loading;
        if (loading) StatusText.Text = "";
    }

    public static void SaveSetting(string key, string value)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuraCorePro");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"{key}.txt"), value);
        }
        catch { }
    }

    public static string? LoadSetting(string key)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuraCorePro", $"{key}.txt");
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch { return null; }
    }
}
