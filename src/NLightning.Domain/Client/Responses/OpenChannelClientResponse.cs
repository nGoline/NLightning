namespace NLightning.Domain.Client.Responses;

using Channels.ValueObjects;

public sealed class OpenChannelClientResponse
{
    public ChannelId ChannelId { get; }

    public OpenChannelClientResponse(ChannelId channelId)
    {
        ChannelId = channelId;
    }
}