using System.Drawing;
using System.Drawing.Imaging;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VDisplay.Core.Interop;

namespace VDisplay.Tray;

[SupportedOSPlatform("windows")]
internal sealed class SharedFrameReader : IDisposable
{
    private MemoryMappedFile? _layoutMap;
    private MemoryMappedViewAccessor? _layoutView;
    private MemoryMappedFile? _framesMap;
    private MemoryMappedViewAccessor? _framesView;

    private byte[]? _buffer;
    private Bitmap? _bitmap;
    private int _width;
    private int _height;

    /// <summary>Son başarılı kare. Caller Dispose etmemeli.</summary>
    public Bitmap? CurrentBitmap => _bitmap;

    /// <summary>
    /// MMF'den kareyi iç Bitmap'e yazar (reuse). true ise <see cref="CurrentBitmap"/> günceldir.
    /// </summary>
    public bool TryUpdateFrame(int monitorIndex)
    {
        if (monitorIndex < 0 || monitorIndex >= SharedMemoryConstants.MaxVirtualMonitors)
        {
            return false;
        }

        try
        {
            EnsureOpen();
        }
        catch
        {
            return false;
        }

        _layoutView!.Read(0, out SharedLayoutHeader header);
        if (header.CaptureActive == 0)
        {
            return false;
        }

        var region = header.GetVm(monitorIndex);
        if (region.FrameReady == 0)
        {
            return false;
        }

        var width = (int)region.DstW;
        var height = (int)region.DstH;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        width = Math.Min(width, SharedMemoryConstants.MaxFrameWidth);
        height = Math.Min(height, SharedMemoryConstants.MaxFrameHeight);

        var rowBytes = width * 4;
        var byteCount = rowBytes * height;
        var offset = monitorIndex * SharedMemoryConstants.FrameSize;

        EnsureBuffer(byteCount);
        EnsureBitmap(width, height);

        // Pitch == width*4 (WriteFrame ile aynı) → tek blok okuma
        _framesView!.ReadArray(offset, _buffer!, 0, byteCount);
        for (var i = 3; i < byteCount; i += 4)
        {
            _buffer![i] = 255;
        }

        var rect = new Rectangle(0, 0, width, height);
        var data = _bitmap!.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            if (data.Stride == rowBytes)
            {
                Marshal.Copy(_buffer!, 0, data.Scan0, byteCount);
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    Marshal.Copy(_buffer!, y * rowBytes, data.Scan0 + (y * data.Stride), rowBytes);
                }
            }
        }
        finally
        {
            _bitmap.UnlockBits(data);
        }

        return true;
    }

    /// <summary>Geriye uyum: her çağrıda yeni Bitmap (caller Dispose eder).</summary>
    public bool TryReadFrameBitmap(int monitorIndex, out Bitmap? bitmap)
    {
        bitmap = null;
        if (!TryUpdateFrame(monitorIndex) || _bitmap is null)
        {
            return false;
        }

        bitmap = (Bitmap)_bitmap.Clone();
        return true;
    }

    private void EnsureBuffer(int byteCount)
    {
        if (_buffer is null || _buffer.Length < byteCount)
        {
            _buffer = new byte[byteCount];
        }
    }

    private void EnsureBitmap(int width, int height)
    {
        if (_bitmap is not null && _width == width && _height == height)
        {
            return;
        }

        _bitmap?.Dispose();
        _bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        _width = width;
        _height = height;
    }

    private void EnsureOpen()
    {
        _layoutMap ??= MemoryMappedFile.OpenExisting(
            SharedMemoryConstants.LayoutMapName,
            MemoryMappedFileRights.Read);

        _layoutView ??= _layoutMap.CreateViewAccessor(0, Marshal.SizeOf<SharedLayoutHeader>(), MemoryMappedFileAccess.Read);

        _framesMap ??= MemoryMappedFile.OpenExisting(
            SharedMemoryConstants.FramesMapName,
            MemoryMappedFileRights.Read);

        _framesView ??= _framesMap.CreateViewAccessor(0, SharedMemoryConstants.FramesMapSize, MemoryMappedFileAccess.Read);
    }

    public void Dispose()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        _buffer = null;
        _layoutView?.Dispose();
        _framesMap?.Dispose();
        _layoutMap?.Dispose();
        _framesView?.Dispose();
    }
}
