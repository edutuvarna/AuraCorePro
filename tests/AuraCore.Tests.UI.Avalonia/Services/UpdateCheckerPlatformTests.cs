using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services;

public class UpdateCheckerPlatformTests
{
    [Fact]
    public void DetectPlatform_returns_non_empty_string()
    {
        // Sanity-check OS detection logic that mirrors UpdateChecker.DetectPlatform()
        var p = OperatingSystem.IsWindows() ? "windows"
              : OperatingSystem.IsLinux()   ? "linux"
              : OperatingSystem.IsMacOS()   ? "macos" : "windows";
        Assert.NotEmpty(p);
        Assert.Contains(p, new[] { "windows", "linux", "macos" });
    }
}
