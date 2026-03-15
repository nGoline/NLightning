namespace NLightning.Domain.Client.Responses;

using Bitcoin.ValueObjects;
using Channels.ValueObjects;

public sealed class OpenChannelClientResponse
{
    public TxId TxId { get; }
    public ushort Index { get; }
    public ChannelId ChannelId { get; }

    public OpenChannelClientResponse(TxId txId, ushort index, ChannelId channelId)
    {
        TxId = txId;
        Index = index;
        ChannelId = channelId;
    }
}