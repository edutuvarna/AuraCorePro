using Avalonia;
using Avalonia.Themes.Fluent;

namespace AuraCore.Tests.UI.Avalonia;

public class AvaloniaTestApplication : global::Avalonia.Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}
