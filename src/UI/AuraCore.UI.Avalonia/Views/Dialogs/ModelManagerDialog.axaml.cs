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
    }

    public ModelManagerDialog(ModelManagerDialogViewModel vm) : this()
    {
        DataContext = vm;
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
