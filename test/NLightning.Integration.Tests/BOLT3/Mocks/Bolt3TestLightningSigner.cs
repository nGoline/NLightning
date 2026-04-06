using Microsoft.Extensions.Logging;
using NBitcoin;
using NLightning.Tests.Utils.Vectors;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

namespace NLightning.Integration.Tests.BOLT3.Mocks;

using Domain.Bitcoin.Interfaces;
using Domain.Bitcoin.ValueObjects;
using Domain.Channels.ValueObjects;
using Domain.Node.Options;
using Infrastructure.Bitcoin.Builders;
using Infrastructure.Bitcoin.Signers;
using CompactSignature = Domain.Crypto.ValueObjects.CompactSignature;

public class Bolt3TestLightningSigner : LocalLightningSigner, ILightningSigner
{
    public Bolt3TestLightningSigner(NodeOptions nodeOptions, ILogger<LocalLightningSigner> logger)
        : base(new FundingOutputBuilder(), null, logger, nodeOptions, null, null)
    {
    }

    public new ChannelBasepoints GetChannelBasepoints(uint channelKeyIndex)
    {
        return new ChannelBasepoints();
    }

    public new ChannelBasepoints GetChannelBasepoints(ChannelId channelId)
    {
        return new ChannelBasepoints();
    }

    public new void RegisterChannel(ChannelId channelId, ChannelSigningInfo signingInfo)
    {
        base.RegisterChannel(channelId, signingInfo);
    }

    public new void ValidateSignature(ChannelId channelId, CompactSignature signature,
                                      SignedTransaction unsignedTransaction)
    {
        base.ValidateSignature(channelId, signature, unsignedTransaction);
    }

    protected override Key GenerateFundingPrivateKey(uint channelKeyIndex)
    {
        return new Key(Bolt3AppendixCVectors.NodeAFundingPrivkey.ToBytes());
    }
}