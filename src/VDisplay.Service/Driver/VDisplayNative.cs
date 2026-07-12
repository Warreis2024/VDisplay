using System.Runtime.InteropServices;

namespace VDisplay.Service.Driver;

internal static class VDisplayNative
{
    private const string DllName = "VDisplayNative.dll";

    [DllImport(DllName, ExactSpelling = true)]
    public static extern int VDisplayStartSoftwareDevice();

    [DllImport(DllName, ExactSpelling = true)]
    public static extern void VDisplayStopSoftwareDevice();

    [DllImport(DllName, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool VDisplayIsSoftwareDeviceRunning();
}
