using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VDisplay.Capture;
using VDisplay.Core.Models;

namespace VDisplay.Tray;

[SupportedOSPlatform("windows")]
internal static class ScreenCapture
{
    private const int SrcCopy = 0x00CC0020;
    private const int StretchCopy = 0x00CC0020;
    private const int CursorShowing = 0x00000001;
    private const int DiNormal = 0x0003;

    public static Bitmap? CaptureMonitor(PhysicalMonitorInfo monitor)
    {
        var dxgi = DxgiDesktopCapture.TryCapture(monitor);
        if (dxgi is not null)
        {
            return dxgi;
        }

        return CaptureInternal(monitor.X, monitor.Y, monitor.Width, monitor.Height, monitor.Width, monitor.Height, monitor.X, monitor.Y, drawCursor: false);
    }

    public static Bitmap? CaptureMonitorPreview(PhysicalMonitorInfo monitor, int maxWidth = 960)
    {
        if (monitor.Width <= maxWidth)
        {
            return CaptureMonitor(monitor);
        }

        var dstW = maxWidth;
        var dstH = Math.Max(1, (int)((double)monitor.Height * maxWidth / monitor.Width));

        var dxgi = DxgiDesktopCapture.TryCapture(monitor, dstW, dstH);
        if (dxgi is not null)
        {
            return dxgi;
        }

        return CaptureInternal(
            monitor.X, monitor.Y, monitor.Width, monitor.Height,
            dstW, dstH,
            monitor.X, monitor.Y,
            drawCursor: false);
    }

    public static Bitmap? CaptureRegion(int x, int y, int width, int height) =>
        CaptureInternal(x, y, width, height, width, height, x, y, drawCursor: false);

    private static Bitmap? CaptureInternal(
        int srcX,
        int srcY,
        int srcW,
        int srcH,
        int dstW,
        int dstH,
        int cursorRegionX,
        int cursorRegionY,
        bool drawCursor)
    {
        if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0)
        {
            return null;
        }

        var hdcScreen = GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero)
        {
            return null;
        }

        var hdcMem = CreateCompatibleDC(hdcScreen);
        var hBitmap = CreateCompatibleBitmap(hdcScreen, dstW, dstH);
        var old = SelectObject(hdcMem, hBitmap);
        Bitmap? result = null;

        try
        {
            var ok = srcW == dstW && srcH == dstH
                ? BitBlt(hdcMem, 0, 0, dstW, dstH, hdcScreen, srcX, srcY, SrcCopy)
                : StretchBlt(hdcMem, 0, 0, dstW, dstH, hdcScreen, srcX, srcY, srcW, srcH, StretchCopy);

            if (ok)
            {
                result = Image.FromHbitmap(hBitmap);
                if (drawCursor && result is not null)
                {
                    var scaleX = (double)result.Width / srcW;
                    var scaleY = (double)result.Height / srcH;
                    DrawCursorScaled(result, cursorRegionX, cursorRegionY, scaleX, scaleY);
                }
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

    public static void DrawCursor(Bitmap bitmap, int regionX, int regionY) =>
        DrawCursorScaled(bitmap, regionX, regionY, 1.0, 1.0);

    private static void DrawCursorScaled(Bitmap bitmap, int regionX, int regionY, double scaleX, double scaleY)
    {
        var info = new CursorInfo { cbSize = Marshal.SizeOf<CursorInfo>() };
        if (!GetCursorInfo(ref info) || (info.flags & CursorShowing) == 0 || info.hCursor == IntPtr.Zero)
        {
            return;
        }

        if (!GetIconInfo(info.hCursor, out var iconInfo))
        {
            return;
        }

        try
        {
            if (info.ptScreenPos.X < regionX || info.ptScreenPos.X >= regionX + (bitmap.Width / scaleX)
                || info.ptScreenPos.Y < regionY || info.ptScreenPos.Y >= regionY + (bitmap.Height / scaleY))
            {
                return;
            }

            var drawX = (int)((info.ptScreenPos.X - regionX - iconInfo.xHotspot) * scaleX);
            var drawY = (int)((info.ptScreenPos.Y - regionY - iconInfo.yHotspot) * scaleY);

            if (drawX >= bitmap.Width || drawY >= bitmap.Height)
            {
                return;
            }

            using var g = Graphics.FromImage(bitmap);
            var hdc = g.GetHdc();
            try
            {
                DrawIconEx(hdc, drawX, drawY, info.hCursor, 0, 0, 0, IntPtr.Zero, DiNormal);
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
        }
        finally
        {
            if (iconInfo.hbmMask != IntPtr.Zero)
            {
                DeleteObject(iconInfo.hbmMask);
            }

            if (iconInfo.hbmColor != IntPtr.Zero)
            {
                DeleteObject(iconInfo.hbmColor);
            }
        }
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
    private static extern bool StretchBlt(
        IntPtr hdcDest, int xDest, int yDest, int nDestWidth, int nDestHeight,
        IntPtr hdcSrc, int xSrc, int ySrc, int nSrcWidth, int nSrcHeight, int rop);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorInfo(ref CursorInfo cursorInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetIconInfo(IntPtr hIcon, out IconInfo iconInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DrawIconEx(
        IntPtr hdc,
        int xLeft,
        int yTop,
        IntPtr hIcon,
        int cxWidth,
        int cyHeight,
        int istepIfAniCur,
        IntPtr hbrFlickerFreeDraw,
        int diFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public Point ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}
