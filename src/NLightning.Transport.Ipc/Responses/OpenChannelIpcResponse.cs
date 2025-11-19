using MessagePack;

namespace NLightning.Transport.Ipc.Responses;

using Domain.Bitcoin.ValueObjects;
using Domain.Channels.ValueObjects;
using Domain.Client.Responses;

/// <summary>
/// Response for OpenChannel command
/// </summary>
[MessagePackObject]
public sealed class OpenChannelIpcResponse
{
    [Key(0)] public required SignedTransaction Transaction { get; init; }
    [Key(2)] public uint Index { get; init; }
    [Key(3)] public ChannelId ChannelId { get; init; }

    public static OpenChannelIpcResponse FromClientResponse(OpenChannelClientResponse clientResponse)
    {
        return new OpenChannelIpcResponse
        {
            Transaction = clientResponse.Transaction,
            Index = clientResponse.Index,
            ChannelId = clientResponse.ChannelId
        };
    }
}