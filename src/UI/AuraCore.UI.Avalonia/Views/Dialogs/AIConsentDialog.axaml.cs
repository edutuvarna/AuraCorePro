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
        TitleLabel.Text       = LocalizationService.Get("aiConsent.title");
        DescriptionLabel.Text = LocalizationService.Get("aiConsent.description");
        AllowBtnText.Text     = LocalizationService.Get("aiConsent.allow");
        DeclineBtnText.Text   = LocalizationService.Get("aiConsent.decline");
        NoteLabel.Text        = LocalizationService.Get("aiConsent.note");
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
