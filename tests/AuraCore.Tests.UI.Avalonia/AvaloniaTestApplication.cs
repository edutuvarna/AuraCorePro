using Avalonia;
using Avalonia.Themes.Fluent;
using AuraCore.UI.Avalonia.Converters;

namespace AuraCore.Tests.UI.Avalonia;

public class AvaloniaTestApplication : global::Avalonia.Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());

        // Register converters as static resources for UI tests.
        // Phase 5.3/5.4 narrow-mode + text-transform converters.
        Resources.Add("BoundsToIsNarrowModeConverter", new BoundsToIsNarrowModeConverter());
        Resources.Add("NarrowToColumnCountConverter", new NarrowToColumnCountConverter());
        Resources.Add("BoolToGridLengthConverter", new BoolToGridLengthConverter());
        Resources.Add("NarrowModeGridLengthChainConverter", new NarrowModeGridLengthChainConverter());
        Resources.Add("UppercaseTransformConverter", new UppercaseTransformConverter());
    }
}
