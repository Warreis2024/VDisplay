using System.IO.Pipes;

namespace VDisplay.Core;

public sealed class NamedPipeIpcClient : IAsyncDisposable
{
    public async Task<IpcResponse> SendAsync(IpcRequest request, CancellationToken cancellationToken = default)
    {
        await using var pipe = new NamedPipeClientStream(
            ".",
            IpcConstants.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        try
        {
            await pipe.ConnectAsync(3000, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("VDisplay servisine baglanilamadi.", ex);
        }

        await IpcSerializer.WriteAsync(pipe, request, cancellationToken);
        var response = await IpcSerializer.ReadAsync<IpcResponse>(pipe, cancellationToken);
        return response ?? IpcResponse.Fail("Gecersiz yanit alindi.");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
