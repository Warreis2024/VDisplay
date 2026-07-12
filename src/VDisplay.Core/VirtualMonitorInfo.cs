namespace VDisplay.Core;

public sealed class VirtualMonitorInfo
{
    public int Index { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; }
    public bool IsActive { get; set; }
    public string Name { get; set; } = string.Empty;
}
