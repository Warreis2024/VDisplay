using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VDisplay.Core.Interop;

namespace VDisplay.Service.Capture;

[SupportedOSPlatform("windows")]
public sealed class SharedGpuFrameBridge : IDisposable
{
    private MemoryMappedFile? _map;
    private MemoryMappedViewAccessor? _view;

    public void EnsureCreated()
    {
        _map ??= MemoryMappedFile.CreateOrOpen(
            SharedMemoryConstants.GpuFramesMapName,
            Marshal.SizeOf<SharedGpuFramesHeader>(),
            MemoryMappedFileAccess.ReadWrite);

        _view ??= _map.CreateViewAccessor(0, Marshal.SizeOf<SharedGpuFramesHeader>());
    }

    public void SetCaptureActive(bool active)
    {
        EnsureCreated();
        _view!.Write(4, active ? 1 : 0);
    }

    public void Publish(
        int sourceMonitorIndex,
        long adapterLuid,
        long sharedHandle,
        int width,
        int height,
        long sequence)
    {
        if (sourceMonitorIndex < 0 || sourceMonitorIndex >= SharedMemoryConstants.MaxCaptureSources)
        {
            return;
        }

        EnsureCreated();
        _view!.Read(0, out SharedGpuFramesHeader header);
        header.Version = 1;
        header.CaptureActive = 1;
        header.SourceCount = Math.Max(header.SourceCount, sourceMonitorIndex + 1);

        header.SetSlot(sourceMonitorIndex, new SharedGpuSourceSlot
        {
            Sequence = sequence,
            AdapterLuid = adapterLuid,
            SharedHandle = sharedHandle,
            Width = width,
            Height = height,
            Ready = sharedHandle != 0 ? 1 : 0,
            SourceMonitorIndex = sourceMonitorIndex,
            ProducerProcessId = Environment.ProcessId
        });

        _view.Write(0, ref header);
    }

    public void Dispose()
    {
        _view?.Dispose();
        _map?.Dispose();
    }
}
