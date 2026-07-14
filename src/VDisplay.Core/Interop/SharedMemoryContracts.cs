using System.Runtime.InteropServices;

namespace VDisplay.Core.Interop;

public static class SharedMemoryConstants
{
    // Local (session) maps — Global\\ needs SeCreateGlobalPrivilege and can hang without elevation.
    public const string LayoutMapName = "Local\\VDisplay.Layout";
    public const string FramesMapName = "Local\\VDisplay.Frames";
    public const string GpuFramesMapName = "Local\\VDisplay.GpuFrames";
    public const int MaxVirtualMonitors = 10;
    public const int MaxCaptureSources = 10;
    public const int MaxFrameWidth = 1920;
    public const int MaxFrameHeight = 1080;
    public const int FramePitch = MaxFrameWidth * 4;
    public const int FrameSize = FramePitch * MaxFrameHeight;
    public const int FramesMapSize = FrameSize * MaxVirtualMonitors;

    public static string GpuTextureName(int sourceMonitorIndex) =>
        $@"Local\VDisplay.GpuTex.{Math.Clamp(sourceMonitorIndex, 0, MaxCaptureSources - 1)}";
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SharedVirtualRegion
{
    public uint SrcX;
    public uint SrcY;
    public uint SrcW;
    public uint SrcH;
    public uint DstW;
    public uint DstH;
    public int SourceMonitorIndex;
    public int FrameReady;
}

/// <summary>GPU NT shared texture metadata (pixels stay on GPU).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SharedGpuSourceSlot
{
    public long Sequence;
    public long AdapterLuid;
    public long SharedHandle;
    public int Width;
    public int Height;
    public int Ready;
    public int SourceMonitorIndex;
    public int ProducerProcessId;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SharedGpuFramesHeader
{
    public int Version;
    public int CaptureActive;
    public int SourceCount;
    public int Reserved;
    public SharedGpuSourceSlot Slot0;
    public SharedGpuSourceSlot Slot1;
    public SharedGpuSourceSlot Slot2;
    public SharedGpuSourceSlot Slot3;
    public SharedGpuSourceSlot Slot4;
    public SharedGpuSourceSlot Slot5;
    public SharedGpuSourceSlot Slot6;
    public SharedGpuSourceSlot Slot7;
    public SharedGpuSourceSlot Slot8;
    public SharedGpuSourceSlot Slot9;

    public SharedGpuSourceSlot GetSlot(int index) => index switch
    {
        0 => Slot0,
        1 => Slot1,
        2 => Slot2,
        3 => Slot3,
        4 => Slot4,
        5 => Slot5,
        6 => Slot6,
        7 => Slot7,
        8 => Slot8,
        9 => Slot9,
        _ => default
    };

    public void SetSlot(int index, SharedGpuSourceSlot slot)
    {
        switch (index)
        {
            case 0: Slot0 = slot; break;
            case 1: Slot1 = slot; break;
            case 2: Slot2 = slot; break;
            case 3: Slot3 = slot; break;
            case 4: Slot4 = slot; break;
            case 5: Slot5 = slot; break;
            case 6: Slot6 = slot; break;
            case 7: Slot7 = slot; break;
            case 8: Slot8 = slot; break;
            case 9: Slot9 = slot; break;
        }
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SharedLayoutHeader
{
    public int Version;
    public int CaptureActive;
    public uint SourceWidth;
    public uint SourceHeight;
    public uint MonitorCount;
    public SharedVirtualRegion Vm0;
    public SharedVirtualRegion Vm1;
    public SharedVirtualRegion Vm2;
    public SharedVirtualRegion Vm3;
    public SharedVirtualRegion Vm4;
    public SharedVirtualRegion Vm5;
    public SharedVirtualRegion Vm6;
    public SharedVirtualRegion Vm7;
    public SharedVirtualRegion Vm8;
    public SharedVirtualRegion Vm9;

    public SharedVirtualRegion GetVm(int index) => index switch
    {
        0 => Vm0,
        1 => Vm1,
        2 => Vm2,
        3 => Vm3,
        4 => Vm4,
        5 => Vm5,
        6 => Vm6,
        7 => Vm7,
        8 => Vm8,
        9 => Vm9,
        _ => default
    };

    public void SetVm(int index, SharedVirtualRegion region)
    {
        switch (index)
        {
            case 0: Vm0 = region; break;
            case 1: Vm1 = region; break;
            case 2: Vm2 = region; break;
            case 3: Vm3 = region; break;
            case 4: Vm4 = region; break;
            case 5: Vm5 = region; break;
            case 6: Vm6 = region; break;
            case 7: Vm7 = region; break;
            case 8: Vm8 = region; break;
            case 9: Vm9 = region; break;
        }
    }
}
