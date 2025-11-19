namespace NLightning.Domain.Protocol.Interfaces;

using Bitcoin.ValueObjects;
using Channels.ValueObjects;
using Crypto.ValueObjects;

public interface IChannelIdFactory
{
    ChannelId CreateTemporaryChannelId();
    ChannelId CreateV1(TxId fundingTxId, ushort fundingOutputIndex);
    ChannelId CreateV2(CompactPubKey lesserRevocationBasepoint, CompactPubKey greaterRevocationBasepoint);
}