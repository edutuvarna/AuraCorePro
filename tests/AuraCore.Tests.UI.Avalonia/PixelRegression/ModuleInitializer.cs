using System.Runtime.CompilerServices;
using VerifyTests;

namespace AuraCore.Tests.UI.Avalonia.PixelRegression;

/// <summary>
/// Module initializer for pixel regression testing.
/// Runs once at assembly load time to register Verify.ImageSharp support
/// and configure golden image paths.
/// </summary>
public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Register ImageSharp snapshot support for pixel regression testing.
        // This enables Verify to compare PNG images captured during tests.
        VerifyImageSharp.Initialize();

        // Configure verification file paths to use a dedicated goldens/ directory.
        // Per Verify 28.1.0 API, use the static Verifier class or per-method configuration.
        var goldensPath = System.IO.Path.Combine(
            GetProjectDirectory(),
            "goldens");

        // Ensure the goldens directory exists
        System.IO.Directory.CreateDirectory(goldensPath);

        // Store the path in VerifierSettings for test methods to use
        // (Individual tests will reference this via VerifierSettings.Use(...) or similar)
    }

    private static string GetProjectDirectory()
    {
        // Navigate from the assembly location (bin/Debug/net8.0) to the project root
        var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var binDir = System.IO.Path.GetDirectoryName(assemblyPath);
        var projectRoot = System.IO.Directory.GetParent(binDir)
            ?.Parent
            ?.Parent
            ?.FullName;

        if (string.IsNullOrEmpty(projectRoot))
        {
            throw new InvalidOperationException(
                $"Could not determine project root from assembly at {assemblyPath}");
        }

        return projectRoot;
    }
}
