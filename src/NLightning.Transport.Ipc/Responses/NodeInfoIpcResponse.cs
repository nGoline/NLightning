using MessagePack;

namespace NLightning.Transport.Ipc.Responses;

using Daemon.Contracts.Control;
using Domain.Crypto.ValueObjects;
using Domain.Protocol.ValueObjects;

/// <summary>
/// Response for NodeInfo (transport-specific DTO for MessagePack).
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

    public NodeInfoResponse ToContractResponse()
    {
        return new NodeInfoResponse
        {
            Network = Network,
            BestBlockHash = BestBlockHash.ToString(),
            BestBlockHeight = BestBlockHeight,
            BestBlockTime = BestBlockTime,
            Implementation = Implementation,
            Version = Version
        };
    }
}