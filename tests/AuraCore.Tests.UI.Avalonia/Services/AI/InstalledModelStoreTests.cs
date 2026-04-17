using AuraCore.UI.Avalonia.Services.AI;
using System.IO;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services.AI;

public class InstalledModelStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IModelCatalog _catalog;

    public InstalledModelStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "auracore-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _catalog = new ModelCatalog();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Enumerate_EmptyDir_ReturnsEmpty()
    {
        var store = new InstalledModelStore(_catalog, _tempDir);
        Assert.Empty(store.Enumerate());
    }

    [Fact]
    public void Enumerate_MissingDir_ReturnsEmpty()
    {
        var missing = Path.Combine(_tempDir, "nonexistent");
        var store = new InstalledModelStore(_catalog, missing);
        Assert.Empty(store.Enumerate());
    }

    [Fact]
    public void Enumerate_KnownGgufFile_ReturnsInstalledModel()
    {
        var path = Path.Combine(_tempDir, "auracore-tinyllama.gguf");
        File.WriteAllText(path, "fake-gguf");

        var store = new InstalledModelStore(_catalog, _tempDir);
        var installed = store.Enumerate();

        Assert.Single(installed);
        Assert.Equal("tinyllama", installed[0].ModelId);
    }

    [Fact]
    public void Enumerate_UnknownGgufFile_Ignored()
    {
        var path = Path.Combine(_tempDir, "random-model.gguf");
        File.WriteAllText(path, "fake-gguf");

        var store = new InstalledModelStore(_catalog, _tempDir);
        Assert.Empty(store.Enumerate());
    }

    [Fact]
    public void Enumerate_NonGgufFile_Ignored()
    {
        File.WriteAllText(Path.Combine(_tempDir, "readme.txt"), "hi");
        File.WriteAllText(Path.Combine(_tempDir, "auracore-tinyllama.zip"), "data");

        var store = new InstalledModelStore(_catalog, _tempDir);
        Assert.Empty(store.Enumerate());
    }

    [Fact]
    public void IsInstalled_Installed_ReturnsTrue()
    {
        File.WriteAllText(Path.Combine(_tempDir, "auracore-phi2.gguf"), "fake");
        var store = new InstalledModelStore(_catalog, _tempDir);
        Assert.True(store.IsInstalled("phi2"));
    }

    [Fact]
    public void IsInstalled_NotInstalled_ReturnsFalse()
    {
        var store = new InstalledModelStore(_catalog, _tempDir);
        Assert.False(store.IsInstalled("phi2"));
    }

    [Fact]
    public void GetFile_Installed_ReturnsFileInfo()
    {
        var path = Path.Combine(_tempDir, "auracore-phi2.gguf");
        File.WriteAllText(path, "fake");
        var store = new InstalledModelStore(_catalog, _tempDir);

        var file = store.GetFile("phi2");

        Assert.NotNull(file);
        Assert.Equal(path, file!.FullName);
    }

    [Fact]
    public void GetFile_NotInstalled_ReturnsNull()
    {
        var store = new InstalledModelStore(_catalog, _tempDir);
        Assert.Null(store.GetFile("phi2"));
    }
}
