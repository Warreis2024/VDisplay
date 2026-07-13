using System.Runtime.InteropServices;
using VDisplay.Core.Models;

namespace VDisplay.Service.Capture;

public sealed class PhysicalMonitorProvider
{
    private List<PhysicalMonitorInfo>? _currentList;
    private int _currentIndex;

    public IReadOnlyList<PhysicalMonitorInfo> GetMonitors()
    {
        var classification = ClassifyMonitors();
        return classification.Physical.Concat(classification.Virtual)
            .OrderBy(m => m.X)
            .ThenBy(m => m.Y)
            .ToList();
    }

    public IReadOnlyList<PhysicalMonitorInfo> GetCaptureSources() =>
        ClassifyMonitors().Physical;

    public IReadOnlyList<PhysicalMonitorInfo> GetVirtualMonitorDisplays(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        return ClassifyMonitors().Virtual.Take(count).ToList();
    }

    public void RefreshDeviceCache()
    {
        // Eski surumlerle uyumluluk; siniflandirma her cagrida yenilenir.
    }

    private MonitorClassification ClassifyMonitors()
    {
        var all = EnumerateAllMonitors();
        if (all.Count == 0)
        {
            return new MonitorClassification([], []);
        }

        var adapterVirtualNames = BuildVirtualDeviceNames();
        var maxArea = all.Max(m => (long)m.Width * m.Height);

        // En buyuk cozunurluk = fiziksel panel (DISPLAY1/2)
        var physicalMonitors = all
            .Where(m => (long)m.Width * m.Height == maxArea)
            .Where(m => !adapterVirtualNames.Contains(NormalizeDeviceName(m.Name)))
            .OrderBy(m => m.X)
            .ThenBy(m => m.Y)
            .ToList();

        if (physicalMonitors.Count == 0)
        {
            physicalMonitors = all
                .Where(m => (long)m.Width * m.Height == maxArea)
                .OrderBy(m => m.X)
                .ThenBy(m => m.Y)
                .ToList();
        }

        var physicalNames = physicalMonitors
            .Select(m => NormalizeDeviceName(m.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Only real VDisplay / IDD adapters — not other GPUs' secondary outputs.
        var virtualMonitors = all
            .Where(m =>
            {
                var name = NormalizeDeviceName(m.Name);
                return !physicalNames.Contains(name)
                    && adapterVirtualNames.Contains(name);
            })
            .OrderBy(m => m.X)
            .ThenBy(m => m.Y)
            .ToList();

        return new MonitorClassification(physicalMonitors, virtualMonitors);
    }

    private List<PhysicalMonitorInfo> EnumerateAllMonitors()
    {
        var monitors = new List<PhysicalMonitorInfo>();
        _currentList = monitors;
        _currentIndex = 0;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);

        _currentList = null;
        return monitors;
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
                }

                monitor = CreateDisplayDevice();
            }

            adapter = CreateDisplayDevice();
        }

        return names;
    }

    private bool Callback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
    {
        var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        if (!GetMonitorInfo(hMonitor, ref info))
        {
            return true;
        }

        var deviceName = NormalizeDeviceName(info.szDevice);
        var rect = info.rcMonitor;
        _currentList!.Add(new PhysicalMonitorInfo
        {
            Index = _currentIndex++,
            Name = deviceName,
            X = rect.Left,
            Y = rect.Top,
            Width = rect.Right - rect.Left,
            Height = rect.Bottom - rect.Top,
            IsPrimary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0
        });

        return true;
    }

    private static string NormalizeDeviceName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return trimmed;
        }

        return trimmed.StartsWith("DISPLAY", StringComparison.OrdinalIgnoreCase)
            ? @"\\.\" + trimmed
            : trimmed;
    }

    private static bool IsVirtualAdapter(DISPLAY_DEVICE adapter) =>
        ContainsVirtualMarker(adapter.DeviceString)
        || ContainsVirtualMarker(adapter.DeviceID);

    private static bool IsVirtualMonitorDevice(DISPLAY_DEVICE monitor) =>
        ContainsVirtualMarker(monitor.DeviceString)
        || ContainsVirtualMarker(monitor.DeviceID);

    private static bool ContainsVirtualMarker(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("VDisplay", StringComparison.OrdinalIgnoreCase)
            || value.Contains("VDisplayDriver", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Indirect Display", StringComparison.OrdinalIgnoreCase)
            || value.Contains("IndirectDisplay", StringComparison.OrdinalIgnoreCase)
            || value.Contains("IddSample", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Virtual Monitor", StringComparison.OrdinalIgnoreCase);
    }

    private static DISPLAY_DEVICE CreateDisplayDevice() =>
        new() { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };

    private sealed record MonitorClassification(
        IReadOnlyList<PhysicalMonitorInfo> Physical,
        IReadOnlyList<PhysicalMonitorInfo> Virtual);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DISPLAY_DEVICE lpDisplayDevice,
        uint dwFlags);

    private const int MONITORINFOF_PRIMARY = 1;
    private const int DISPLAY_DEVICE_ACTIVE = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
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
