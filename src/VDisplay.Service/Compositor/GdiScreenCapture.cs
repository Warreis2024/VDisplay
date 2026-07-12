using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VDisplay.Core.Models;

namespace VDisplay.Service.Compositor;

[SupportedOSPlatform("windows")]
internal static class GdiScreenCapture
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

        try
        {
            if (!BitBlt(hdcMem, 0, 0, width, height, hdcScreen, x, y, SrcCopy))
            {
                return null;
            }

            return Image.FromHbitmap(hBitmap);
        }
        finally
        {
            SelectObject(hdcMem, old);
            DeleteObject(hBitmap);
            DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    public static Bitmap? CaptureMonitor(PhysicalMonitorInfo monitor) =>
        CaptureRegion(monitor.X, monitor.Y, monitor.Width, monitor.Height);

    public static Bitmap? CaptureLeftHalf(PhysicalMonitorInfo monitor)
    {
        var halfW = Math.Max(1, monitor.Width / 2);
        return CaptureRegion(monitor.X, monitor.Y, halfW, monitor.Height);
    }

    public static Bitmap? CaptureRightHalf(PhysicalMonitorInfo monitor)
    {
        var halfW = Math.Max(1, monitor.Width / 2);
        return CaptureRegion(monitor.X + halfW, monitor.Y, monitor.Width - halfW, monitor.Height);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(
        IntPtr hdcDest, int xDest, int yDest, int width, int height,
        IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);
}
