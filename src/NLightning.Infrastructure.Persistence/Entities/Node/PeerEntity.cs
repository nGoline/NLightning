namespace NLightning.Infrastructure.Persistence.Entities.Node;

using Channel;
using Domain.Crypto.ValueObjects;

public class PeerEntity
{
    public required CompactPubKey NodeId { get; set; }
    public required string Host { get; set; }
    public required uint Port { get; set; }
    public required string Type { get; set; }
    public required DateTime LastSeenAt { get; set; }

    public virtual ICollection<ChannelEntity>? Channels { get; set; }

    internal PeerEntity() { }
}