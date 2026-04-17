using System.Threading;
using System.Threading.Tasks;
using AuraCore.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.ViewModels.Dialogs;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Dialogs;

public class PrivilegedHelperInstallDialogVMTests
{
    private sealed class FakePipeProbe : IPipeProbe
    {
        private readonly bool _reachable;
        public FakePipeProbe(bool reachable) { _reachable = reachable; }
        public Task<bool> CanConnectAsync(int timeoutMs, CancellationToken ct)
            => Task.FromResult(_reachable);
    }

    [Fact]
    public void Constructor_sets_intro_text()
    {
        var installer = new PrivilegedHelperInstaller(new FakePipeProbe(false), _ => Task.FromResult(true));
        var vm = new PrivilegedHelperInstallDialogVM(installer);
        Assert.False(string.IsNullOrEmpty(vm.StatusText));
        Assert.False(vm.IsInstalling);
    }

    [Fact]
    public async Task InstallAsync_resets_IsInstalling_after_completion()
    {
        var installer = new PrivilegedHelperInstaller(new FakePipeProbe(true), _ => Task.FromResult(true));
        var vm = new PrivilegedHelperInstallDialogVM(installer);
        await vm.InstallAsync(CancellationToken.None);
        Assert.False(vm.IsInstalling);
    }

    [Fact]
    public void Title_InstallLabel_CancelLabel_resolve_through_localization()
    {
        var installer = new PrivilegedHelperInstaller(new FakePipeProbe(false), _ => Task.FromResult(true));
        var vm = new PrivilegedHelperInstallDialogVM(installer);
        Assert.False(string.IsNullOrEmpty(vm.Title));
        Assert.False(string.IsNullOrEmpty(vm.InstallLabel));
        Assert.False(string.IsNullOrEmpty(vm.CancelLabel));
    }
}
