namespace NLightning.Domain.Client.Requests;

using Channels.ValueObjects;

public class OpenChannelClientSubscriptionRequest
{
    public ChannelId ChannelId { get; }

    public OpenChannelClientSubscriptionRequest(ChannelId channelId)
    {
        ChannelId = channelId;
    }
}