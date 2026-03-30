using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using AuraCore.Application;
using AuraCore.UI.Avalonia.Views;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class AdminPanelView : UserControl
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public AdminPanelView()
    {
        InitializeComponent();
        Loaded += async (s, e) => await CheckHealthAsync();
    }

    private void OpenAdmin_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var url = "https://admin.auracore.pro";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) { Debug.WriteLine($"Open browser error: {ex.Message}"); }
    }

    private async void RefreshStatus_Click(object? sender, RoutedEventArgs e)
    {
        RefreshStatusBtn.IsEnabled = false;
        await CheckHealthAsync();
        RefreshStatusBtn.IsEnabled = true;
    }

    private async Task CheckHealthAsync()
    {
        try
        {
            ApiStatusText.Text = "Checking...";
            var url = $"{LoginWindow.ApiBaseUrl}/api/health";
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrEmpty(SessionState.AccessToken))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SessionState.AccessToken);

            var response = await Http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                ApiStatusText.Text = "Online";
                ApiStatusText.Foreground = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#22C55E"));

                if (root.TryGetProperty("version", out var ver)) ApiVersionText.Text = ver.GetString() ?? "--";
                if (root.TryGetProperty("uptime", out var up)) UptimeText.Text = up.GetString() ?? "--";
                if (root.TryGetProperty("memoryMB", out var mem)) MemoryText.Text = $"{mem.GetInt32()} MB";
                if (root.TryGetProperty("environment", out var env)) EnvText.Text = env.GetString() ?? "--";
            }
            else
            {
                ApiStatusText.Text = $"Error ({(int)response.StatusCode})";
                ApiStatusText.Foreground = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#EF4444"));
            }
        }
        catch (Exception ex)
        {
            ApiStatusText.Text = "Offline";
            ApiStatusText.Foreground = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#EF4444"));
            Debug.WriteLine($"Health check error: {ex.Message}");
        }
    }
}
