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
    private Bitmap? _scaledBitmap;

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

    /// <summary>
    /// ownedByCaller=false → SharedFrameReader / ölçek buffer'ı; Dispose etme, sadece Invalidate.
    /// </summary>
    public bool TryCaptureFrame(bool preview, out Bitmap? bitmap, out bool ownedByCaller)
    {
        bitmap = null;
        ownedByCaller = true;

        if (preview)
        {
            return TryCapturePreview(PreviewMaxWidth, out bitmap, out ownedByCaller);
        }

        if (TrySharedReusable(out bitmap))
        {
            ownedByCaller = false;
            return true;
        }

        bitmap = ScreenCapture.CaptureMonitor(_monitor);
        return bitmap is not null;
    }

    public bool TryCaptureThumbnail(out Bitmap? bitmap, out bool ownedByCaller) =>
        TryCapturePreview(ThumbnailMaxWidth, out bitmap, out ownedByCaller);

    public Bitmap? CaptureFrame(bool preview = false) =>
        TryCaptureFrame(preview, out var bitmap, out _) ? bitmap : null;

    public Bitmap? CaptureThumbnail() =>
        TryCaptureThumbnail(out var bitmap, out _) ? bitmap : null;

    private bool TryCapturePreview(int maxWidth, out Bitmap? bitmap, out bool ownedByCaller)
    {
        bitmap = null;
        ownedByCaller = true;

        if (TrySharedReusable(out var shared) && shared is not null)
        {
            bitmap = ScaleToMaxWidthReuse(shared, maxWidth);
            ownedByCaller = false;
            return true;
        }

        bitmap = ScreenCapture.CaptureMonitorPreview(_monitor, maxWidth);
        return bitmap is not null;
    }

    private bool TrySharedReusable(out Bitmap? bitmap)
    {
        bitmap = null;
        if (_sharedReader?.TryUpdateFrame(_sharedIndex) != true)
        {
            return false;
        }

        bitmap = _sharedReader.CurrentBitmap;
        return bitmap is not null;
    }

    private Bitmap ScaleToMaxWidthReuse(Bitmap source, int maxWidth)
    {
        if (source.Width <= maxWidth)
        {
            return source;
        }

        var dstH = Math.Max(1, (int)((double)source.Height * maxWidth / source.Width));
        if (_scaledBitmap is null || _scaledBitmap.Width != maxWidth || _scaledBitmap.Height != dstH)
        {
            _scaledBitmap?.Dispose();
            _scaledBitmap = new Bitmap(maxWidth, dstH, PixelFormat.Format32bppArgb);
        }

        using (var g = Graphics.FromImage(_scaledBitmap))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            g.DrawImage(source, 0, 0, maxWidth, dstH);
        }

        return _scaledBitmap;
    }

    public void Dispose()
    {
        _scaledBitmap?.Dispose();
        _scaledBitmap = null;
        _sharedReader?.Dispose();
    }
}
