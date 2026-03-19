using MessagePack;

namespace NLightning.Transport.Ipc.Requests;

using Domain.Channels.ValueObjects;
using Domain.Client.Requests;

/// <summary>
/// Empty request for OpenChannelSubscription.
/// </summary>
[MessagePackObject]
public sealed class OpenChannelSubscriptionIpcRequest
{
    [Key(0)] public required ChannelId ChannelId { get; init; }

    public OpenChannelClientSubscriptionRequest ToClientRequest()
    {
        return new OpenChannelClientSubscriptionRequest(ChannelId);
    }
}