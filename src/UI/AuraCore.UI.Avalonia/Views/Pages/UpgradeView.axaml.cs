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
        ApplyLocalization();
        if (!string.IsNullOrEmpty(moduleName))
        {
            TitleText.Text = $"{moduleName} - {LocalizationService._("upgrade.proRequired")}";
            SubText.Text = string.Format(LocalizationService._("upgrade.proRequired"), moduleName);
        }
        LocalizationService.LanguageChanged += OnLanguageChanged;
        Unloaded += OnUnloaded;
    }

    private void OnLanguageChanged() =>
        global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    private void ApplyLocalization()
    {
        string L(string k) => LocalizationService._(k);
        TitleText.Text = L("upgrade.required");
        SubText.Text = L("upgrade.proRequired");
        FreePlanTitle.Text = L("upgrade.free");
        FreePriceText.Text = L("upgrade.freePrice");
        FreeDescText.Text = L("upgrade.freeDesc");
        FreeF1.Text = L("upgrade.freeFeature.systemHealth");
        FreeF2.Text = L("upgrade.freeFeature.junkCleaner");
        FreeF3.Text = L("upgrade.freeFeature.ramOptimizer");
        FreeF4.Text = L("upgrade.freeFeature.networkOptimizer");
        FreeF5.Text = L("upgrade.freeFeature.gamingMode");
        CurrentPlanLabel.Text = L("upgrade.currentPlan");
        ProPlanTitle.Text = L("upgrade.pro");
        ProPriceText.Text = L("upgrade.proPrice");
        ProPriceSuffix.Text = L("upgrade.proPriceSuffix");
        ProDescText.Text = L("upgrade.proDesc");
        ProF1.Text = L("upgrade.proFeature.everything");
        ProF2.Text = L("upgrade.proFeature.registry");
        ProF3.Text = L("upgrade.proFeature.bloatware");
        ProF4.Text = L("upgrade.proFeature.privacy");
        ProF5.Text = L("upgrade.proFeature.iso");
        ProF6.Text = L("upgrade.proFeature.driver");
        UpgradeBtn.Content = L("upgrade.upgradeToPro");
        YearlySavings.Text = L("upgrade.yearlySavings");
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
