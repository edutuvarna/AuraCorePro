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
        LoginForm.Visibility = Visibility.Visible;
        RegisterForm.Visibility = Visibility.Collapsed;
        LoginTab.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"];
        RegisterTab.Style = null;
    }

    private void RegisterTab_Click(object sender, RoutedEventArgs e)
    {
        LoginForm.Visibility = Visibility.Collapsed;
        RegisterForm.Visibility = Visibility.Visible;
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

    private async Task AuthenticateAsync(string action, string email, string password)
    {
        SetLoading(true);
        try
        {
            var endpoint = action == "login" ? "api/auth/login" : "api/auth/register";
            var response = await Http.PostAsJsonAsync($"{ApiBaseUrl}/{endpoint}", new { email, password });
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (response.IsSuccessStatusCode)
            {
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
