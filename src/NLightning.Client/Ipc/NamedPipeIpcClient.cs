using System.Buffers;
using System.IO.Pipes;
using MessagePack;
using NLightning.Daemon.Contracts.Control;

namespace NLightning.Client.Ipc;

using Daemon.Contracts;
using Transport.Ipc;

public sealed class NamedPipeIpcClient : IControlClient, IAsyncDisposable
{
    private readonly string _namedPipeFilePath;
    private readonly string _cookieFilePath;
    private readonly string? _server;

    public NamedPipeIpcClient(string namedPipeFilePath, string cookieFilePath, string? server = ".")
    {
        _namedPipeFilePath = namedPipeFilePath;
        _cookieFilePath = cookieFilePath;
        _server = server;
    }

    public async Task<NodeInfoResponse> GetNodeInfoAsync(CancellationToken ct = default)
    {
        var req = new NodeInfoRequest();
        var payload = MessagePackSerializer.Serialize(req, cancellationToken: ct);
        var env = new IpcEnvelope
        {
            Version = 1,
            Command = NodeIpcCommand.NodeInfo,
            CorrelationId = Guid.NewGuid(),
            AuthToken = await GetAuthTokenAsync(ct),
            Payload = payload,
            Kind = 0
        };

        var respEnv = await SendAsync(env, ct);
        if (respEnv.Kind != 2)
        {
            var transport =
                MessagePackSerializer.Deserialize<NodeInfoIpcResponse>(respEnv.Payload, cancellationToken: ct);
            return new NodeInfoResponse
            {
                Network = transport.Network,
                BestBlockHash = transport.BestBlockHash,
                BestBlockHeight = transport.BestBlockHeight,
                BestBlockTime = transport.BestBlockTime,
                Implementation = transport.Implementation,
                Version = transport.Version
            };
        }

        var err = MessagePackSerializer.Deserialize<IpcError>(respEnv.Payload, cancellationToken: ct);
        throw new InvalidOperationException($"IPC error {err.Code}: {err.Message}");
    }

    private async Task<IpcEnvelope> SendAsync(IpcEnvelope envelope, CancellationToken ct)
    {
        await using var client =
            new NamedPipeClientStream(_server, _namedPipeFilePath, PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            await client.ConnectAsync(TimeSpan.FromSeconds(2), ct);
        }
        catch (TimeoutException)
        {
            throw new IOException(
                "Could not connect to NLightning node IPC pipe. Ensure the node is running and listening for IPC.");
        }

        // Send request
        var bytes = MessagePackSerializer.Serialize(envelope, cancellationToken: ct);
        var lenPrefix = BitConverter.GetBytes(bytes.Length);
        await client.WriteAsync(lenPrefix, ct);
        await client.WriteAsync(bytes, ct);
        await client.FlushAsync(ct);

        // Read response length
        var header = new byte[4];
        await ReadExactAsync(client, header, ct);
        var respLen = BitConverter.ToInt32(header, 0);
        if (respLen is <= 0 or > 10_000_000)
            throw new IOException("Invalid IPC response length.");

        // Read payload
        var respBuf = ArrayPool<byte>.Shared.Rent(respLen);
        try
        {
            await ReadExactAsync(client, respBuf.AsMemory(0, respLen), ct);
            var env = MessagePackSerializer.Deserialize<IpcEnvelope>(respBuf.AsMemory(0, respLen),
                                                                     cancellationToken: ct);
            return env;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(respBuf);
        }
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

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
        => await ReadExactAsync(stream, buffer.AsMemory(), ct);

    private async Task<string> GetAuthTokenAsync(CancellationToken ct)
    {
        if (!File.Exists(_cookieFilePath))
            throw new IOException(
                "Authentication cookie file not found. Ensure the node is running and the cookie file path is correct.");

        var content = await File.ReadAllTextAsync(_cookieFilePath, ct);
        return content.Trim();
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}