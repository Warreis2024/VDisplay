using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VDisplay.Core.Models;

namespace VDisplay.Overlay;

[SupportedOSPlatform("windows")]
internal static class MonitorEnumerator
{
    public static List<PhysicalMonitorInfo> GetPhysicalMonitors()
    {
        var all = EnumerateAll();
        if (all.Count == 0)
        {
            return [];
        }

        var maxArea = all.Max(m => (long)m.Width * m.Height);
        return all
            .Where(m => (long)m.Width * m.Height == maxArea)
            .OrderBy(m => m.X)
            .ThenBy(m => m.Y)
            .ToList();
    }

    private static List<PhysicalMonitorInfo> EnumerateAll()
    {
        var list = new List<PhysicalMonitorInfo>();
        var index = 0;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr _, IntPtr __, ref Rect rect, IntPtr ___) =>
        {
            var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
            if (!GetMonitorInfo(_, ref info))
            {
                return true;
            }

            list.Add(new PhysicalMonitorInfo
            {
                Index = index++,
                Name = info.Device.Trim(),
                X = rect.Left,
                Y = rect.Top,
                Width = rect.Right - rect.Left,
                Height = rect.Bottom - rect.Top,
                IsPrimary = (info.Flags & 1) != 0
            });
            return true;
        }, IntPtr.Zero);
        return list;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx info);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public int Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Device;
    }
}

[SupportedOSPlatform("windows")]
internal static class ScreenCapture
{
    private const int SrcCopy = 0x00CC0020;

    public static Bitmap? CaptureRegion(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var hdcScreen = GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero)
        {
            return null;
        }

        var hdcMem = CreateCompatibleDC(hdcScreen);
        var hBitmap = CreateCompatibleBitmap(hdcScreen, width, height);
        var old = SelectObject(hdcMem, hBitmap);
        Bitmap? result = null;

        try
        {
            if (BitBlt(hdcMem, 0, 0, width, height, hdcScreen, x, y, SrcCopy))
            {
                result = Image.FromHbitmap(hBitmap);
            }
        }
        finally
        {
            SelectObject(hdcMem, old);
            DeleteObject(hBitmap);
            DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcScreen);
        }

        return result;
    }

    public static Bitmap? CaptureLeftHalf(PhysicalMonitorInfo m)
    {
        var w = Math.Max(1, m.Width / 2);
        return CaptureRegion(m.X, m.Y, w, m.Height);
    }

    public static Bitmap? CaptureRightHalf(PhysicalMonitorInfo m)
    {
        var w = Math.Max(1, m.Width / 2);
        return CaptureRegion(m.X + w, m.Y, m.Width - w, m.Height);
    }

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr d, int dx, int dy, int w, int h, IntPtr s, int sx, int sy, int op);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
}
