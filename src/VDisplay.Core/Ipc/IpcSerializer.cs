using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace VDisplay.Core;

public static class IpcSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static async Task WriteAsync<T>(PipeStream stream, T message, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(message, Options);
        var bytes = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(bytes.Length);

        await stream.WriteAsync(lengthBytes, cancellationToken);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<T?> ReadAsync<T>(PipeStream stream, CancellationToken cancellationToken = default)
    {
        var lengthBuffer = new byte[4];
        await ReadExactAsync(stream, lengthBuffer, cancellationToken);

        var length = BitConverter.ToInt32(lengthBuffer, 0);
        if (length <= 0 || length > 1024 * 1024)
        {
            return default;
        }

        var buffer = new byte[length];
        await ReadExactAsync(stream, buffer, cancellationToken);

        return JsonSerializer.Deserialize<T>(buffer, Options);
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }
    }
}
