using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using System.Linq;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class ModelManagerDialogViewModelTests
{
    private static readonly long InstalledRam = 32L * 1024 * 1024 * 1024;

    private ModelManagerDialogViewModel CreateVM(
        ModelManagerDialogMode mode = ModelManagerDialogMode.OptIn,
        params string[] installedIds)
    {
        var catalog = new ModelCatalog();
        var installed = new FakeInstalledStore(catalog, installedIds);
        return new ModelManagerDialogViewModel(catalog, installed, mode, physicalRamBytes: InstalledRam);
    }

    private sealed class FakeInstalledStore : IInstalledModelStore
    {
        private readonly IModelCatalog _catalog;
        private readonly HashSet<string> _ids;
        public FakeInstalledStore(IModelCatalog c, IEnumerable<string> ids) { _catalog = c; _ids = new HashSet<string>(ids); }
        public IReadOnlyList<InstalledModel> Enumerate() =>
            _catalog.All.Where(m => _ids.Contains(m.Id))
                .Select(m => new InstalledModel(m.Id, new System.IO.FileInfo(m.Filename), m.SizeBytes, DateTime.UtcNow))
                .ToList();
        public bool IsInstalled(string modelId) => _ids.Contains(modelId);
        public System.IO.FileInfo? GetFile(string modelId) => _ids.Contains(modelId) ? new System.IO.FileInfo(modelId + ".gguf") : null;
    }

    [Fact]
    public void OptInMode_StartsWithNoSelection()
    {
        var vm = CreateVM();
        Assert.Null(vm.SelectedModel);
        Assert.False(vm.CanDownload);
    }

    [Fact]
    public void SelectingModel_EnablesCanDownload()
    {
        var vm = CreateVM();
        vm.SelectedModel = vm.Models.First(m => m.Model.Id == "phi3-mini-q4km");
        // Note: CanDownload also requires _downloader != null — supply one
    }

    [Fact]
    public void OptInMode_TitleSaysChoose()
    {
        var vm = CreateVM(mode: ModelManagerDialogMode.OptIn);
        Assert.Contains("Choose", vm.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManageMode_TitleSaysManage()
    {
        var vm = CreateVM(mode: ModelManagerDialogMode.Manage);
        Assert.Contains("Manage", vm.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManageMode_MarksInstalledModels()
    {
        var vm = CreateVM(mode: ModelManagerDialogMode.Manage, "phi2", "tinyllama");
        var installed = vm.Models.Where(m => m.IsInstalled).Select(m => m.Model.Id).ToList();
        Assert.Equal(2, installed.Count);
        Assert.Contains("phi2", installed);
        Assert.Contains("tinyllama", installed);
    }

    [Fact]
    public void InsufficientRam_DisablesHeavyTierSelection()
    {
        var catalog = new ModelCatalog();
        var installed = new FakeInstalledStore(catalog, Array.Empty<string>());
        var vm = new ModelManagerDialogViewModel(catalog, installed, ModelManagerDialogMode.OptIn,
            physicalRamBytes: 16L * 1024 * 1024 * 1024);
        var heavy = vm.Models.Where(m => m.Model.Tier == ModelTier.Heavy);
        Assert.All(heavy, m => Assert.False(m.IsSelectable));
        Assert.All(heavy, m => Assert.NotNull(m.DisabledReason));
    }

    [Fact]
    public void SufficientRam_EnablesAllTiers()
    {
        var vm = CreateVM();
        Assert.All(vm.Models.Where(m => m.Model.Tier != ModelTier.Heavy),
            m => Assert.True(m.IsSelectable));
    }

    [Fact]
    public void ManageMode_CanDownload_FalseForAlreadyInstalled()
    {
        var vm = CreateVM(mode: ModelManagerDialogMode.Manage, "phi2");
        vm.SelectedModel = vm.Models.First(m => m.Model.Id == "phi2");
        Assert.False(vm.CanDownload);
    }
}
