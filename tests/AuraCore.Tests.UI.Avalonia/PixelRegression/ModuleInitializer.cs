using System.Runtime.CompilerServices;
using VerifyTests;

namespace AuraCore.Tests.UI.Avalonia.PixelRegression;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifyImageSharp.Initialize();
    }
}

public static class PixelVerify
{
    // .verified/.received files default to the caller's source file directory.
    // Tests live in PixelRegression/, so "../goldens" lands in tests/.../goldens/.
    public static SettingsTask Verify(byte[] png) =>
        Verifier.Verify(png, "png").UseDirectory("../goldens");
}
