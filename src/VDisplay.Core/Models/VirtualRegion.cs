namespace VDisplay.Core.Models;

public sealed class VirtualRegion
{
    public int Index { get; set; }
    public int SourceMonitorIndex { get; set; }
    public RegionRect Source { get; set; } = new();
    public RegionRect Destination { get; set; } = new();
}
