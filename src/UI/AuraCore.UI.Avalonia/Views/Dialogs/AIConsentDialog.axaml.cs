using global::Avalonia.Controls;
using global::Avalonia.Interactivity;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class AIConsentDialog : UserControl
{
    /// <summary>Fired when user makes a consent choice (allow or decline).</summary>
    public event EventHandler? ConsentCompleted;

    public AIConsentDialog()
    {
        InitializeComponent();
        ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private void ApplyLocalization()
    {
        var isTr = LocalizationService.CurrentLanguage == "tr";

        TitleLabel.Text = isTr
            ? "AuraCore'un \u00D6\u011Frenmesine Yard\u0131m Et"
            : "Help AuraCore Learn";

        DescriptionLabel.Text = isTr
            ? "AuraCore Pro, sisteminizi analiz etmek i\u00E7in lokal yapay zeka kullan\u0131r. " +
              "Yapay zekan\u0131n do\u011Frulu\u011Funu art\u0131rmak i\u00E7in anonim kullan\u0131m istatistikleri " +
              "(CPU/RAM ortalamalar\u0131, disk trendleri) toplayabiliriz. " +
              "Ki\u015Fisel veri toplanmaz. T\u00FCm AI analizi cihaz\u0131n\u0131zda lokal olarak \u00E7al\u0131\u015F\u0131r."
            : "AuraCore Pro uses local AI to analyze your system. " +
              "To improve AI accuracy, we can collect anonymous usage statistics " +
              "(CPU/RAM averages, disk trends). No personal data is collected. " +
              "All AI analysis runs locally on your device.";

        AllowBtnText.Text = isTr ? "\u2713 \u0130zin Veriyorum" : "\u2713 Allow";
        DeclineBtnText.Text = isTr ? "\u2717 \u0130zin Vermiyorum" : "\u2717 Decline";

        NoteLabel.Text = isTr
            ? "Bu ayar\u0131 daha sonra Ayarlar sayfas\u0131ndan de\u011Fi\u015Ftirebilirsiniz."
            : "You can change this later in Settings.";
    }

    private void Allow_Click(object? sender, RoutedEventArgs e)
    {
        AIConsentSettings.Save(true);
        ConsentCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void Decline_Click(object? sender, RoutedEventArgs e)
    {
        AIConsentSettings.Save(false);
        ConsentCompleted?.Invoke(this, EventArgs.Empty);
    }
}
