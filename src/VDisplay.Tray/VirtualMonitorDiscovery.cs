using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VDisplay.Core.Models;

namespace VDisplay.Tray;

[SupportedOSPlatform("windows")]
internal static class VirtualMonitorDiscovery
{
    /// <summary>
    /// Only returns monitors that belong to the VDisplay IDD driver.
    /// Never treats smaller physical screens as virtual (old heuristic bug).
    /// </summary>
    public static List<PhysicalMonitorInfo> GetVirtualMonitors()
    {
        var virtualDeviceNames = BuildVirtualDeviceNames();
        if (virtualDeviceNames.Count == 0)
        {
            return [];
        }

        return EnumerateAllMonitors()
            .Where(m => virtualDeviceNames.Contains(NormalizeDeviceName(m.Name)))
            .OrderBy(m => m.X)
            .ThenBy(m => m.Y)
            .Select((m, i) =>
            {
                m.Index = i;
                return m;
            })
            .ToList();
    }

    private static List<PhysicalMonitorInfo>? _enumList;
    private static int _enumIndex;

    private static List<PhysicalMonitorInfo> EnumerateAllMonitors()
    {
        var monitors = new List<PhysicalMonitorInfo>();
        _enumList = monitors;
        _enumIndex = 0;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorCallback, IntPtr.Zero);

        _enumList = null;
        return monitors;
    }

    private static bool MonitorCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT rect, IntPtr dwData)
    {
        var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        if (!GetMonitorInfo(hMonitor, ref info))
        {
            return true;
        }

        _enumList!.Add(new PhysicalMonitorInfo
        {
            Index = _enumIndex++,
            Name = info.szDevice.Trim(),
            X = rect.Left,
            Y = rect.Top,
            Width = rect.Right - rect.Left,
            Height = rect.Bottom - rect.Top,
            IsPrimary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0
        });

        return true;
    }

    private static HashSet<string> BuildVirtualDeviceNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var adapter = CreateDisplayDevice();

        for (uint adapterIndex = 0; EnumDisplayDevices(null, adapterIndex, ref adapter, 0); adapterIndex++)
        {
            var virtualAdapter = IsVirtualAdapter(adapter);
            var monitor = CreateDisplayDevice();

            for (uint monitorIndex = 0;
                 EnumDisplayDevices(adapter.DeviceName, monitorIndex, ref monitor, 0);
                 monitorIndex++)
            {
                if ((monitor.StateFlags & DISPLAY_DEVICE_ACTIVE) == 0)
                {
                    monitor = CreateDisplayDevice();
                    continue;
                }

                if (virtualAdapter || IsVirtualMonitorDevice(monitor))
                {
                    names.Add(NormalizeDeviceName(monitor.DeviceName));
                    // DISPLAY1 style from GetMonitorInfo
                    names.Add(NormalizeDeviceName(adapter.DeviceName));
                }

                monitor = CreateDisplayDevice();
            }

            adapter = CreateDisplayDevice();
        }

        return names;
    }

    private static string NormalizeDeviceName(string name)
    {
        var trimmed = name.Trim();
        return trimmed.StartsWith(@"\\.\", StringComparison.Ordinal)
            ? trimmed
            : trimmed.StartsWith("DISPLAY", StringComparison.OrdinalIgnoreCase)
                ? @"\\.\" + trimmed
                : trimmed;
    }

    private static bool IsVirtualAdapter(DISPLAY_DEVICE adapter) =>
        ContainsVirtualMarker(adapter.DeviceString) || ContainsVirtualMarker(adapter.DeviceID);

    private static bool IsVirtualMonitorDevice(DISPLAY_DEVICE monitor) =>
        ContainsVirtualMarker(monitor.DeviceString) || ContainsVirtualMarker(monitor.DeviceID);

    private static bool ContainsVirtualMarker(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && (value.Contains("VDisplay", StringComparison.OrdinalIgnoreCase)
            || value.Contains("VDisplayDriver", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Indirect Display", StringComparison.OrdinalIgnoreCase)
            || value.Contains("IndirectDisplay", StringComparison.OrdinalIgnoreCase)
            || value.Contains("IddSampleDriver", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Virtual Monitor", StringComparison.OrdinalIgnoreCase));

    private static DISPLAY_DEVICE CreateDisplayDevice() =>
        new() { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    private const int MONITORINFOF_PRIMARY = 1;
    private const int DISPLAY_DEVICE_ACTIVE = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }
}
