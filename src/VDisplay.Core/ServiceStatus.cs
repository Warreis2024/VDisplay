using VDisplay.Core.Models;

namespace VDisplay.Core;

public sealed class ServiceStatus
{
    public bool DriverRunning { get; set; }
    public bool CaptureRunning { get; set; }
    public bool PhysicalSplitRunning { get; set; }
    public int MonitorCount { get; set; }
    public int SourceMonitorIndex { get; set; }
    public LayoutType CurrentLayout { get; set; }
    public LayoutDefinition? ActiveLayout { get; set; }
    public List<VirtualMonitorInfo> Monitors { get; set; } = [];
    public ulong FramesCaptured { get; set; }
}
