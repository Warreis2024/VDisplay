using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using VDisplay.Core.Models;

namespace VDisplay.Tray;

[SupportedOSPlatform("windows")]
internal sealed class VmFrameSource : IDisposable
{
    private const int PreviewMaxWidth = 960;
    private const int ThumbnailMaxWidth = 128;

    private readonly PhysicalMonitorInfo _monitor;
    private readonly int _sharedIndex;
    private readonly SharedFrameReader? _sharedReader;

    public VmFrameSource(PhysicalMonitorInfo monitor, int sharedIndex, bool useSharedMemory)
    {
        _monitor = monitor;
        _sharedIndex = sharedIndex;
        if (useSharedMemory)
        {
            try
            {
                _sharedReader = new SharedFrameReader();
            }
            catch
            {
                _sharedReader = null;
            }
        }
    }

    public Bitmap? CaptureFrame(bool preview = false) =>
        preview ? CapturePreview() : CaptureFull();

    public Bitmap? CaptureThumbnail() => CapturePreview(ThumbnailMaxWidth);

    private Bitmap? CaptureFull()
    {
        if (TrySharedFrame(out var shared))
        {
            return shared;
        }

        return ScreenCapture.CaptureMonitor(_monitor);
    }

    private Bitmap? CapturePreview(int maxWidth = PreviewMaxWidth)
    {
        // Shared memory = servisin yazdığı VM karesi (DXGI'dan ucuz, aynı çözünürlük).
        if (TrySharedFrame(out var shared) && shared is not null)
        {
            return ScaleToMaxWidth(shared, maxWidth);
        }

        var desktop = ScreenCapture.CaptureMonitorPreview(_monitor, maxWidth);
        if (desktop is not null)
        {
            return desktop;
        }

        return null;
    }

    private bool TrySharedFrame(out Bitmap? bitmap)
    {
        bitmap = null;
        if (_sharedReader?.TryReadFrameBitmap(_sharedIndex, out var shared) != true || shared is null)
        {
            return false;
        }

        // Sahte imleç yok — gerçek Windows imleci yeterli (çift cursor olmasın).
        bitmap = shared;
        return true;
    }

    private static Bitmap ScaleToMaxWidth(Bitmap source, int maxWidth)
    {
        if (source.Width <= maxWidth)
        {
            return source;
        }

        var dstH = Math.Max(1, (int)((double)source.Height * maxWidth / source.Width));
        var scaled = new Bitmap(maxWidth, dstH, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            g.DrawImage(source, 0, 0, maxWidth, dstH);
        }

        source.Dispose();
        return scaled;
    }

    public void Dispose() => _sharedReader?.Dispose();
}
