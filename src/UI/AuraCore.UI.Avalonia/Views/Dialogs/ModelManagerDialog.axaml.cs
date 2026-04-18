using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Markup.Xaml;
using AuraCore.UI.Avalonia.ViewModels;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class ModelManagerDialog : UserControl
{
    public ModelManagerDialog()
    {
        InitializeComponent();
        ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    public ModelManagerDialog(ModelManagerDialogViewModel vm) : this()
    {
        DataContext = vm;
    }

    private void ApplyLocalization()
    {
        var downloadingLabel = this.FindControl<TextBlock>("DownloadingLabel");
        if (downloadingLabel is not null)
            downloadingLabel.Text = LocalizationService.Get("modelManager.downloading");

        var cancelBtn = this.FindControl<Button>("CancelDialogButton");
        if (cancelBtn is not null)
            cancelBtn.Content = LocalizationService.Get("modelManager.cancelButton");

        var downloadBtn = this.FindControl<Button>("DownloadUseButton");
        if (downloadBtn is not null)
            downloadBtn.Content = LocalizationService.Get("modelManager.downloadButton");
    }

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border b && b.Tag is ModelListItemVM item && DataContext is ModelManagerDialogViewModel vm)
        {
            if (!item.IsSelectable) return;
            vm.SelectedModel = item;
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
