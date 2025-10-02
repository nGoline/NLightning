using MessagePack;

namespace NLightning.Transport.Ipc;

/// <summary>
/// Commands supported by the IPC protocol.
/// </summary>
public enum NodeIpcCommand
{
    // Reserve 0 for unknown
    Unknown = 0,
    NodeInfo = 1,
}

/// <summary>
/// Envelope for all IPC messages, request and response, encoded with MessagePack.
/// </summary>
[MessagePackObject]
public sealed class IpcEnvelope
{
    [Key(0)] public int Version { get; init; } = 1;
    [Key(1)] public NodeIpcCommand Command { get; init; }

    [Key(2)] public Guid CorrelationId { get; init; } = Guid.NewGuid();

    // Auth token derived from a local cookie file (only accessible locally) to secure the channel
    [Key(3)] public string? AuthToken { get; init; }

    // Raw payload serialized with MessagePack separately for the specific request/response type
    [Key(4)] public byte[] Payload { get; init; } = Array.Empty<byte>();

    // 0 = request, 1 = response, 2 = error
    [Key(5)] public byte Kind { get; init; } = 0;
}

/// <summary>
/// Empty request for NodeInfo.
/// </summary>
[MessagePackObject]
public readonly struct NodeInfoRequest
{
}

/// <summary>
/// Response for NodeInfo (transport-specific DTO for MessagePack).
/// </summary>
[MessagePackObject]
public sealed class NodeInfoIpcResponse
{
    [Key(0)] public string Network { get; init; } = string.Empty;
    [Key(1)] public string BestBlockHash { get; init; } = string.Empty;
    [Key(2)] public long BestBlockHeight { get; init; }
    [Key(3)] public DateTimeOffset? BestBlockTime { get; init; }
    [Key(4)] public string? Implementation { get; init; } = "NLightning";
    [Key(5)] public string? Version { get; init; }
}

/// <summary>
/// Error payload
/// </summary>
[MessagePackObject]
public sealed class IpcError
{
    [Key(0)] public string Code { get; init; } = string.Empty;
    [Key(1)] public string Message { get; init; } = string.Empty;
}