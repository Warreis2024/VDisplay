namespace VDisplay.Core.Models;

public sealed class PhysicalMonitorInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsPrimary { get; set; }

    public string VmLabel(int vmNumber) => $"VM {vmNumber}";
}
