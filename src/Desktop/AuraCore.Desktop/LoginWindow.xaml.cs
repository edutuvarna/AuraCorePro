using System.Net.Http;
using System.Text;
using System.Text.Json;
using AuraCore.Application;
using AuraCore.Desktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AuraCore.Desktop;

public sealed partial class LoginWindow : Microsoft.UI.Xaml.Window
{
    public static string? AccessToken { get; set; }
    public static string? RefreshToken { get; set; }
    public static string? UserEmail { get; set; }
    public static string? UserRole { get; set; }
    public static string? UserTier { get; set; } = "free";
    public static string? UserId { get; set; }
    public static string ApiBaseUrl { get; set; } = "https://api.auracore.pro";

    private bool _windowClosed;

    // ── DEBUG LOG ────────────────────────────────────────────
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "login-debug.log");

    private static void Log(string msg)
    {
        try
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n";
            File.AppendAllText(LogFile, line);
        }
        catch { }
    }

    public LoginWindow()
    {
        Log("=== LoginWindow constructor START ===");
        InitializeComponent();
        Title = "Aura Core Pro";
        ExtendsContentIntoTitleBar = true;
        var saved = LoadSetting("ApiBaseUrl");
        if (!string.IsNullOrEmpty(saved)) ApiBaseUrl = saved;
        this.Closed += (s, e) => { _windowClosed = true; Log("Window.Closed event"); };
        ApplyLocalization();
        Log($"ApiBaseUrl = {ApiBaseUrl}");
        _ = TryAutoLoginAsync();
        Log("Constructor END (TryAutoLogin fired)");
    }

    // ── SAFE UI HELPERS ──────────────────────────────────────

    private void UI(Action action)
    {
        if (_windowClosed) return;
        try { DispatcherQueue?.TryEnqueue(() => { if (!_windowClosed) action(); }); } catch { }
    }

    private void SetStatus(string text) => UI(() => { try { StatusText.Text = text; } catch { } });

    private void SetLoading(bool on) => UI(() =>
    {
        try
        {
            Progress.IsActive = on;
            Progress.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            LoginBtn.IsEnabled = !on; RegisterBtn.IsEnabled = !on;
            ForgotBtn.IsEnabled = !on; ResetBtn.IsEnabled = !on;
        }
        catch { }
    });

    // ── AUTO LOGIN ───────────────────────────────────────────

    private async Task TryAutoLoginAsync()
    {
        Log("TryAutoLogin START");
        var savedToken = LoadSetting("RefreshToken");
        var savedEmail = LoadSetting("UserEmail");
        if (string.IsNullOrEmpty(savedToken) || string.IsNullOrEmpty(savedEmail))
        {
            Log("TryAutoLogin: No saved session, skipping");
            return;
        }

        SetLoading(true);
        SetStatus(S._("login.autoLogin") ?? "Signing in...");
        Log($"TryAutoLogin: Attempting refresh for {savedEmail}");

        try
        {
            Log("TryAutoLogin: Creating HTTP request...");
            var url = $"{ApiBaseUrl}/api/auth/refresh";
            var jsonBody = JsonSerializer.Serialize(new { refreshToken = savedToken });

            Log($"TryAutoLogin: POST {url}");
            var httpTask = Task.Run(async () =>
            {
                Log("TryAutoLogin [BG]: Inside Task.Run, creating HttpClient...");
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                Log("TryAutoLogin [BG]: Sending POST...");
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                var resp = await http.PostAsync(url, content);
                Log($"TryAutoLogin [BG]: Got response {(int)resp.StatusCode}");
                var body = await resp.Content.ReadAsStringAsync();
                Log($"TryAutoLogin [BG]: Body length = {body.Length}");
                return (resp.IsSuccessStatusCode, body);
            });

            Log("TryAutoLogin: Waiting with WhenAny (7s fallback)...");
            var timeout = Task.Delay(TimeSpan.FromSeconds(7));
            var completed = await Task.WhenAny(httpTask, timeout);

            if (completed == timeout)
            {
                Log("TryAutoLogin: TIMEOUT - 7s elapsed");
                ClearSavedSession();
                SetLoading(false);
                SetStatus("Connection timed out");
                return;
            }

            Log("TryAutoLogin: HTTP completed before timeout");
            var (ok, json) = await httpTask;
            Log($"TryAutoLogin: ok={ok}, json.Length={json.Length}");

            if (ok)
            {
                var doc = JsonDocument.Parse(json);
                ParseLoginResponse(doc);
                SaveSession();
                SyncSessionState();
                Log("TryAutoLogin: SUCCESS - calling OpenMainWindow via UI()");
                UI(() =>
                {
                    Log("TryAutoLogin [UI]: OpenMainWindow...");
                    OpenMainWindow();
                    Log("TryAutoLogin [UI]: OpenMainWindow done");
                    Task.Run(() => DeviceRegistrationService.RegisterAsync());
                });
                return;
            }
            else
            {
                Log($"TryAutoLogin: Server returned error: {json[..Math.Min(100, json.Length)]}");
            }
        }
        catch (Exception ex)
        {
            Log($"TryAutoLogin: EXCEPTION {ex.GetType().Name}: {ex.Message}");
        }

        Log("TryAutoLogin: Failed, clearing session");
        ClearSavedSession();
        SetLoading(false);
        SetStatus("");
    }

    // ── AUTHENTICATE ─────────────────────────────────────────

    private async Task AuthenticateAsync(string action, string email, string password, string? totpCode = null)
    {
        Log($"AuthenticateAsync START action={action} email={email}");
        SetLoading(true);
        SetStatus("Connecting...");

        try
        {
            var endpoint = action == "login" ? "api/auth/login" : "api/auth/register";
            var url = $"{ApiBaseUrl}/{endpoint}";

            string jsonBody;
            if (totpCode != null)
                jsonBody = JsonSerializer.Serialize(new { email, password, totpCode });
            else
                jsonBody = JsonSerializer.Serialize(new { email, password });

            SetStatus("Authenticating...");
            Log($"AuthenticateAsync: POST {url}");

            var httpTask = Task.Run(async () =>
            {
                Log("Auth [BG]: Inside Task.Run, creating HttpClient...");
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                Log("Auth [BG]: Sending POST...");
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                var resp = await http.PostAsync(url, content);
                Log($"Auth [BG]: Got response {(int)resp.StatusCode}");
                var body = await resp.Content.ReadAsStringAsync();
                Log($"Auth [BG]: Body length = {body.Length}");
                return (resp.IsSuccessStatusCode, body);
            });

            Log("AuthenticateAsync: Waiting with WhenAny (10s fallback)...");
            var timeout = Task.Delay(TimeSpan.FromSeconds(10));
            var completed = await Task.WhenAny(httpTask, timeout);

            if (completed == timeout)
            {
                Log("AuthenticateAsync: TIMEOUT - 10s elapsed");
                SetStatus("Connection timed out. Please try again.");
                SetLoading(false);
                return;
            }

            Log("AuthenticateAsync: HTTP completed before timeout");
            var (ok, json) = await httpTask;
            Log($"AuthenticateAsync: ok={ok}");

            var doc = JsonDocument.Parse(json);

            if (ok)
            {
                // 2FA check
                if (doc.RootElement.TryGetProperty("requires2fa", out var r2fa) && r2fa.GetBoolean())
                {
                    Log("AuthenticateAsync: 2FA required");
                    SetLoading(false);
                    var code = await Show2faDialogAsync();
                    if (!string.IsNullOrEmpty(code))
                        await AuthenticateAsync("login", email, password, code);
                    else
                        SetStatus(S.Current == S.Lang.EN ? "2FA code required to sign in" : "Giris icin 2FA kodu gerekli");
                    return;
                }

                Log("AuthenticateAsync: Parsing response...");
                ParseLoginResponse(doc);
                SetStatus("Loading app...");
                SaveSession();
                SyncSessionState();
                Log("AuthenticateAsync: SUCCESS - calling OpenMainWindow via UI()");
                UI(() =>
                {
                    Log("Auth [UI]: OpenMainWindow...");
                    OpenMainWindow();
                    Log("Auth [UI]: OpenMainWindow done");
                    Task.Run(() => DeviceRegistrationService.RegisterAsync());
                });
                return;
            }
            else
            {
                var error = doc.RootElement.TryGetProperty("error", out var errProp) ? errProp.GetString() : "Authentication failed";
                Log($"AuthenticateAsync: Server error: {error}");
                SetStatus(error ?? "Authentication failed");
            }
        }
        catch (Exception ex)
        {
            Log($"AuthenticateAsync: EXCEPTION {ex.GetType().Name}: {ex.Message}");
            SetStatus($"Error: {ex.Message}");
        }

        Log("AuthenticateAsync: END (setting loading false)");
        SetLoading(false);
    }

    // ── PARSE RESPONSE ───────────────────────────────────────

    private static void ParseLoginResponse(JsonDocument doc)
    {
        AccessToken = doc.RootElement.GetProperty("accessToken").GetString();
        RefreshToken = doc.RootElement.GetProperty("refreshToken").GetString();
        var user = doc.RootElement.GetProperty("user");
        UserEmail = user.GetProperty("email").GetString();
        UserRole = user.GetProperty("role").GetString();
        UserId = user.GetProperty("id").GetString();
        UserTier = user.TryGetProperty("tier", out var tierProp) ? tierProp.GetString() ?? "free" : "free";
        if (UserRole == "admin") UserTier = "admin";
        Log($"ParseLoginResponse: email={UserEmail} role={UserRole} tier={UserTier}");
    }

    // ── SESSION ──────────────────────────────────────────────

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
        Log("OpenMainWindow START");
        if (_windowClosed) { Log("OpenMainWindow: window already closed, abort"); return; }
        _windowClosed = true;
        Log("OpenMainWindow: Creating MainWindow...");
        var main = new MainWindow();
        Log("OpenMainWindow: MainWindow created, setting App.MainWindow...");
        App.MainWindow = main;
        Log("OpenMainWindow: Activating...");
        main.Activate();
        Log("OpenMainWindow: Closing LoginWindow...");
        this.Close();
        Log("OpenMainWindow END");
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

    private void LangEN_Click(object sender, RoutedEventArgs e) { S.SetLanguage(S.Lang.EN); ApplyLocalization(); }
    private void LangTR_Click(object sender, RoutedEventArgs e) { S.SetLanguage(S.Lang.TR); ApplyLocalization(); }

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
        Log("LoginBtn_Click");
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

    private void OfflineBtn_Click(object sender, RoutedEventArgs e)
    {
        Log("OfflineBtn_Click");
        AccessToken = null; UserEmail = "offline@local";
        UserRole = "user"; UserTier = "free"; UserId = null;
        SyncSessionState();
        OpenMainWindow();
    }

    // ── 2FA DIALOG ───────────────────────────────────────────

    private async Task<string?> Show2faDialogAsync()
    {
        var input = new TextBox
        {
            PlaceholderText = "000000", MaxLength = 6,
            TextAlignment = Microsoft.UI.Xaml.TextAlignment.Center,
            FontSize = 24, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = S.Current == S.Lang.EN
                ? "Enter the 6-digit code from your authenticator app"
                : "Dogrulama uygulamanizdan 6 haneli kodu girin",
            TextWrapping = TextWrapping.Wrap, Opacity = 0.7, FontSize = 13
        });
        panel.Children.Add(input);
        var dialog = new ContentDialog
        {
            Title = S.Current == S.Lang.EN ? "Two-Factor Authentication" : "Iki Adimli Dogrulama",
            Content = panel,
            PrimaryButtonText = S.Current == S.Lang.EN ? "Verify" : "Dogrula",
            CloseButtonText = S.Current == S.Lang.EN ? "Cancel" : "Iptal",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = Content.XamlRoot
        };
        var result = await dialog.ShowAsync();
        return (result == ContentDialogResult.Primary && input.Text.Length == 6) ? input.Text.Trim() : null;
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

    private void ForgotLink_Click(object sender, RoutedEventArgs e) { ForgotEmail.Text = LoginEmail.Text.Trim(); ShowForm("forgot"); }
    private void BackToLogin_Click(object sender, RoutedEventArgs e) => ShowForm("login");

    private async void ForgotBtn_Click(object sender, RoutedEventArgs e)
    {
        var email = ForgotEmail.Text.Trim();
        if (string.IsNullOrEmpty(email)) { StatusText.Text = S._("login.invalidEmail"); return; }
        SetLoading(true);
        try
        {
            var body = JsonSerializer.Serialize(new { email });
            var httpTask = Task.Run(async () =>
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                var c = new StringContent(body, Encoding.UTF8, "application/json");
                var resp = await http.PostAsync($"{ApiBaseUrl}/api/auth/password/forgot", c);
                var rb = await resp.Content.ReadAsStringAsync();
                return (resp.IsSuccessStatusCode, rb);
            });
            var timeout = Task.Delay(TimeSpan.FromSeconds(10));
            if (await Task.WhenAny(httpTask, timeout) == timeout)
            { SetStatus("Timed out"); SetLoading(false); return; }
            var (ok, json) = await httpTask;
            var doc = JsonDocument.Parse(json);
            if (ok)
            {
                SetStatus(S.Current == S.Lang.EN ? "Reset code sent! Check your email." : "Sifirlama kodu gonderildi!");
                UI(() => { ResetEmail.Text = email; });
                await Task.Delay(1500);
                UI(() => ShowForm("reset"));
            }
            else
            {
                var error = doc.RootElement.TryGetProperty("error", out var p) ? p.GetString() : "Failed";
                SetStatus(error ?? "Failed");
            }
        }
        catch (Exception ex) { SetStatus($"Error: {ex.Message}"); }
        SetLoading(false);
        UI(() => ForgotBtn.IsEnabled = true);
    }

    private async void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        var email = ResetEmail.Text.Trim();
        var code = ResetCode.Text.Trim();
        var pass = ResetNewPass.Password;
        var confirm = ResetConfirmPass.Password;
        if (string.IsNullOrEmpty(code) || code.Length != 6) { StatusText.Text = "Enter the 6-digit code"; return; }
        if (string.IsNullOrEmpty(pass) || pass.Length < 8) { StatusText.Text = S._("login.passwordTooShort"); return; }
        if (pass != confirm) { StatusText.Text = S._("login.passwordMismatch"); return; }
        SetLoading(true);
        try
        {
            var body = JsonSerializer.Serialize(new { email, code, newPassword = pass });
            var httpTask = Task.Run(async () =>
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                var c = new StringContent(body, Encoding.UTF8, "application/json");
                var resp = await http.PostAsync($"{ApiBaseUrl}/api/auth/password/reset", c);
                var rb = await resp.Content.ReadAsStringAsync();
                return (resp.IsSuccessStatusCode, rb);
            });
            var timeout = Task.Delay(TimeSpan.FromSeconds(10));
            if (await Task.WhenAny(httpTask, timeout) == timeout)
            { SetStatus("Timed out"); SetLoading(false); return; }
            var (ok, json) = await httpTask;
            var doc = JsonDocument.Parse(json);
            if (ok)
            {
                SetStatus("Password reset! You can now sign in.");
                UI(() => { LoginEmail.Text = email; });
                await Task.Delay(2000);
                UI(() => ShowForm("login"));
            }
            else
            {
                var error = doc.RootElement.TryGetProperty("error", out var p) ? p.GetString() : "Failed";
                SetStatus(error ?? "Failed");
            }
        }
        catch (Exception ex) { SetStatus($"Error: {ex.Message}"); }
        SetLoading(false);
        UI(() => ResetBtn.IsEnabled = true);
    }

    private async Task FetchUserTierAsync()
    {
        try
        {
            var httpTask = Task.Run(async () =>
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
                var resp = await http.GetAsync($"{ApiBaseUrl}/api/license/validate?key=self&device=self");
                var body = await resp.Content.ReadAsStringAsync();
                return (resp.IsSuccessStatusCode, body);
            });
            var timeout = Task.Delay(TimeSpan.FromSeconds(7));
            if (await Task.WhenAny(httpTask, timeout) == timeout) return;
            var (ok, json) = await httpTask;
            if (ok)
            {
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("tier", out var tierProp))
                    UserTier = tierProp.GetString();
            }
        }
        catch { }
    }

    // ── SETTINGS ─────────────────────────────────────────────

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
