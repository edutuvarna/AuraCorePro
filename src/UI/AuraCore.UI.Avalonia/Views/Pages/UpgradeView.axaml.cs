using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using AuraCore.Application;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class UpgradeView : UserControl
{
    private readonly string? _moduleId;

    public UpgradeView() : this(null, null) { }

    public UpgradeView(string? moduleId, string? moduleName)
    {
        _moduleId = moduleId;
        InitializeComponent();
        if (!string.IsNullOrEmpty(moduleName))
        {
            TitleText.Text = $"{moduleName} - Pro Required";
            SubText.Text = $"The {moduleName} module requires an AuraCore Pro subscription to use.";
        }
    }

    private void Upgrade_Click(object? sender, RoutedEventArgs e)
    {
        // Navigate to PaymentView with Stripe checkout
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window window)
        {
            // Find MainWindow's ContentArea
            if (window is MainWindow mw)
            {
                var payment = new PaymentView("pro", "monthly");
                payment.GoBackAction = () => mw.ContentArea.Content = this;
                payment.PaymentSuccessAction = () =>
                {
                    mw.RefreshSession();
                    mw.ContentArea.Content = new DashboardView();
                };
                mw.ContentArea.Content = payment;
                return;
            }
        }

        // Fallback: open browser
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://auracore.pro/#pricing",
                UseShellExecute = true
            });
        }
        catch { }
    }
}
