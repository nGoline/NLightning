using System.Buffers;
using MessagePack;

namespace NLightning.Daemon.Services.Ipc;

using Daemon.Ipc.Interfaces;
using Transport.Ipc;

/// <summary>
/// Length-prefixed MessagePack framing for IpcEnvelope.
/// </summary>
public sealed class LengthPrefixedIpcFraming : IIpcFraming
{
    public async Task<IpcEnvelope> ReadAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[4];
        await ReadExactAsync(stream, header, ct);
        var len = BitConverter.ToInt32(header, 0);
        if (len is <= 0 or > 10_000_000) throw new IOException("Invalid IPC frame length.");

        var buffer = ArrayPool<byte>.Shared.Rent(len);
        try
        {
            await ReadExactAsync(stream, buffer.AsMemory(0, len), ct);
            return MessagePackSerializer.Deserialize<IpcEnvelope>(buffer.AsMemory(0, len), cancellationToken: ct);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async Task WriteAsync(Stream stream, IpcEnvelope envelope, CancellationToken ct)
    {
        var payload = MessagePackSerializer.Serialize(envelope, cancellationToken: ct);
        var len = BitConverter.GetBytes(payload.Length);
        await stream.WriteAsync(len, ct);
        await stream.WriteAsync(payload, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task ReadExactAsync(Stream stream, Memory<byte> buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[total..], ct);
            if (read == 0) throw new EndOfStreamException();
            total += read;
        }
    }
}