using global::Avalonia;

namespace AuraCore.UI.Avalonia;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Phase 6.1.D — URL scheme launch-path integration (Windows-only).
        string? pendingUrl = null;
        AuraCore.UI.Avalonia.Helpers.InstanceMutex? singletonLock = null;

        if (OperatingSystem.IsWindows())
        {
            // Extract a single URL-arg if present.
            foreach (var a in args)
            {
                if (!string.IsNullOrEmpty(a) &&
                    a.StartsWith("auracore://", System.StringComparison.Ordinal))
                {
                    pendingUrl = a;
                    break;
                }
            }

            // Single-instance check.
            singletonLock = new AuraCore.UI.Avalonia.Helpers.InstanceMutex("AuraCorePro.SingletonLock");
            var isPrimary = singletonLock.TryAcquire();

            if (!isPrimary)
            {
                // Secondary: forward URL to primary + exit.
                if (pendingUrl is not null)
                {
                    var client = new AuraCore.UI.Avalonia.Helpers.UrlGatewayClient();
                    try
                    {
                        var sendResult = client.SendAsync(pendingUrl).GetAwaiter().GetResult();
                        if (sendResult == AuraCore.UI.Avalonia.Helpers.GatewaySendResult.NoServer)
                        {
                            // Edge: mutex owned but pipe not up yet. Fall through:
                            // dispose failed state, try to become primary ourselves.
                            singletonLock.Dispose();
                            singletonLock = new AuraCore.UI.Avalonia.Helpers.InstanceMutex("AuraCorePro.SingletonLock");
                            singletonLock.TryAcquire();
                        }
                        else
                        {
                            AuraCore.UI.Avalonia.Helpers.Win32Interop.FocusWindowByTitle("AuraCorePro");
                            singletonLock.Dispose();
                            return;
                        }
                    }
                    catch
                    {
                        AuraCore.UI.Avalonia.Helpers.Win32Interop.FocusWindowByTitle("AuraCorePro");
                        singletonLock.Dispose();
                        return;
                    }
                }
                else
                {
                    // No URL; focus primary + exit.
                    AuraCore.UI.Avalonia.Helpers.Win32Interop.FocusWindowByTitle("AuraCorePro");
                    singletonLock.Dispose();
                    return;
                }
            }

            // Primary path — auto-register scheme idempotently.
            try
            {
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exe))
                {
                    AuraCore.UI.Avalonia.Helpers.UrlSchemeRegistrar.RegisterIfNeeded(exe!);
                }
            }
            catch { /* best-effort */ }
        }

        // Stash pendingUrl + singletonLock so Avalonia's App startup can pick them up.
        App.PendingLaunchUrl = pendingUrl;
        App.SingletonLock = singletonLock;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
