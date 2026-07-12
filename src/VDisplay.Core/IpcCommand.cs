namespace VDisplay.Core;

public enum IpcCommand
{
    Ping,
    GetStatus,
    StartDriver,
    StopDriver,
    SetMonitorCount,
    GetMonitors,
    SetLayout,
    GetPhysicalMonitors,
    GetVirtualMonitors,
    SetSourceMonitor,
    StartCapture,
    StopCapture,
    StartVmSplit,
    StopVmSplit,
    StartPhysicalSplit,
    StopPhysicalSplit
}
