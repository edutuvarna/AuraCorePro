using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Threading;
using AuraCore.Application;
using AuraCore.UI.Avalonia.Views;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class PaymentView : UserControl
{
    private string _tier = "pro";
    private string _plan = "monthly";
    private string? _checkoutUrl;
    private volatile bool _polling;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>Action to call when user wants to go back (set by caller)</summary>
    public Action? GoBackAction { get; set; }

    /// <summary>Action to call after successful payment (set by caller)</summary>
    public Action? PaymentSuccessAction { get; set; }

    public PaymentView() : this("pro", "monthly") { }

    public PaymentView(string tier, string plan)
    {
        _tier = tier;
        _plan = plan;
        InitializeComponent();
        Loaded += async (s, e) => await StartCheckout();
        Unloaded += (s, e) => _polling = false;
    }

    private async Task StartCheckout()
    {
        ShowScreen("loading");
        LoadingText.Text = LocalizationService._("pay.creatingSession");

        try
        {
            _checkoutUrl = await GetCheckoutUrlAsync();

            if (string.IsNullOrEmpty(_checkoutUrl))
            {
                ShowError(LocalizationService._("pay.failedSession"));
                return;
            }

            // Open Stripe checkout in default browser
            LoadingText.Text = LocalizationService._("pay.openingBrowser");
            Process.Start(new ProcessStartInfo(_checkoutUrl) { UseShellExecute = true });

            // Show waiting screen and start polling
            ShowScreen("waiting");
            _polling = true;
            _ = PollForPaymentAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
        }
    }

    private async Task<string?> GetCheckoutUrlAsync()
    {
        try
        {
            var apiUrl = LoginWindow.ApiBaseUrl;
            var token = SessionState.AccessToken;

            HttpRequestMessage request;

            if (!string.IsNullOrEmpty(token))
            {
                request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/api/payment/stripe/create-session")
                {
                    Content = JsonContent.Create(new { tier = _tier, plan = _plan, deviceCount = 1 })
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/api/payment/checkout")
                {
                    Content = JsonContent.Create(new { tier = _tier, plan = _plan, deviceCount = 1, email = SessionState.UserEmail })
                };
            }

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
        }
        catch { return null; }
    }

    private async Task PollForPaymentAsync()
    {
        var attempts = 0;
        const int maxAttempts = 120; // 10 min

        while (_polling && attempts < maxAttempts)
        {
            attempts++;
            await Task.Delay(5000);
            if (!_polling) return;

            try
            {
                Dispatcher.UIThread.Post(() =>
                    PollStatusText.Text = $"Waiting for payment... ({attempts * 5}s)");

                var token = SessionState.AccessToken;
                if (string.IsNullOrEmpty(token)) continue;

                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{LoginWindow.ApiBaseUrl}/api/license/validate?key=self&device=self");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await Http.SendAsync(request);
                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("tier", out var tierProp))
                {
                    var currentTier = tierProp.GetString() ?? "free";
                    if (currentTier != "free")
                    {
                        _polling = false;
                        SessionState.UserTier = currentTier;
                        LoginWindow.SaveSetting("UserTier", currentTier);

                        Dispatcher.UIThread.Post(() => HandlePaymentSuccess(currentTier));
                        return;
                    }
                }
            }
            catch { /* continue polling */ }
        }

        if (_polling)
        {
            _polling = false;
            Dispatcher.UIThread.Post(() =>
                PollStatusText.Text = LocalizationService._("pay.paymentTimeout"));
        }
    }

    private void HandlePaymentSuccess(string tier)
    {
        var tierName = tier == "enterprise" ? "Enterprise" : "Pro";
        ShowScreen("success");
        SuccessMessage.Text = $"You are now an AuraCore {tierName} member!";
        TierUpdateText.Text = $"Your account has been upgraded to {tierName}. All features are now unlocked.";
    }

    private void ShowScreen(string screen)
    {
        LoadingScreen.IsVisible = screen == "loading";
        WaitingScreen.IsVisible = screen == "waiting";
        SuccessScreen.IsVisible = screen == "success";
        ErrorScreen.IsVisible = screen == "error";
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ShowScreen("error");
    }

    private void Reopen_Click(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_checkoutUrl))
        {
            try { Process.Start(new ProcessStartInfo(_checkoutUrl) { UseShellExecute = true }); }
            catch { }
        }
    }

    private async void TryAgain_Click(object? sender, RoutedEventArgs e) => await StartCheckout();

    private void Back_Click(object? sender, RoutedEventArgs e)
    {
        _polling = false;
        GoBackAction?.Invoke();
    }

    private void Continue_Click(object? sender, RoutedEventArgs e)
    {
        PaymentSuccessAction?.Invoke();
    }
}
