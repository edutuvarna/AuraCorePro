using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;

namespace AuraCore.UI.Avalonia.Views.Controls;

/// <summary>
/// Soft non-modal banner shown when a non-mandatory update is available.
/// Visibility controlled by MainWindow code-behind via UpdateChecker.UpdateFound event.
/// Button text is applied via ApplyLocalization() on load + language change.
/// </summary>
public partial class UpdateBanner : UserControl
{
    public UpdateBanner()
    {
        InitializeComponent();
        ApplyLocalization();

        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private void ApplyLocalization()
    {
        var updateBtn = this.FindControl<Button>("UpdateBtn");
        var laterBtn  = this.FindControl<Button>("LaterBtn");

        if (updateBtn is not null)
            updateBtn.Content = LocalizationService.Get("UpdateBanner_UpdateNow");
        if (laterBtn is not null)
            laterBtn.Content = LocalizationService.Get("UpdateBanner_Later");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
