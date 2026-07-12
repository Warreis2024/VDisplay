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

    public bool TryReadFrameBitmap(int monitorIndex, out Bitmap? bitmap)
    {
        bitmap = null;
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

        var rowPitch = width * 4;
        var offset = monitorIndex * SharedMemoryConstants.FrameSize;
        var buffer = new byte[width * height * 4];
        var rowBytes = width * 4;

        for (var y = 0; y < height; y++)
        {
            _framesView!.ReadArray(offset + (y * rowPitch), buffer, y * rowBytes, rowBytes);
        }

        for (var i = 3; i < buffer.Length; i += 4)
        {
            buffer[i] = 255;
        }

        bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            if (data.Stride == rowBytes)
            {
                Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    Marshal.Copy(buffer, y * rowBytes, data.Scan0 + (y * data.Stride), rowBytes);
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return true;
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
        _layoutView?.Dispose();
        _framesMap?.Dispose();
        _layoutMap?.Dispose();
        _framesView?.Dispose();
    }
}
