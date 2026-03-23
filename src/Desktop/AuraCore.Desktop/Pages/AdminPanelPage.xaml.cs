using AuraCore.Desktop.Services;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace AuraCore.Desktop.Pages;

public sealed partial class AdminPanelPage : Page
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public AdminPanelPage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        ServerUrlBox.Text = LoginWindow.ApiBaseUrl;
        SetAuth();
        Loaded += async (s, e) =>
        {
            await LoadStatsAsync();
            await LoadUsersAsync(null);
            await LoadRecentPaymentsAsync();
        };
    }

    private static void SetAuth()
    {
        if (LoginWindow.AccessToken is not null)
            Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LoginWindow.AccessToken);
    }

    // ── Server Config + Health ──

    private void SaveServerUrl_Click(object sender, RoutedEventArgs e)
    {
        var url = ServerUrlBox.Text.TrimEnd('/');
        LoginWindow.ApiBaseUrl = url;
        LoginWindow.SaveSetting("ApiBaseUrl", url);
        ServerStatus.Text = "Saved!";
    }

    private async void HealthCheck_Click(object sender, RoutedEventArgs e)
    {
        HealthCheckBtn.IsEnabled = false;
        HealthProgress.IsActive = true; HealthProgress.Visibility = Visibility.Visible;
        HealthPanel.Visibility = Visibility.Collapsed;

        try
        {
            var sw = Stopwatch.StartNew();
            var resp = await Http.GetAsync($"{LoginWindow.ApiBaseUrl}/health");
            sw.Stop();

            var isOk = resp.IsSuccessStatusCode;
            ApiDot.Background = new SolidColorBrush(isOk
                ? Windows.UI.Color.FromArgb(255, 76, 175, 80)
                : Windows.UI.Color.FromArgb(255, 244, 67, 54));
            ApiHealthText.Text = isOk ? "API: Online" : $"API: {resp.StatusCode}";
            HealthLatencyText.Text = $"Response: {sw.ElapsedMilliseconds}ms";
            HealthPanel.Visibility = Visibility.Visible;

            if (isOk)
            {
                try
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("database", out var dbProp))
                    {
                        var dbOk = dbProp.GetString() == "connected";
                        ApiHealthText.Text += dbOk ? "  |  DB: Connected" : "  |  DB: Error";
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            ApiDot.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 67, 54));
            ApiHealthText.Text = $"API: Offline — {ex.Message}";
            HealthPanel.Visibility = Visibility.Visible;
        }
        finally
        {
            HealthCheckBtn.IsEnabled = true;
            HealthProgress.IsActive = false; HealthProgress.Visibility = Visibility.Collapsed;
        }
    }

    // ── Stats ──

    private async void RefreshStats_Click(object sender, RoutedEventArgs e) => await LoadStatsAsync();

    private async Task LoadStatsAsync()
    {
        StatsProgress.IsActive = true; StatsProgress.Visibility = Visibility.Visible;
        try
        {
            var resp = await Http.GetAsync($"{LoginWindow.ApiBaseUrl}/api/admin/dashboard/stats");
            if (!resp.IsSuccessStatusCode) return;
            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var r = doc.RootElement;
            var total = GetInt(r, "totalUsers");
            var pro = GetInt(r, "proUsers");
            var enterprise = GetInt(r, "enterpriseUsers");
            var revenue = GetDecimal(r, "totalRevenue");
            var pending = GetInt(r, "pendingCryptoPayments");

            StatUsers.Text = total.ToString();
            StatPro.Text = pro.ToString();
            StatEnterprise.Text = enterprise.ToString();
            StatRevenue.Text = $"${revenue:F2}";
            StatPending.Text = pending.ToString();

            var paid = pro + enterprise;
            StatConversion.Text = total > 0 ? $"{(double)paid / total * 100:F1}%" : "0%";
        }
        catch (Exception ex) { ServerStatus.Text = ex.Message; }
        finally { StatsProgress.IsActive = false; StatsProgress.Visibility = Visibility.Collapsed; }
    }

    // ── Recent Payments ──

    private async void RefreshPayments_Click(object sender, RoutedEventArgs e) => await LoadRecentPaymentsAsync();

    private async Task LoadRecentPaymentsAsync()
    {
        PaymentsProgress.IsActive = true; PaymentsProgress.Visibility = Visibility.Visible;
        PaymentsList.Children.Clear();

        try
        {
            var resp = await Http.GetAsync($"{LoginWindow.ApiBaseUrl}/api/admin/dashboard/recent-payments?count=10");
            if (!resp.IsSuccessStatusCode)
            {
                PaymentsStatus.Text = $"Failed: {resp.StatusCode}";
                return;
            }

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var payments = doc.RootElement;

            // Header
            var header = new Grid { ColumnSpacing = 8, Padding = new Thickness(12, 8, 12, 8),
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(6) };
            AddPaymentColumns(header);
            AddTextToGrid(header, "Email", 0, true); AddTextToGrid(header, "Amount", 1, true);
            AddTextToGrid(header, "Method", 2, true); AddTextToGrid(header, "Status", 3, true);
            AddTextToGrid(header, "Date", 4, true);
            PaymentsList.Children.Add(header);

            var items = payments.ValueKind == JsonValueKind.Array ? payments : (payments.TryGetProperty("payments", out var p) ? p : payments);
            int count = 0;
            foreach (var pay in items.EnumerateArray())
            {
                var row = new Grid { ColumnSpacing = 8, Padding = new Thickness(12, 6, 12, 6),
                    BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(0, 0, 0, 0.5) };
                AddPaymentColumns(row);

                AddTextToGrid(row, pay.TryGetProperty("userEmail", out var ue) ? ue.GetString() ?? "" : "", 0);
                AddTextToGrid(row, pay.TryGetProperty("amount", out var am) ? $"${am.GetDecimal():F2}" : "", 1);

                var method = pay.TryGetProperty("method", out var me) ? me.GetString() ?? "" : "";
                AddTextToGrid(row, method, 2);

                var status = pay.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
                var statusColor = status switch
                {
                    "completed" or "succeeded" => Windows.UI.Color.FromArgb(255, 46, 125, 50),
                    "pending" => Windows.UI.Color.FromArgb(255, 230, 81, 0),
                    _ => Windows.UI.Color.FromArgb(255, 158, 158, 158)
                };
                var badge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, statusColor.R, statusColor.G, statusColor.B)),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center
                };
                badge.Child = new TextBlock { Text = status.ToUpper(), FontSize = 9,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(statusColor) };
                Grid.SetColumn(badge, 3); row.Children.Add(badge);

                var date = pay.TryGetProperty("createdAt", out var da) ? da.GetDateTimeOffset().ToString("MMM dd, HH:mm") : "";
                AddTextToGrid(row, date, 4);

                PaymentsList.Children.Add(row);
                count++;
            }
            PaymentsStatus.Text = $"{count} recent payment(s)";
        }
        catch (Exception ex) { PaymentsStatus.Text = $"Error: {ex.Message}"; }
        finally { PaymentsProgress.IsActive = false; PaymentsProgress.Visibility = Visibility.Collapsed; }
    }

    private static void AddPaymentColumns(Grid g)
    {
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
    }

    private static void AddTextToGrid(Grid g, string text, int col, bool header = false)
    {
        var tb = new TextBlock { Text = text, FontSize = header ? 11 : 12,
            FontWeight = header ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center, Opacity = header ? 0.7 : 1 };
        Grid.SetColumn(tb, col); g.Children.Add(tb);
    }

    // ── User List ──

    private void UserSearch_KeyDown(object sender, KeyRoutedEventArgs e)
    { if (e.Key == Windows.System.VirtualKey.Enter) UserSearch_Click(sender, e); }

    private async void UserSearch_Click(object sender, RoutedEventArgs e)
    { var s = UserSearchBox.Text.Trim(); await LoadUsersAsync(string.IsNullOrEmpty(s) ? null : s); }

    private async void ShowAllUsers_Click(object sender, RoutedEventArgs e)
    { UserSearchBox.Text = ""; await LoadUsersAsync(null); }

    private async Task LoadUsersAsync(string? search)
    {
        UserProgress.IsActive = true; UserProgress.Visibility = Visibility.Visible;
        UserList.Children.Clear();

        try
        {
            var url = $"{LoginWindow.ApiBaseUrl}/api/admin/users?pageSize=100";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
            var resp = await Http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) { UserCountText.Text = $"Error: {resp.StatusCode}"; return; }

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            var total = GetInt(root, "total");
            if (!root.TryGetProperty("users", out var users)) return;

            // Header
            var header = CreateUserRow("Email", "Role", "Tier", "Created", null, isHeader: true);
            UserList.Children.Add(header);

            foreach (var u in users.EnumerateArray())
            {
                var email = u.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "";
                var role = u.TryGetProperty("role", out var ro) ? ro.GetString() ?? "user" : "user";
                var created = u.TryGetProperty("createdAt", out var ca) ? ca.GetDateTimeOffset().ToString("yyyy-MM-dd") : "";
                var userId = u.TryGetProperty("id", out var uid) ? uid.GetString() : null;

                var tier = "free";
                if (u.TryGetProperty("license", out var lic) && lic.ValueKind != JsonValueKind.Null)
                    if (lic.TryGetProperty("tier", out var ti)) tier = ti.GetString() ?? "free";
                if (role == "admin") tier = "admin";

                UserList.Children.Add(CreateUserRow(email, role, tier, created, userId));
            }

            UserCountText.Text = $"{total} user(s) found";
        }
        catch (Exception ex) { UserCountText.Text = $"Error: {ex.Message}"; }
        finally { UserProgress.IsActive = false; UserProgress.Visibility = Visibility.Collapsed; }
    }

    private Grid CreateUserRow(string email, string role, string tier, string created, string? userId, bool isHeader = false)
    {
        var grid = new Grid
        {
            ColumnSpacing = 8,
            Padding = new Thickness(12, isHeader ? 8 : 6, 12, isHeader ? 8 : 6),
            Background = isHeader ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"] : null,
            CornerRadius = isHeader ? new CornerRadius(6) : new CornerRadius(0),
            BorderBrush = isHeader ? null : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = isHeader ? new Thickness(0) : new Thickness(0, 0, 0, 0.5)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var weight = isHeader ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        var sz = isHeader ? 12 : 13;

        var emailTb = new TextBlock { Text = email, FontSize = sz, FontWeight = weight, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(emailTb, 0); grid.Children.Add(emailTb);

        var roleTb = new TextBlock { Text = role, FontSize = sz, FontWeight = weight, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
        Grid.SetColumn(roleTb, 1); grid.Children.Add(roleTb);

        if (!isHeader)
        {
            var tierColor = tier switch
            {
                "admin" => Windows.UI.Color.FromArgb(255, 198, 40, 40),
                "enterprise" => Windows.UI.Color.FromArgb(255, 106, 27, 154),
                "pro" => Windows.UI.Color.FromArgb(255, 21, 101, 192),
                _ => Windows.UI.Color.FromArgb(255, 96, 125, 139)
            };
            var badge = new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, tierColor.R, tierColor.G, tierColor.B)),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock { Text = tier.ToUpper(), FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(tierColor) };
            Grid.SetColumn(badge, 2); grid.Children.Add(badge);

            // Delete button (inline)
            if (role != "admin" && userId is not null)
            {
                var deleteBtn = new Button { Content = "Delete", Padding = new Thickness(8, 2, 8, 2), FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center };
                var capturedEmail = email;
                var capturedId = userId;
                deleteBtn.Click += async (s, ev) =>
                {
                    var dlg = new ContentDialog
                    {
                        Title = $"Delete {capturedEmail}?",
                        Content = "This will permanently remove the user and all associated data. This action cannot be undone.",
                        PrimaryButtonText = "Delete",
                        CloseButtonText = "Cancel",
                        XamlRoot = this.XamlRoot,
                        DefaultButton = ContentDialogButton.Close
                    };
                    if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
                    try
                    {
                        var resp = await Http.DeleteAsync($"{LoginWindow.ApiBaseUrl}/api/admin/users/{capturedId}");
                        if (resp.IsSuccessStatusCode)
                        {
                            UserCountText.Text = $"Deleted {capturedEmail}";
                            await LoadUsersAsync(null);
                        }
                        else UserCountText.Text = $"Failed: {resp.StatusCode}";
                    }
                    catch (Exception ex) { UserCountText.Text = ex.Message; }
                };
                Grid.SetColumn(deleteBtn, 4); grid.Children.Add(deleteBtn);
            }
        }
        else
        {
            var tierH = new TextBlock { Text = "Tier", FontSize = sz, FontWeight = weight, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tierH, 2); grid.Children.Add(tierH);
            var actH = new TextBlock { Text = "Actions", FontSize = sz, FontWeight = weight, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(actH, 4); grid.Children.Add(actH);
        }

        var createdTb = new TextBlock { Text = created, FontSize = sz, FontWeight = weight, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.5 };
        Grid.SetColumn(createdTb, 3); grid.Children.Add(createdTb);

        return grid;
    }

    // ── Grant Sub ──

    private async void GrantSub_Click(object sender, RoutedEventArgs e)
    {
        var email = GrantEmailBox.Text.Trim();
        if (string.IsNullOrEmpty(email)) { GrantStatus.Text = "Enter an email."; return; }
        var tier = (GrantTierBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "pro";
        var days = int.Parse((GrantDaysBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "30");

        GrantStatus.Text = "Looking up user...";
        try
        {
            var userId = await FindUserIdByEmailAsync(email);
            if (userId is null) { GrantStatus.Text = "User not found."; return; }

            var resp = await Http.PostAsJsonAsync(
                $"{LoginWindow.ApiBaseUrl}/api/admin/subscriptions/grant",
                new { userId = Guid.Parse(userId), tier, days });

            GrantStatus.Text = resp.IsSuccessStatusCode
                ? $"Granted {tier.ToUpper()} for {days} days to {email}. User must re-login to see changes."
                : $"Failed: {resp.StatusCode}";
            await LoadUsersAsync(null);
        }
        catch (Exception ex) { GrantStatus.Text = ex.Message; }
    }

    // ── Revoke Sub ──

    private async void RevokeSub_Click(object sender, RoutedEventArgs e)
    {
        var email = RevokeEmailBox.Text.Trim();
        if (string.IsNullOrEmpty(email)) { RevokeStatus.Text = "Enter an email."; return; }

        var dlg = new ContentDialog
        {
            Title = $"Revoke subscription for {email}?",
            Content = "The user will be downgraded to Free tier immediately.",
            PrimaryButtonText = "Revoke",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Close
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        RevokeStatus.Text = "Revoking...";
        try
        {
            var userId = await FindUserIdByEmailAsync(email);
            if (userId is null) { RevokeStatus.Text = "User not found."; return; }

            var resp = await Http.PostAsync(
                $"{LoginWindow.ApiBaseUrl}/api/admin/subscriptions/revoke/{userId}",
                null);

            RevokeStatus.Text = resp.IsSuccessStatusCode
                ? $"Subscription revoked for {email}"
                : $"Failed: {resp.StatusCode}";
            await LoadUsersAsync(null);
        }
        catch (Exception ex) { RevokeStatus.Text = ex.Message; }
    }

    // ── Password Reset ──

    private async void ResetPassword_Click(object sender, RoutedEventArgs e)
    {
        var email = ResetEmailBox.Text.Trim();
        var newPw = ResetPasswordBox.Password;
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(newPw))
        { ResetStatus.Text = "Enter email and new password."; return; }
        if (newPw.Length < 8) { ResetStatus.Text = "Password must be at least 8 characters."; return; }

        var dialog = new ContentDialog
        {
            Title = "Reset password?",
            Content = $"This will change the password for {email}.\nThe user will need to log in with the new password.",
            PrimaryButtonText = "Reset", CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot, DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            var resp = await Http.PostAsJsonAsync(
                $"{LoginWindow.ApiBaseUrl}/api/admin/users/reset-password",
                new { email, newPassword = newPw });
            ResetStatus.Text = resp.IsSuccessStatusCode ? $"Password reset for {email}" : $"Failed: {resp.StatusCode}";
        }
        catch (Exception ex) { ResetStatus.Text = ex.Message; }
    }

    // ── Helpers ──

    private static async Task<string?> FindUserIdByEmailAsync(string email)
    {
        var resp = await Http.GetAsync($"{LoginWindow.ApiBaseUrl}/api/admin/users?search={Uri.EscapeDataString(email)}");
        if (!resp.IsSuccessStatusCode) return null;
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        foreach (var u in doc.RootElement.GetProperty("users").EnumerateArray())
        {
            var e = u.TryGetProperty("email", out var ep) ? ep.GetString() : "";
            if (string.Equals(e, email, StringComparison.OrdinalIgnoreCase))
                return u.GetProperty("id").GetString();
        }
        return null;
    }

    private static int GetInt(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var i) ? i : 0;
    private static decimal GetDecimal(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.TryGetDecimal(out var d) ? d : 0;

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("admin.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("admin.subtitle");
    }
}
