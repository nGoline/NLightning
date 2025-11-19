namespace NLightning.Domain.Channels.Validators.Parameters;

using Money;
using Node.Options;
using Protocol.Payloads;
using Protocol.Tlv;
using Protocol.ValueObjects;
using ValueObjects;

public sealed class ChannelOpenMandatoryValidationParameters
{
    public ChannelTypeTlv? ChannelTypeTlv { get; init; }
    public required LightningMoney CurrentFeeRatePerKw { get; init; }
    public required FeatureOptions NegotiatedFeatures { get; init; }
    public ChainHash? ChainHash { get; init; }
    public LightningMoney? PushAmount { get; init; }
    public LightningMoney? FundingAmount { get; init; }
    public ushort ToSelfDelay { get; init; }
    public uint MaxAcceptedHtlcs { get; init; }
    public LightningMoney? FeeRatePerKw { get; init; }
    public required LightningMoney DustLimitAmount { get; init; }
    public required LightningMoney ChannelReserveAmount { get; init; }
    public ChannelFlags? ChannelFlags { get; init; }

    public static ChannelOpenMandatoryValidationParameters FromOpenChannel1Payload(
        ChannelTypeTlv? channelTypeTlv, LightningMoney currentFeeRatePerKw, FeatureOptions negotiatedFeatures,
        OpenChannel1Payload payload)
    {
        return new ChannelOpenMandatoryValidationParameters
        {
            ChannelTypeTlv = channelTypeTlv,
            CurrentFeeRatePerKw = currentFeeRatePerKw,
            NegotiatedFeatures = negotiatedFeatures,
            ChainHash = payload.ChainHash,
            PushAmount = payload.PushAmount,
            FundingAmount = payload.FundingAmount,
            ToSelfDelay = payload.ToSelfDelay,
            MaxAcceptedHtlcs = payload.MaxAcceptedHtlcs,
            FeeRatePerKw = payload.FeeRatePerKw,
            DustLimitAmount = payload.DustLimitAmount,
            ChannelReserveAmount = payload.ChannelReserveAmount,
            ChannelFlags = payload.ChannelFlags,
        };
    }

    public static ChannelOpenMandatoryValidationParameters FromAcceptChannel1Payload(
        ChannelTypeTlv? channelTypeTlv, LightningMoney feeRateAmountPerKw,
        FeatureOptions negotiatedFeatures, AcceptChannel1Payload payload)
    {
        return new ChannelOpenMandatoryValidationParameters
        {
            ChannelTypeTlv = channelTypeTlv,
            CurrentFeeRatePerKw = feeRateAmountPerKw,
            NegotiatedFeatures = negotiatedFeatures,
            ToSelfDelay = payload.ToSelfDelay,
            MaxAcceptedHtlcs = payload.MaxAcceptedHtlcs,
            DustLimitAmount = payload.DustLimitAmount,
            ChannelReserveAmount = payload.ChannelReserveAmount
        };
    }
}