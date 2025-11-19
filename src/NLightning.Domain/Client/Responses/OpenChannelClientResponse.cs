namespace NLightning.Domain.Client.Responses;

using Bitcoin.ValueObjects;
using Channels.ValueObjects;

public sealed class OpenChannelClientResponse
{
    public SignedTransaction Transaction { get; }
    public uint Index { get; }
    public ChannelId ChannelId { get; }

    public OpenChannelClientResponse(SignedTransaction transaction, uint index, ChannelId channelId)
    {
        Transaction = transaction;
        Index = index;
        ChannelId = channelId;
    }
}