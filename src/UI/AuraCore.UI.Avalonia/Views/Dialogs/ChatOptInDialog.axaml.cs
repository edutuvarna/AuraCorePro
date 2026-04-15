using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;
using AuraCore.UI.Avalonia.ViewModels;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class ChatOptInDialog : Window
{
    public ChatOptInDialog()
    {
        InitializeComponent();
    }

    public ChatOptInDialog(ChatOptInDialogViewModel vm) : this()
    {
        DataContext = vm;
        vm.RequestClose = accepted => Close(accepted);
    }

    public void MountStep2Content(Control content)
    {
        var host = this.FindControl<ContentControl>("PART_Step2Host");
        if (host is not null) host.Content = content;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
