using AuraCore.Application;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AuraCore.Desktop.Pages;

public sealed partial class PaymentPage : Page
{
    private string _tier = "pro";
    private string _plan = "monthly";
    private int _deviceCount = 1;
    private string? _checkoutUrl;
    private bool _polling;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public PaymentPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is PaymentParams p)
        {
            _tier = p.Tier;
            _plan = p.Plan;
            _deviceCount = p.DeviceCount;
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _polling = false; // Stop polling when leaving page
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await StartCheckout();
    }

    private async Task StartCheckout()
    {
        ShowScreen("loading");
        LoadingText.Text = AuraCore.Desktop.Services.S._("pay.creatingSession");

        try
        {
            _checkoutUrl = await GetCheckoutUrlAsync();

            if (string.IsNullOrEmpty(_checkoutUrl))
            {
                ShowError(AuraCore.Desktop.Services.S._("pay.failedSession"));
                return;
            }

            // Open Stripe checkout in default browser
            LoadingText.Text = AuraCore.Desktop.Services.S._("pay.openingBrowser");
            await Windows.System.Launcher.LaunchUriAsync(new Uri(_checkoutUrl));

            // Show waiting screen and start polling for payment completion
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
            var token = LoginWindow.AccessToken;
            var email = LoginWindow.UserEmail;

            HttpRequestMessage request;

            if (!string.IsNullOrEmpty(token))
            {
                // Authenticated checkout — uses user's email automatically
                request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/api/payment/stripe/create-session")
                {
                    Content = JsonContent.Create(new { tier = _tier, plan = _plan, deviceCount = _deviceCount })
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                // Public checkout — pass email if available
                request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/api/payment/checkout")
                {
                    Content = JsonContent.Create(new { tier = _tier, plan = _plan, deviceCount = _deviceCount, email })
                };
            }

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Polls the license API every 5 seconds to detect when payment completes.
    /// When Stripe webhook fires, the user's tier changes from "free" to "pro/enterprise".
    /// </summary>
    private async Task PollForPaymentAsync()
    {
        var attempts = 0;
        var maxAttempts = 120; // 10 minutes max (120 * 5 seconds)

        while (_polling && attempts < maxAttempts)
        {
            attempts++;
            await Task.Delay(5000); // Check every 5 seconds

            if (!_polling) return;

            try
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    PollStatusText.Text = string.Format(AuraCore.Desktop.Services.S._("pay.waitingPayment"), attempts * 5);
                });

                var apiUrl = LoginWindow.ApiBaseUrl;
                var token = LoginWindow.AccessToken;

                if (string.IsNullOrEmpty(token)) continue;

                var request = new HttpRequestMessage(HttpMethod.Get, $"{apiUrl}/api/license/validate?key=self&device=self");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await Http.SendAsync(request);
                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("tier", out var tierProp))
                {
                    var currentTier = tierProp.GetString() ?? "free";

                    // Payment detected! Tier changed from free
                    if (currentTier != "free")
                    {
                        _polling = false;
                        LoginWindow.UserTier = currentTier;
                        SessionState.UserTier = currentTier;

                        DispatcherQueue?.TryEnqueue(() => HandlePaymentSuccess(currentTier));
                        return;
                    }
                }
            }
            catch { /* Continue polling */ }
        }

        // Timeout — stop polling
        if (_polling)
        {
            _polling = false;
            DispatcherQueue?.TryEnqueue(() =>
            {
                PollStatusText.Text = AuraCore.Desktop.Services.S._("pay.paymentTimeout");
            });
        }
    }

    private void HandlePaymentSuccess(string tier)
    {
        var tierName = tier == "enterprise" ? "Enterprise" : "Pro";

        ShowScreen("success");
        SuccessMessage.Text = string.Format(AuraCore.Desktop.Services.S._("pay.successMsg"), tierName);
        TierUpdateText.Text = string.Format(AuraCore.Desktop.Services.S._("pay.tierUpdated"), tierName);
    }

    private void ShowScreen(string screen)
    {
        LoadingScreen.Visibility = screen == "loading" ? Visibility.Visible : Visibility.Collapsed;
        WaitingScreen.Visibility = screen == "waiting" ? Visibility.Visible : Visibility.Collapsed;
        SuccessScreen.Visibility = screen == "success" ? Visibility.Visible : Visibility.Collapsed;
        ErrorScreen.Visibility = screen == "error" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ShowScreen("error");
    }

    private async void Reopen_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_checkoutUrl))
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(_checkoutUrl));
        }
    }

    private async void TryAgain_Click(object sender, RoutedEventArgs e)
    {
        await StartCheckout();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        _polling = false;
        GoBack();
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
            Frame.GoBack();
        else
            Frame.Navigate(typeof(DashboardPage));

        // Force MainWindow to refresh tier UI
        if (App.MainWindow is MainWindow mw)
        {
            mw.RefreshAfterPayment();
        }
    }

    private void GoBack()
    {
        if (Frame.CanGoBack)
            Frame.GoBack();
        else
            Frame.Navigate(typeof(UpgradePage));
    }
}

/// <summary>Navigation parameter for PaymentPage</summary>
public sealed record PaymentParams(string Tier, string Plan, int DeviceCount);
