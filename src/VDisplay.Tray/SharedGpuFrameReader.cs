using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VDisplay.Core.Interop;

namespace VDisplay.Tray;

[SupportedOSPlatform("windows")]
internal sealed class SharedGpuFrameReader : IDisposable
{
    private MemoryMappedFile? _gpuMap;
    private MemoryMappedViewAccessor? _gpuView;
    private MemoryMappedFile? _layoutMap;
    private MemoryMappedViewAccessor? _layoutView;

    public bool TryReadSourceSlot(int sourceMonitorIndex, out SharedGpuSourceSlot slot)
    {
        slot = default;
        if (sourceMonitorIndex < 0 || sourceMonitorIndex >= SharedMemoryConstants.MaxCaptureSources)
        {
            return false;
        }

        try
        {
            EnsureGpuOpen();
        }
        catch
        {
            return false;
        }

        _gpuView!.Read(0, out SharedGpuFramesHeader header);
        if (header.CaptureActive == 0)
        {
            return false;
        }

        slot = header.GetSlot(sourceMonitorIndex);
        return slot.Ready != 0
            && slot.SharedHandle != 0
            && slot.Width > 0
            && slot.Height > 0
            && slot.Sequence > 0;
    }

    public bool TryReadVmRegion(int vmIndex, out SharedVirtualRegion region, out uint sourceWidth, out uint sourceHeight)
    {
        region = default;
        sourceWidth = 0;
        sourceHeight = 0;
        if (vmIndex < 0 || vmIndex >= SharedMemoryConstants.MaxVirtualMonitors)
        {
            return false;
        }

        try
        {
            EnsureLayoutOpen();
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

        region = header.GetVm(vmIndex);
        sourceWidth = header.SourceWidth;
        sourceHeight = header.SourceHeight;
        return region.SrcW > 0 && region.SrcH > 0;
    }

    private void EnsureGpuOpen()
    {
        _gpuMap ??= MemoryMappedFile.OpenExisting(
            SharedMemoryConstants.GpuFramesMapName,
            MemoryMappedFileRights.Read);
        _gpuView ??= _gpuMap.CreateViewAccessor(0, Marshal.SizeOf<SharedGpuFramesHeader>(), MemoryMappedFileAccess.Read);
    }

    private void EnsureLayoutOpen()
    {
        _layoutMap ??= MemoryMappedFile.OpenExisting(
            SharedMemoryConstants.LayoutMapName,
            MemoryMappedFileRights.Read);
        _layoutView ??= _layoutMap.CreateViewAccessor(0, Marshal.SizeOf<SharedLayoutHeader>(), MemoryMappedFileAccess.Read);
    }

    public void Dispose()
    {
        _gpuView?.Dispose();
        _gpuMap?.Dispose();
        _layoutView?.Dispose();
        _layoutMap?.Dispose();
    }
}
