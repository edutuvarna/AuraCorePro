using AuraCore.Desktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuraCore.Desktop.Pages;

public sealed partial class UpgradePage : Page
{
    private static readonly HttpClient Http = new();

    public UpgradePage() { InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization); }

    private async void UpgradePro_Click(object sender, RoutedEventArgs e) => await ShowPlanDialog("pro");
    private async void UpgradeEnterprise_Click(object sender, RoutedEventArgs e) => await ShowPlanDialog("enterprise");

    private async Task ShowPlanDialog(string tier)
    {
        var tierName = tier == "enterprise" ? "Enterprise" : "Pro";
        var monthlyPrice = tier == "enterprise" ? "$12.99" : "$4.99";
        var yearlyPrice = tier == "enterprise" ? "$129.99" : "$49.99";

        var panel = new StackPanel { Spacing = 12 };

        panel.Children.Add(new TextBlock { Text = $"Choose your {tierName} billing plan:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        var monthlyBtn = new RadioButton { Content = $"Monthly — {monthlyPrice}/month", IsChecked = true, GroupName = "plan" };
        var yearlyBtn = new RadioButton { Content = $"Yearly — {yearlyPrice}/year (save ~20%)", GroupName = "plan" };
        panel.Children.Add(monthlyBtn);
        panel.Children.Add(yearlyBtn);

        panel.Children.Add(new TextBlock { Text = "Payment method:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 0) });

        var stripeBtn = new RadioButton { Content = "Credit/Debit Card (Stripe)", IsChecked = true, GroupName = "method" };
        var btcBtn = new RadioButton { Content = "Bitcoin (BTC)", GroupName = "method" };
        var usdtBtn = new RadioButton { Content = "USDT (ERC-20)", GroupName = "method" };
        panel.Children.Add(stripeBtn);
        panel.Children.Add(btcBtn);
        panel.Children.Add(usdtBtn);

        panel.Children.Add(new TextBlock
        {
            Text = "Card payments are instant. Crypto payments are activated within 1 hour after confirmation.",
            FontSize = 12, Opacity = 0.5, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap
        });

        var dialog = new ContentDialog
        {
            Title = $"Upgrade to {tierName}",
            Content = panel,
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var plan = yearlyBtn.IsChecked == true ? "yearly" : "monthly";

            if (btcBtn.IsChecked == true)
            {
                await CreateCryptoPayment(tier, plan, "btc");
            }
            else if (usdtBtn.IsChecked == true)
            {
                await CreateCryptoPayment(tier, plan, "usdt_erc20");
            }
            else
            {
                Frame.Navigate(typeof(PaymentPage), new PaymentParams(tier, plan, 1));
            }
        }
    }

    private async Task CreateCryptoPayment(string tier, string plan, string crypto)
    {
        var coinName = crypto == "btc" ? "BTC" : "USDT (ERC-20)";

        try
        {
            // Call API to create crypto payment record
            var apiUrl = LoginWindow.ApiBaseUrl;
            var token = LoginWindow.AccessToken;

            if (string.IsNullOrEmpty(token))
            {
                await ShowErrorDialog("Please sign in first to make a payment.");
                return;
            }

            var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/api/payment/crypto/create")
            {
                Content = JsonContent.Create(new { crypto = crypto, tier = tier, plan = plan })
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                await ShowErrorDialog("Failed to create payment. Please try again.");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var paymentId = root.GetProperty("paymentId").GetString() ?? "";
            var address = root.GetProperty("address").GetString() ?? "";
            var amount = root.GetProperty("amount").GetDecimal();
            var currency = root.GetProperty("currency").GetString() ?? "";

            await ShowCryptoPaymentDialog(paymentId, coinName, address, amount, currency);
        }
        catch (Exception ex)
        {
            await ShowErrorDialog($"Connection error: {ex.Message}");
        }
    }

    private async Task ShowCryptoPaymentDialog(string paymentId, string coinName, string address, decimal amount, string currency)
    {
        var panel = new StackPanel { Spacing = 12 };

        panel.Children.Add(new TextBlock
        {
            Text = $"Send exactly ${amount:F2} in {coinName} to:",
            FontSize = 14, TextWrapping = TextWrapping.Wrap
        });

        var addressBox = new TextBox
        {
            Text = address,
            IsReadOnly = true,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        panel.Children.Add(addressBox);

        var copyBtn = new Button
        {
            Content = "Copy Address",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 4, 0, 0)
        };
        copyBtn.Click += (s, e) =>
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(address);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            copyBtn.Content = "Copied!";
        };
        panel.Children.Add(copyBtn);

        panel.Children.Add(new TextBlock
        {
            Text = $"Amount: ${amount:F2} {currency}\n\nAfter sending, paste your TX hash below:",
            FontSize = 13, Opacity = 0.7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0)
        });

        var txHashBox = new TextBox
        {
            PlaceholderText = "Paste transaction hash here...",
            Margin = new Thickness(0, 4, 0, 0)
        };
        panel.Children.Add(txHashBox);

        panel.Children.Add(new TextBlock
        {
            Text = "Or email your TX hash to support@auracore.pro\nSubscription activated within 1 hour.",
            FontSize = 12, Opacity = 0.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0)
        });

        var dialog = new ContentDialog
        {
            Title = $"Pay with {coinName}",
            Content = panel,
            PrimaryButtonText = "Submit TX Hash",
            SecondaryButtonText = "I'll Email Later",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(txHashBox.Text))
        {
            await SubmitTxHash(paymentId, txHashBox.Text.Trim());
        }
        else if (result == ContentDialogResult.Secondary)
        {
            await ShowInfoDialog("Payment Created", $"Your payment ID: {paymentId}\n\nPlease email your TX hash to support@auracore.pro with this payment ID.\n\nYour subscription will be activated within 1 hour after verification.");
        }
    }

    private async Task SubmitTxHash(string paymentId, string txHash)
    {
        try
        {
            var apiUrl = LoginWindow.ApiBaseUrl;
            var token = LoginWindow.AccessToken;

            var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/api/payment/crypto/confirm/{paymentId}")
            {
                Content = JsonContent.Create(new { txHash = txHash })
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await Http.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                await ShowInfoDialog("Payment Submitted", "Thank you! Your transaction is being verified.\nYour subscription will be activated within 1 hour.");
            }
            else
            {
                await ShowErrorDialog("Failed to submit TX hash. Please email it to support@auracore.pro instead.");
            }
        }
        catch
        {
            await ShowErrorDialog("Connection error. Please email your TX hash to support@auracore.pro instead.");
        }
    }

    private async Task ShowErrorDialog(string message)
    {
        var d = new ContentDialog { Title = "Error", Content = message, CloseButtonText = "OK", XamlRoot = this.XamlRoot };
        await d.ShowAsync();
    }

    private async Task ShowInfoDialog(string title, string message)
    {
        var d = new ContentDialog { Title = title, Content = message, CloseButtonText = "OK", XamlRoot = this.XamlRoot };
        await d.ShowAsync();
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("upgrade.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("upgrade.subtitle");
    }
}
