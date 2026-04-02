using System.Net.Http;
using System.Text;
using System.Text.Json;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using AuraCore.Application;

namespace AuraCore.UI.Avalonia.Views;

public partial class LoginWindow : Window
{
    public static string ApiBaseUrl { get; set; } = "https://api.auracore.pro";
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuraCorePro");
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            try { await TryAutoLoginAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"AutoLogin error: {ex.Message}"); }
        };
    }

    // ── AUTO LOGIN ────────────────────────────────────────

    private async Task TryAutoLoginAsync()
    {
        var savedToken = LoadSetting("RefreshToken");
        var savedEmail = LoadSetting("UserEmail");
        if (string.IsNullOrEmpty(savedToken) || string.IsNullOrEmpty(savedEmail)) return;

        SetStatus("Signing in...", false);
        SetLoading(true);

        try
        {
            var url = $"{ApiBaseUrl}/api/auth/refresh";
            var json = JsonSerializer.Serialize(new { refreshToken = savedToken });

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(7) };
            var resp = await http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var doc = JsonDocument.Parse(body);
                ParseLoginResponse(doc);
                SaveSession();
                OpenMainWindow();
                return;
            }
        }
        catch { }

        ClearSavedSession();
        SetLoading(false);
        SetStatus("", false);
    }

    // ── AUTH ──────────────────────────────────────────────

    private async Task AuthenticateAsync(string action, string email, string password, string? totpCode = null)
    {
        SetLoading(true);
        SetStatus("Connecting...", false);

        try
        {
            var endpoint = action == "login" ? "api/auth/login" : "api/auth/register";
            var url = $"{ApiBaseUrl}/{endpoint}";

            string jsonBody;
            if (totpCode != null)
                jsonBody = JsonSerializer.Serialize(new { email, password, totpCode });
            else
                jsonBody = JsonSerializer.Serialize(new { email, password });

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var resp = await http.PostAsync(url, new StringContent(jsonBody, Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);

            if (resp.IsSuccessStatusCode)
            {
                // 2FA check
                if (doc.RootElement.TryGetProperty("requires2fa", out var r2fa) && r2fa.GetBoolean())
                {
                    SetLoading(false);
                    var code = await Show2faDialogAsync();
                    if (!string.IsNullOrEmpty(code))
                        await AuthenticateAsync("login", email, password, code);
                    else
                        SetStatus("2FA code required", true);
                    return;
                }

                ParseLoginResponse(doc);
                SetStatus("Loading app...", false);
                SaveSession();
                OpenMainWindow();
                return;
            }
            else
            {
                var error = doc.RootElement.TryGetProperty("error", out var errProp)
                    ? errProp.GetString() : "Authentication failed";
                SetStatus(error ?? "Authentication failed", true);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Connection error: {ex.Message}", true);
        }

        SetLoading(false);
    }

    // ── PARSE ─────────────────────────────────────────────

    private static void ParseLoginResponse(JsonDocument doc)
    {
        try
        {
            SessionState.AccessToken = doc.RootElement.GetProperty("accessToken").GetString();
            var refreshToken = doc.RootElement.GetProperty("refreshToken").GetString();
            var user = doc.RootElement.GetProperty("user");
            SessionState.UserEmail = user.GetProperty("email").GetString();
            SessionState.UserRole = user.GetProperty("role").GetString();
            SessionState.UserId = user.GetProperty("id").GetString();
            SessionState.UserTier = user.TryGetProperty("tier", out var tierProp)
                ? tierProp.GetString() ?? "free" : "free";
            if (SessionState.UserRole == "admin") SessionState.UserTier = "admin";
            SessionState.ApiBaseUrl = ApiBaseUrl;

            // Store refresh token for persistence
            SaveSetting("RefreshToken", refreshToken ?? "");
        }
        catch (KeyNotFoundException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoginWindow] Missing property in login response: {ex.Message}");
        }
    }

    // ── SESSION ───────────────────────────────────────────

    private static void SaveSession()
    {
        SaveSetting("AccessToken", SessionState.AccessToken ?? "");
        SaveSetting("UserEmail", SessionState.UserEmail ?? "");
        SaveSetting("UserRole", SessionState.UserRole ?? "");
        SaveSetting("UserTier", SessionState.UserTier ?? "free");
        SaveSetting("UserId", SessionState.UserId ?? "");
    }

    private static void ClearSavedSession()
    {
        SaveSetting("AccessToken", "");
        SaveSetting("RefreshToken", "");
        SaveSetting("UserEmail", "");
        SaveSetting("UserRole", "");
        SaveSetting("UserTier", "");
        SaveSetting("UserId", "");
    }

    // ── NAVIGATE ──────────────────────────────────────────

    private void OpenMainWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var main = new MainWindow();
            main.Show();
            this.Close();
        });
    }

    // ── UI EVENTS ─────────────────────────────────────────

    private async void Login_Click(object? sender, RoutedEventArgs e)
    {
        var email = LoginEmail.Text?.Trim() ?? "";
        var pw = LoginPassword.Text ?? "";
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pw))
        { SetStatus("Enter email and password", true); return; }
        await AuthenticateAsync("login", email, pw);
    }

    private async void Register_Click(object? sender, RoutedEventArgs e)
    {
        var email = RegEmail.Text?.Trim() ?? "";
        var pw = RegPassword.Text ?? "";
        var confirm = RegConfirm.Text ?? "";
        if (string.IsNullOrEmpty(email) || pw.Length < 6)
        { SetStatus("Email and password (min 6 chars) required", true); return; }
        if (pw != confirm)
        { SetStatus("Passwords do not match", true); return; }
        await AuthenticateAsync("register", email, pw);
    }

    private void LoginTab_Click(object? sender, RoutedEventArgs e)
    {
        LoginForm.IsVisible = true; RegisterForm.IsVisible = false;
        LoginTab.Background = new SolidColorBrush(Color.Parse("#1500D4AA"));
        LoginTab.Foreground = new SolidColorBrush(Color.Parse("#00D4AA"));
        RegisterTab.Background = Brushes.Transparent;
        RegisterTab.Foreground = new SolidColorBrush(Color.Parse("#8888A0"));
    }

    private void RegisterTab_Click(object? sender, RoutedEventArgs e)
    {
        LoginForm.IsVisible = false; RegisterForm.IsVisible = true;
        RegisterTab.Background = new SolidColorBrush(Color.Parse("#1500D4AA"));
        RegisterTab.Foreground = new SolidColorBrush(Color.Parse("#00D4AA"));
        LoginTab.Background = Brushes.Transparent;
        LoginTab.Foreground = new SolidColorBrush(Color.Parse("#8888A0"));
    }

    private void Forgot_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://auracore.pro/forgot-password",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void Offline_Click(object? sender, RoutedEventArgs e)
    {
        // Offline mode = free tier, no auth
        SessionState.AccessToken = null;
        SessionState.UserEmail = null;
        SessionState.UserTier = "free";
        SessionState.UserRole = null;
        OpenMainWindow();
    }

    // ── 2FA DIALOG ────────────────────────────────────────

    private async Task<string?> Show2faDialogAsync()
    {
        var tcs = new TaskCompletionSource<string?>();

        Dispatcher.UIThread.Post(() =>
        {
            // Simple inline 2FA - replace login form temporarily
            LoginForm.IsVisible = false;
            RegisterForm.IsVisible = false;
            StatusText.Text = "";

            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock
            {
                Text = "Two-Factor Authentication", FontSize = 16, FontWeight = global::Avalonia.Media.FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")), TextAlignment = TextAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Enter the 6-digit code from your authenticator app",
                FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#8888A0")),
                TextAlignment = TextAlignment.Center
            });

            var codeBox = new TextBox
            {
                Watermark = "000000", MaxLength = 6, FontSize = 24,
                Background = new SolidColorBrush(Color.Parse("#1A1A28")),
                Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")),
                BorderBrush = new SolidColorBrush(Color.Parse("#33334A")),
                CornerRadius = new global::Avalonia.CornerRadius(8), Padding = new global::Avalonia.Thickness(12, 10),
                TextAlignment = TextAlignment.Center, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                MinWidth = 200
            };
            panel.Children.Add(codeBox);

            var btnRow = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center
            };
            var verifyBtn = new Button
            {
                Content = "Verify", Background = new SolidColorBrush(Color.Parse("#00D4AA")),
                Foreground = new SolidColorBrush(Color.Parse("#0A0A0F")),
                FontWeight = global::Avalonia.Media.FontWeight.Bold, Padding = new global::Avalonia.Thickness(20, 10),
                CornerRadius = new global::Avalonia.CornerRadius(8)
            };
            var cancelBtn = new Button
            {
                Content = "Cancel", Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.Parse("#8888A0")),
                Padding = new global::Avalonia.Thickness(20, 10), CornerRadius = new global::Avalonia.CornerRadius(8),
                BorderBrush = new SolidColorBrush(Color.Parse("#2A2A3A")), BorderThickness = new global::Avalonia.Thickness(1)
            };
            verifyBtn.Click += (s, e) => { tcs.TrySetResult(codeBox.Text?.Trim()); };
            cancelBtn.Click += (s, e) => { tcs.TrySetResult(null); LoginForm.IsVisible = true; };
            btnRow.Children.Add(verifyBtn);
            btnRow.Children.Add(cancelBtn);
            panel.Children.Add(btnRow);

            // Replace content temporarily - using Tag as temp holder
            LoginForm.Tag = panel;
            // Insert panel where LoginForm is
            if (LoginForm.Parent is Grid grid)
            {
                Grid.SetRow(panel, 1);
                grid.Children.Add(panel);
            }
        });

        return await tcs.Task;
    }

    // ── HELPERS ───────────────────────────────────────────

    private void SetStatus(string text, bool isError)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText.Text = text;
            StatusText.Foreground = new SolidColorBrush(Color.Parse(isError ? "#EF4444" : "#8888A0"));
        });
    }

    private void SetLoading(bool on)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LoginBtn.IsEnabled = !on;
            RegisterBtn.IsEnabled = !on;
            LoginBtn.Content = on ? "Signing in..." : "Sign In";
        });
    }

    // ── SETTINGS PERSISTENCE ──────────────────────────────

    private static Dictionary<string, string>? _settingsCache;
    private static readonly object _settingsLock = new();

    private static Dictionary<string, string> LoadSettings()
    {
        lock (_settingsLock)
        {
            if (_settingsCache != null) return _settingsCache;
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    _settingsCache = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                    return _settingsCache;
                }
            }
            catch { }
            _settingsCache = new();
            return _settingsCache;
        }
    }

    public static string LoadSetting(string key)
    {
        lock (_settingsLock)
        {
            var settings = LoadSettings();
            return settings.TryGetValue(key, out var val) ? val : "";
        }
    }

    public static void SaveSetting(string key, string value)
    {
        lock (_settingsLock)
        {
            var settings = LoadSettings();
            settings[key] = value;
            try
            {
                Directory.CreateDirectory(SettingsDir);
                File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings));
            }
            catch { }
        }
    }
}
