namespace VDisplay.Core;

public enum PhysicalSplitMode
{
    /// <summary>Fiziksel sol yari + sanal sag yari (2 VM).</summary>
    HybridPhysicalVirtual,

    /// <summary>Her fiziksel ekran 2 sanal bolge (4 VM).</summary>
    AllVirtualQuadrants
}
