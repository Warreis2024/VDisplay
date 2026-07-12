using System.Drawing;
using System.Drawing.Imaging;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VDisplay.Core.Interop;
using VDisplay.Core.Models;

namespace VDisplay.Service.Capture;

[SupportedOSPlatform("windows")]
public sealed class SharedFrameBridge : IDisposable
{
    private MemoryMappedFile? _layoutMap;
    private MemoryMappedViewAccessor? _layoutView;
    private MemoryMappedFile? _framesMap;
    private MemoryMappedViewAccessor? _framesView;

    public void EnsureCreated()
    {
        _layoutMap ??= MemoryMappedFile.CreateOrOpen(
            SharedMemoryConstants.LayoutMapName,
            Marshal.SizeOf<SharedLayoutHeader>(),
            MemoryMappedFileAccess.ReadWrite);

        _layoutView ??= _layoutMap.CreateViewAccessor(0, Marshal.SizeOf<SharedLayoutHeader>());

        _framesMap ??= MemoryMappedFile.CreateOrOpen(
            SharedMemoryConstants.FramesMapName,
            SharedMemoryConstants.FramesMapSize,
            MemoryMappedFileAccess.ReadWrite);

        _framesView ??= _framesMap.CreateViewAccessor(0, SharedMemoryConstants.FramesMapSize);
    }

    public void WriteLayout(LayoutDefinition layout, bool captureActive)
    {
        EnsureCreated();

        var header = new SharedLayoutHeader
        {
            Version = 1,
            CaptureActive = captureActive ? 1 : 0,
            SourceWidth = (uint)layout.SourceWidth,
            SourceHeight = (uint)layout.SourceHeight,
            MonitorCount = (uint)layout.Regions.Count
        };

        for (var i = 0; i < layout.Regions.Count && i < SharedMemoryConstants.MaxVirtualMonitors; i++)
        {
            var region = layout.Regions[i];
            header.SetVm(i, new SharedVirtualRegion
            {
                SrcX = (uint)region.Source.X,
                SrcY = (uint)region.Source.Y,
                SrcW = (uint)region.Source.Width,
                SrcH = (uint)region.Source.Height,
                DstW = (uint)region.Destination.Width,
                DstH = (uint)region.Destination.Height,
                FrameReady = 0
            });
        }

        _layoutView!.Write(0, ref header);
    }

    public void SetCaptureActive(bool active)
    {
        EnsureCreated();
        _layoutView!.Write(4, active ? 1 : 0);
    }

    public void WriteFrame(int monitorIndex, ReadOnlySpan<byte> bgra, int width, int height)
    {
        if (monitorIndex < 0 || monitorIndex >= SharedMemoryConstants.MaxVirtualMonitors)
        {
            return;
        }

        EnsureCreated();

        var dstW = Math.Min(width, SharedMemoryConstants.MaxFrameWidth);
        var dstH = Math.Min(height, SharedMemoryConstants.MaxFrameHeight);
        var rowPitch = dstW * 4;
        var offset = monitorIndex * SharedMemoryConstants.FrameSize;

        var rowBytes = dstW * 4;
        var srcPitch = width * 4;
        for (var y = 0; y < dstH; y++)
        {
            var srcOffset = y * srcPitch;
            var dstOffset = offset + (y * rowPitch);
            var row = bgra.Slice(srcOffset, rowBytes).ToArray();
            for (var i = 3; i < row.Length; i += 4)
            {
                row[i] = 255;
            }

            _framesView!.WriteArray(dstOffset, row, 0, row.Length);
        }

        var vmOffset = Marshal.OffsetOf<SharedLayoutHeader>("Vm0").ToInt32()
            + monitorIndex * Marshal.SizeOf<SharedVirtualRegion>()
            + Marshal.OffsetOf<SharedVirtualRegion>("FrameReady").ToInt32();
        _layoutView!.Write(vmOffset, 1);
    }

    public bool TryReadFrameBitmap(int monitorIndex, out Bitmap? bitmap)
    {
        bitmap = null;
        if (monitorIndex < 0 || monitorIndex >= SharedMemoryConstants.MaxVirtualMonitors)
        {
            return false;
        }

        EnsureCreated();

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
            var row = new byte[rowBytes];
            _framesView!.ReadArray(offset + (y * rowPitch), row, 0, rowBytes);
            for (var i = 3; i < row.Length; i += 4)
            {
                row[i] = 255;
            }

            Buffer.BlockCopy(row, 0, buffer, y * rowBytes, rowBytes);
        }

        bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    buffer,
                    y * rowBytes,
                    data.Scan0 + (y * data.Stride),
                    rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return true;
    }

    public bool WaitForFrames(int requiredCount, TimeSpan timeout)
    {
        EnsureCreated();
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;

        while (Environment.TickCount64 < deadline)
        {
            _layoutView!.Read(0, out SharedLayoutHeader header);
            if (header.CaptureActive == 0)
            {
                Thread.Sleep(30);
                continue;
            }

            var ready = 0;
            for (var i = 0; i < requiredCount && i < SharedMemoryConstants.MaxVirtualMonitors; i++)
            {
                if (header.GetVm(i).FrameReady != 0)
                {
                    ready++;
                }
            }

            if (ready >= requiredCount)
            {
                return true;
            }

            Thread.Sleep(30);
        }

        return false;
    }

    public void Dispose()
    {
        _layoutView?.Dispose();
        _framesMap?.Dispose();
        _layoutMap?.Dispose();
        _framesView?.Dispose();
    }
}
