namespace AuraCore.Desktop.Helpers;

public static class WindowsVersion
{
    private static readonly Version _version = Environment.OSVersion.Version;

    // Win11 = 10.0.22000+
    public static bool IsWindows11 => _version.Build >= 22000;
    public static bool IsWindows10 => _version.Build >= 10240 && _version.Build < 22000;

    public static int Build => _version.Build;

    public static string DisplayName => IsWindows11
        ? $"Windows 11 (Build {Build})"
        : $"Windows 10 (Build {Build})";
}
