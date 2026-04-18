using System;
using Microsoft.Win32;

namespace AuraCore.UI.Avalonia.Helpers;

/// <summary>
/// Idempotent HKCU URL-scheme registration for auracore://.
/// Safe to call every app launch — no-op if already registered with the
/// correct binary path.
///
/// Honors opt-out DWORD at HKCU\Software\AuraCorePro\DisableUrlSchemeAutoRegister.
/// If that value is 1, RegisterIfNeeded returns without writing.
/// </summary>
public static class UrlSchemeRegistrar
{
    private const string SchemeRoot = "Software\\Classes\\auracore";
    private const string CommandSubkey = "Software\\Classes\\auracore\\shell\\open\\command";
    private const string OptOutRoot = "Software\\AuraCorePro";
    private const string OptOutValueName = "DisableUrlSchemeAutoRegister";

    public static bool IsAutoRegisterDisabled()
    {
        if (!OperatingSystem.IsWindows()) return true;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(OptOutRoot);
            if (key is null) return false;
            var raw = key.GetValue(OptOutValueName);
            return raw is int i && i == 1;
        }
        catch { return false; }
    }

    public static bool IsRegisteredForBinary(string binaryPath)
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            using var cmdKey = Registry.CurrentUser.OpenSubKey(CommandSubkey);
            var current = cmdKey?.GetValue(null) as string;
            if (string.IsNullOrEmpty(current)) return false;

            var expected = $"\"{binaryPath}\" \"%1\"";
            return string.Equals(current, expected, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static bool RegisterIfNeeded(string binaryPath)
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (IsAutoRegisterDisabled()) return false;
        if (IsRegisteredForBinary(binaryPath)) return false; // no-op

        try
        {
            using (var root = Registry.CurrentUser.CreateSubKey(SchemeRoot))
            {
                root.SetValue(null, "URL:AuraCore Protocol");
                root.SetValue("URL Protocol", "");
            }
            using (var cmd = Registry.CurrentUser.CreateSubKey(CommandSubkey))
            {
                cmd.SetValue(null, $"\"{binaryPath}\" \"%1\"");
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool Unregister()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(SchemeRoot, throwOnMissingSubKey: false);
            return true;
        }
        catch { return false; }
    }
}
