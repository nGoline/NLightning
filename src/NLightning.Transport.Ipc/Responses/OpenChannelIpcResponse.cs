using MessagePack;

namespace NLightning.Transport.Ipc.Responses;

using Domain.Channels.ValueObjects;
using Domain.Client.Responses;

/// <summary>
/// Response for OpenChannel command
/// </summary>
[MessagePackObject]
public sealed class OpenChannelIpcResponse
{
    [Key(0)] public required ChannelId ChannelId { get; init; }

    public static OpenChannelIpcResponse FromClientResponse(OpenChannelClientResponse clientResponse)
    {
        return new OpenChannelIpcResponse
        {
            ChannelId = clientResponse.ChannelId
        };
    }
}