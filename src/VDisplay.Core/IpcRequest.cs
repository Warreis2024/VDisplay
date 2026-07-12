namespace VDisplay.Core;

public sealed class IpcRequest
{
    public IpcCommand Command { get; set; }
    public string? Payload { get; set; }
}
