using MessagePack;

namespace NLightning.Transport.Ipc.Responses;

using Domain.Crypto.ValueObjects;
using Domain.Node;

/// <summary>
/// Response for Peer Info command
/// </summary>
[MessagePackObject]
public sealed class PeerInfoIpcResponse
{
    [Key(0)] public CompactPubKey Id { get; init; }
    [Key(1)] public bool Connected { get; init; }
    [Key(2)] public uint ChannelQty { get; init; }
    [Key(3)] public required string Address { get; init; }
    [Key(4)] public required FeatureSet Features { get; init; }
}