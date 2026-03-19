using MessagePack;

namespace NLightning.Transport.Ipc.Responses;

using Domain.Bitcoin.ValueObjects;
using Domain.Channels.Enums;
using Domain.Channels.ValueObjects;
using Domain.Client.Responses;

/// <summary>
/// Response for OpenChannelSubscription command
/// </summary>
[MessagePackObject]
public sealed class OpenChannelSubscriptionIpcResponse
{
    [Key(0)] public required ChannelId ChannelId { get; init; }
    [Key(1)] public required ChannelState ChannelState { get; init; }
    [Key(2)] public TxId? TxId { get; init; }
    [Key(3)] public uint? Index { get; init; }

    public static OpenChannelSubscriptionIpcResponse FromClientResponse(
        OpenChannelClientSubscriptionResponse clientResponse)
    {
        return new OpenChannelSubscriptionIpcResponse
        {
            ChannelId = clientResponse.ChannelId,
            ChannelState = clientResponse.ChannelState,
            TxId = clientResponse.TxId,
            Index = clientResponse.Index
        };
    }
}