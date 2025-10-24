using MessagePack;

namespace NLightning.Transport.Ipc;

/// <summary>
/// Envelope for all IPC messages, request and response, encoded with MessagePack.
/// </summary>
[MessagePackObject]
public sealed class IpcEnvelope
{
    [Key(0)] public int Version { get; set; } = 1;
    [Key(1)] public NodeIpcCommand Command { get; init; }

    [Key(2)] public Guid CorrelationId { get; set; } = Guid.NewGuid();

    // Auth token derived from a local cookie file (only accessible locally) to secure the channel
    [Key(3)] public string? AuthToken { get; init; }

    // Raw payload serialized with MessagePack separately for the specific request/response type
    [Key(4)] public byte[] Payload { get; set; } = Array.Empty<byte>();

    // 0 = request, 1 = response, 2 = error
    [Key(5)] public IpcEnvelopeKind Kind { get; init; }
}