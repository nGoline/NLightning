using MessagePack;

namespace NLightning.Transport.Ipc.Requests;

using Domain.Node.ValueObjects;

/// <summary>
/// Request for Connect command
/// </summary>
[MessagePackObject]
public sealed class ConnectPeerIpcRequest
{
    [Key(0)] public required PeerAddressInfo Address { get; init; } // {pubkey}@{ip}:{port}
}