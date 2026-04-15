using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;
using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class ChatOptInDialog : Window
{
    private ModelManagerDialog? _managerDialog;

    public ChatOptInDialog()
    {
        InitializeComponent();
    }

    public ChatOptInDialog(ChatOptInDialogViewModel vm) : this()
    {
        DataContext = vm;
        vm.RequestClose = accepted => Close(accepted);

        // If user previously acknowledged, we start at Step 2 — mount immediately.
        if (vm.IsStep2) MountStep2();

        vm.PropertyChanged += OnVMPropertyChanged;
    }

    private void OnVMPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatOptInDialogViewModel.CurrentStep)
            && DataContext is ChatOptInDialogViewModel vm
            && vm.IsStep2
            && _managerDialog is null)
        {
            MountStep2();
        }
    }

    private void MountStep2()
    {
        try
        {
            var catalog = App.Services.GetRequiredService<IModelCatalog>();
            var installed = App.Services.GetRequiredService<IInstalledModelStore>();
            var downloader = App.Services.GetRequiredService<IModelDownloadService>();

            var managerVm = new ModelManagerDialogViewModel(catalog, installed, ModelManagerDialogMode.OptIn, downloader);
            managerVm.RequestClose = selected =>
            {
                if (DataContext is ChatOptInDialogViewModel outer)
                {
                    if (selected is not null)
                        outer.CompleteFromStep2(selected.Id);
                    else
                        outer.CancelFromStep2.Execute(null);
                }
            };

            _managerDialog = new ModelManagerDialog(managerVm);
            var host = this.FindControl<ContentControl>("PART_Step2Host");
            if (host is not null) host.Content = _managerDialog;
        }
        catch
        {
            // DI not available in design-time / test — ignore
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
