using System.Security.Cryptography;

namespace NLightning.Infrastructure.Protocol.Factories;

using Crypto.Hashes;
using Domain.Bitcoin.ValueObjects;
using Domain.Channels.Constants;
using Domain.Channels.ValueObjects;
using Domain.Crypto.ValueObjects;
using Domain.Protocol.Interfaces;

public class ChannelIdFactory : IChannelIdFactory
{
    public ChannelId CreateTemporaryChannelId()
    {
        return new ChannelId(RandomNumberGenerator.GetBytes(ChannelConstants.ChannelIdLength));
    }

    public ChannelId CreateV1(TxId fundingTxId, ushort fundingOutputIndex)
    {
        Span<byte> channelId = stackalloc byte[32];
        ((ReadOnlySpan<byte>)fundingTxId).CopyTo(channelId);

        // XOR the last 2 bytes with the funding_output_index
        channelId[30] ^= (byte)(fundingOutputIndex >> 8);
        channelId[31] ^= (byte)(fundingOutputIndex & 0xFF);

        return new ChannelId(channelId);
    }

    public ChannelId CreateV2(CompactPubKey lesserRevocationBasepoint, CompactPubKey greaterRevocationBasepoint)
    {
        Span<byte> combined = stackalloc byte[66];
        ((ReadOnlySpan<byte>)lesserRevocationBasepoint).CopyTo(combined);
        ((ReadOnlySpan<byte>)greaterRevocationBasepoint).CopyTo(combined[33..]);

        using var hasher = new Sha256();
        hasher.AppendData(combined);
        Span<byte> hash = stackalloc byte[32];
        hasher.GetHashAndReset(hash);
        return new ChannelId(hash);
    }
}