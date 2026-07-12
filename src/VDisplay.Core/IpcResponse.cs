namespace VDisplay.Core;

public sealed class IpcResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Data { get; set; }

    public static IpcResponse Ok(string? data = null, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static IpcResponse Fail(string message) =>
        new() { Success = false, Message = message };
}
