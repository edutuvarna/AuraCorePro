using AuraCore.Application;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;

namespace AuraCore.Desktop.Pages;

public sealed partial class PaymentPage : Page
{
    private string _tier = "pro";
    private string _plan = "monthly";
    private int _deviceCount = 1;
    private static readonly HttpClient Http = new();

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

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadingText.Text = "Creating checkout session...";
            await PaymentWebView.EnsureCoreWebView2Async();

            // Get Stripe Checkout URL from API
            var url = await GetCheckoutUrlAsync();
            if (string.IsNullOrEmpty(url))
            {
                LoadingText.Text = "Failed to create checkout session. Please try again.";
                return;
            }

            LoadingText.Text = "Loading Stripe checkout...";
            PaymentWebView.Source = new Uri(url);
        }
        catch (Exception ex)
        {
            LoadingText.Text = $"Error: {ex.Message}";
        }
    }

    private async Task<string?> GetCheckoutUrlAsync()
    {
        try
        {
            var apiUrl = LoginWindow.ApiBaseUrl;
            var token = LoginWindow.AccessToken;

            HttpRequestMessage request;

            // If user is logged in, use authenticated endpoint
            if (!string.IsNullOrEmpty(token))
            {
                request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/api/payment/stripe/create-session")
                {
                    Content = JsonContent.Create(new { tier = _tier, plan = _plan, deviceCount = _deviceCount })
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                // Use public checkout endpoint
                request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/api/payment/checkout")
                {
                    Content = JsonContent.Create(new { tier = _tier, plan = _plan, deviceCount = _deviceCount })
                };
            }

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("url", out var urlProp))
                return urlProp.GetString();

            return null;
        }
        catch
        {
            return null;
        }
    }

    private void PaymentWebView_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        var uri = args.Uri;

        // Check if redirected to success URL
        if (uri.Contains("payment=success"))
        {
            args.Cancel = true;
            _ = HandlePaymentSuccessAsync();
        }
        // Check if redirected to cancel URL
        else if (uri.Contains("payment=cancelled"))
        {
            args.Cancel = true;
            GoBack();
        }
    }

    private void PaymentWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        // Hide loading overlay once Stripe page loads
        LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    private async Task HandlePaymentSuccessAsync()
    {
        // Show success overlay
        PaymentWebView.Visibility = Visibility.Collapsed;
        LoadingOverlay.Visibility = Visibility.Collapsed;
        SuccessOverlay.Visibility = Visibility.Visible;

        var tierName = _tier == "enterprise" ? "Enterprise" : "Pro";
        SuccessMessage.Text = $"Your {tierName} subscription is now active.\nAll premium features have been unlocked!";
        TierUpdateText.Text = "Refreshing your account...";

        // Refresh tier from API
        try
        {
            await RefreshTierAsync();
            TierUpdateText.Text = $"Account updated — you are now {tierName}!";
        }
        catch
        {
            TierUpdateText.Text = "Account will update on next login.";
        }
    }

    private async Task RefreshTierAsync()
    {
        try
        {
            var apiUrl = LoginWindow.ApiBaseUrl;
            var token = LoginWindow.AccessToken;

            if (string.IsNullOrEmpty(token)) return;

            var request = new HttpRequestMessage(HttpMethod.Get, $"{apiUrl}/api/license/validate?key=self&device=self");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("tier", out var tierProp))
            {
                var newTier = tierProp.GetString() ?? "free";
                LoginWindow.UserTier = newTier;
                SessionState.UserTier = newTier;
            }
        }
        catch { /* Will update on next login */ }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        GoBack();
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        // Navigate back to dashboard — MainWindow will refresh tier badge and feature locking
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
