using MessagePack;

namespace NLightning.Transport.Ipc.Responses;

using Daemon.Contracts.Control;
using Domain.Crypto.ValueObjects;
using Domain.Node;

/// <summary>
/// Response for Connect command
/// </summary>
[MessagePackObject]
public sealed class ConnectPeerIpcResponse
{
    [Key(0)] public CompactPubKey Id { get; set; }
    [Key(1)] public required FeatureSet Features { get; set; }
    [Key(2)] public bool IsInitiator { get; set; }
    [Key(3)] public required string Address { get; set; }
    [Key(4)] public string Type { get; set; } = string.Empty;
    [Key(5)] public uint Port { get; set; }

    public ConnectPeerResponse ToContractResponse()
    {
        return new ConnectPeerResponse
        {
            Id = Id.ToString(),
            Features = Features.ToString(),
            IsInitiator = IsInitiator,
            Address = Address,
            Type = Type,
            Port = Port
        };
    }
}