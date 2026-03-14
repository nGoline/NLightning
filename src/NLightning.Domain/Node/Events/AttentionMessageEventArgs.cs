namespace NLightning.Domain.Node.Events;

using Channels.ValueObjects;
using Crypto.ValueObjects;

public class AttentionMessageEventArgs : EventArgs
{
    public string Message { get; }
    public CompactPubKey PeerPubKey { get; }
    public ChannelId? ChannelId { get; }

    public AttentionMessageEventArgs(string message, CompactPubKey peerPubKey, ChannelId? channelId = null)
    {
        Message = message;
        PeerPubKey = peerPubKey;
        ChannelId = channelId;
    }
}