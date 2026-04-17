using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(AuraCore.Tests.Platform.PrivilegeIpcTestAppBuilder))]

namespace AuraCore.Tests.Platform;

public static class PrivilegeIpcTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<PrivilegeIpcTestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public class PrivilegeIpcTestApplication : global::Avalonia.Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}
