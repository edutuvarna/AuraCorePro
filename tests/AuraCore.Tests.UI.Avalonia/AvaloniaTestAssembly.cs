using Avalonia;
using Avalonia.Headless;
using AuraCore.Tests.UI.Avalonia;

[assembly: AvaloniaTestApplication(typeof(AvaloniaTestAppBuilder))]

namespace AuraCore.Tests.UI.Avalonia;

public static class AvaloniaTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<AvaloniaTestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
