using MessagePack;

namespace NLightning.Transport.Ipc.Responses;

using Domain.Crypto.ValueObjects;
using Domain.Node;

/// <summary>
/// Response for Connect command
/// </summary>
[MessagePackObject]
public sealed class ConnectPeerIpcResponse
{
    [Key(0)] public CompactPubKey Id { get; init; }
    [Key(1)] public required FeatureSet Features { get; init; }
    [Key(2)] public bool IsInitiator { get; init; }
    [Key(3)] public required string Address { get; init; }
    [Key(4)] public required string Type { get; init; }
    [Key(5)] public uint Port { get; init; }
}