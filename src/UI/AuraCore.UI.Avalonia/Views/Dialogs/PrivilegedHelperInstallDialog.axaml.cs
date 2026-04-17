using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AuraCore.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.ViewModels.Dialogs;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class PrivilegedHelperInstallDialog : Window
{
    private PrivilegedHelperInstallDialogVM? _vm;
    private CancellationTokenSource? _cts;

    public PrivilegedHelperInstallOutcome? Outcome { get; private set; }

    public PrivilegedHelperInstallDialog()
    {
        InitializeComponent();
    }

    public PrivilegedHelperInstallDialog(PrivilegedHelperInstaller installer) : this()
    {
        _vm = new PrivilegedHelperInstallDialogVM(installer);
        DataContext = _vm;
    }

    private async void Install_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) { Close(); return; }
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        Outcome = await _vm.InstallAsync(_cts.Token);
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Outcome = PrivilegedHelperInstallOutcome.UserCancelled;
        Close();
    }
}
