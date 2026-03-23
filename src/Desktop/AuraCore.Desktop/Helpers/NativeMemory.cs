using System.Runtime.InteropServices;

namespace AuraCore.Desktop;

internal static class NativeMemory
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct MEMORYSTATUSEX
    {
        internal uint dwLength;
        internal uint dwMemoryLoad;
        internal ulong ullTotalPhys;
        internal ulong ullAvailPhys;
        internal ulong ullTotalPageFile;
        internal ulong ullAvailPageFile;
        internal ulong ullTotalVirtual;
        internal ulong ullAvailVirtual;
        internal ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", EntryPoint = "GlobalMemoryStatusEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool NativeGlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    internal static bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX mem)
    {
        mem.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
        return NativeGlobalMemoryStatusEx(ref mem);
    }
}
