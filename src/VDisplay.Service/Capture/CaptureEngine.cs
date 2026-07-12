using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using VDisplay.Capture;
using VDisplay.Core.Models;

namespace VDisplay.Service.Capture;

[SupportedOSPlatform("windows")]
public sealed class CaptureEngine : IDisposable
{
    private readonly PhysicalMonitorProvider _monitorProvider;
    private PhysicalMonitorInfo? _sourceMonitor;

    public CaptureEngine(PhysicalMonitorProvider monitorProvider)
    {
        _monitorProvider = monitorProvider;
    }

    public void SetSourceMonitor(int index)
    {
        var monitors = _monitorProvider.GetCaptureSources();
        _sourceMonitor = monitors.FirstOrDefault(m => m.Index == index)
            ?? monitors.FirstOrDefault(m => m.IsPrimary)
            ?? monitors.FirstOrDefault();
    }

    public PhysicalMonitorInfo? SourceMonitor => _sourceMonitor;

    public byte[] CaptureFullFrame(out int width, out int height)
    {
        if (_sourceMonitor is null)
        {
            SetSourceMonitor(0);
        }

        if (_sourceMonitor is null)
        {
            width = 0;
            height = 0;
            return [];
        }

        width = _sourceMonitor.Width;
        height = _sourceMonitor.Height;

        using var bitmap = DxgiDesktopCapture.TryCapture(_sourceMonitor)
            ?? CaptureWithGdi(_sourceMonitor);

        if (bitmap is null)
        {
            return [];
        }

        var bytes = new byte[width * height * 4];
        var rect = new Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            for (var y = 0; y < height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    data.Scan0 + (y * stride),
                    bytes,
                    y * width * 4,
                    width * 4);
            }

            for (var i = 3; i < bytes.Length; i += 4)
            {
                bytes[i] = 255;
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bytes;
    }

    public static byte[] CropBgra(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight, RegionRect crop)
    {
        var dstW = crop.Width;
        var dstH = crop.Height;
        var dst = new byte[dstW * dstH * 4];
        var srcPitch = sourceWidth * 4;
        var dstPitch = dstW * 4;

        for (var y = 0; y < dstH; y++)
        {
            var srcY = crop.Y + y;
            if (srcY < 0 || srcY >= sourceHeight)
            {
                continue;
            }

            var srcOffset = (srcY * srcPitch) + (crop.X * 4);
            var dstOffset = y * dstPitch;
            var rowBytes = Math.Min(dstPitch, source.Length - srcOffset);
            if (rowBytes > 0)
            {
                source.Slice(srcOffset, rowBytes).CopyTo(dst.AsSpan(dstOffset, rowBytes));
            }
        }

        return dst;
    }

    public static byte[] ScaleBgra(ReadOnlySpan<byte> source, int srcW, int srcH, int dstW, int dstH)
    {
        if (srcW == dstW && srcH == dstH)
        {
            return source.ToArray();
        }

        if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0)
        {
            return [];
        }

        var dst = new byte[dstW * dstH * 4];
        for (var y = 0; y < dstH; y++)
        {
            var srcY = Math.Min(srcH - 1, (y * srcH) / dstH);
            for (var x = 0; x < dstW; x++)
            {
                var srcX = Math.Min(srcW - 1, (x * srcW) / dstW);
                var srcIdx = (srcY * srcW + srcX) * 4;
                var dstIdx = (y * dstW + x) * 4;
                dst[dstIdx] = source[srcIdx];
                dst[dstIdx + 1] = source[srcIdx + 1];
                dst[dstIdx + 2] = source[srcIdx + 2];
                dst[dstIdx + 3] = 255;
            }
        }

        return dst;
    }

    private static Bitmap? CaptureWithGdi(PhysicalMonitorInfo monitor)
    {
        if (monitor.Width <= 0 || monitor.Height <= 0)
        {
            return null;
        }

        var bitmap = new Bitmap(monitor.Width, monitor.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                monitor.X,
                monitor.Y,
                0,
                0,
                new Size(monitor.Width, monitor.Height),
                CopyPixelOperation.SourceCopy);
        }

        return bitmap;
    }

    public void Dispose()
    {
    }
}
