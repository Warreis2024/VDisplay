namespace VDisplay.Core.Models;

public sealed class LayoutDefinition
{
    public LayoutType LayoutType { get; set; }
    public int SourceWidth { get; set; }
    public int SourceHeight { get; set; }
    public List<VirtualRegion> Regions { get; set; } = [];
}
