using System.Runtime.InteropServices;

namespace VDisplay.Core.Interop;

public static class SharedMemoryConstants
{
    public const string LayoutMapName = "Global\\VDisplay.Layout";
    public const string FramesMapName = "Global\\VDisplay.Frames";
    public const int MaxVirtualMonitors = 4;
    public const int MaxFrameWidth = 1920;
    public const int MaxFrameHeight = 1080;
    public const int FramePitch = MaxFrameWidth * 4;
    public const int FrameSize = FramePitch * MaxFrameHeight;
    public const int FramesMapSize = FrameSize * MaxVirtualMonitors;
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
    public int FrameReady;
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

    public SharedVirtualRegion GetVm(int index) => index switch
    {
        0 => Vm0,
        1 => Vm1,
        2 => Vm2,
        3 => Vm3,
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
        }
    }
}
