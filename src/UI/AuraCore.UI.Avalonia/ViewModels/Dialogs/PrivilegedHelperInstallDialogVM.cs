using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.UI.Avalonia.Helpers;

namespace AuraCore.UI.Avalonia.ViewModels.Dialogs;

public sealed class PrivilegedHelperInstallDialogVM : INotifyPropertyChanged
{
    private readonly PrivilegedHelperInstaller _installer;
    private bool _isInstalling;
    private string _statusText = "";

    public PrivilegedHelperInstallDialogVM(PrivilegedHelperInstaller installer)
    {
        _installer = installer;
        StatusText = LocalizationService.Get("privhelper.dialog.intro");
    }

    public string Title        => LocalizationService.Get("privhelper.dialog.title");
    public string CancelLabel  => LocalizationService.Get("privhelper.dialog.cancelLabel");
    public string InstallLabel => LocalizationService.Get("privhelper.dialog.installLabel");

    public bool IsInstalling
    {
        get => _isInstalling;
        private set { if (_isInstalling != value) { _isInstalling = value; Raise(); } }
    }

    public string StatusText
    {
        get => _statusText;
        private set { if (_statusText != value) { _statusText = value; Raise(); } }
    }

    public async Task<PrivilegedHelperInstallOutcome> InstallAsync(CancellationToken ct)
    {
        IsInstalling = true;
        try
        {
            StatusText = LocalizationService.Get("privhelper.dialog.installing");
            var outcome = await _installer.InstallAsync(ct);
            StatusText = outcome switch
            {
                PrivilegedHelperInstallOutcome.Success       => LocalizationService.Get("privhelper.dialog.success"),
                PrivilegedHelperInstallOutcome.UserCancelled => LocalizationService.Get("privhelper.dialog.userCancelled"),
                PrivilegedHelperInstallOutcome.Timeout       => LocalizationService.Get("privhelper.dialog.timeout"),
                _                                            => LocalizationService.Get("privhelper.dialog.failed")
            };
            return outcome;
        }
        finally
        {
            IsInstalling = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
