using MessagePack;

namespace NLightning.Transport.Ipc.Responses;

using Domain.Crypto.ValueObjects;
using Domain.Protocol.ValueObjects;

/// <summary>
/// Response for NodeInfo command
/// </summary>
[MessagePackObject]
public sealed class NodeInfoIpcResponse
{
    [Key(0)] public BitcoinNetwork Network { get; init; }
    [Key(1)] public Hash BestBlockHash { get; init; }
    [Key(2)] public long BestBlockHeight { get; init; }
    [Key(3)] public DateTimeOffset? BestBlockTime { get; init; }
    [Key(4)] public string? Implementation { get; set; } = "NLightning";
    [Key(5)] public string? Version { get; init; }
}