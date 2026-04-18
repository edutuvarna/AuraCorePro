using System;
using System.Runtime.InteropServices;

namespace AuraCore.UI.Avalonia.Helpers;

public static class Win32Interop
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    public static bool FocusWindowByTitle(string windowTitle)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var hWnd = FindWindow(null, windowTitle);
        if (hWnd == IntPtr.Zero) return false;
        ShowWindow(hWnd, SW_RESTORE);
        return SetForegroundWindow(hWnd);
    }
}
