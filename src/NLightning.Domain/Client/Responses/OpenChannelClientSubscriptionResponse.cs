namespace NLightning.Domain.Client.Responses;

using Bitcoin.ValueObjects;
using Channels.Enums;
using Channels.ValueObjects;

public class OpenChannelClientSubscriptionResponse
{
    public ChannelId ChannelId { get; }
    public ChannelState ChannelState { get; init; }
    public TxId? TxId { get; init; }
    public uint? Index { get; init; }

    public OpenChannelClientSubscriptionResponse(ChannelId channelId)
    {
        ChannelId = channelId;
    }
}