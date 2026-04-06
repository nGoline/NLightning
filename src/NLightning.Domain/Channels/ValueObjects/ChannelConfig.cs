namespace NLightning.Domain.Channels.ValueObjects;

using Bitcoin.ValueObjects;
using Domain.Enums;
using Money;

public readonly record struct ChannelConfig
{
    public LightningMoney ChannelReserveAmount { get; }
    public LightningMoney LocalDustLimitAmount { get; }
    public LightningMoney FeeRateAmountPerKw { get; }
    public LightningMoney HtlcMinimumAmount { get; }
    public ushort MaxAcceptedHtlcs { get; }
    public LightningMoney MaxHtlcAmountInFlight { get; }
    public uint MinimumDepth { get; }
    public bool OptionAnchorOutputs { get; }
    public LightningMoney RemoteDustLimitAmount { get; }
    public ushort ToSelfDelay { get; }
    public FeatureSupport UseScidAlias { get; }
    public BitcoinScript? LocalUpfrontShutdownScript { get; }
    public BitcoinScript? RemoteShutdownScriptPubKey { get; }

    public ChannelConfig(LightningMoney channelReserveAmount, LightningMoney feeRateAmountPerKw,
                         LightningMoney htlcMinimumAmount, LightningMoney localDustLimitAmount,
                         ushort maxAcceptedHtlcs, LightningMoney maxHtlcAmountInFlight, uint minimumDepth,
                         bool optionAnchorOutputs, LightningMoney remoteDustLimitAmount, ushort toSelfDelay,
                         FeatureSupport useScidAlias, BitcoinScript? localUpfrontShutdownScript = null,
                         BitcoinScript? remoteShutdownScriptPubKey = null)
    {
        ChannelReserveAmount = channelReserveAmount;
        FeeRateAmountPerKw = feeRateAmountPerKw;
        HtlcMinimumAmount = htlcMinimumAmount;
        LocalDustLimitAmount = localDustLimitAmount;
        MaxAcceptedHtlcs = maxAcceptedHtlcs;
        MaxHtlcAmountInFlight = maxHtlcAmountInFlight;
        MinimumDepth = minimumDepth;
        OptionAnchorOutputs = optionAnchorOutputs;
        RemoteDustLimitAmount = remoteDustLimitAmount;
        ToSelfDelay = toSelfDelay;
        UseScidAlias = useScidAlias;
        LocalUpfrontShutdownScript = localUpfrontShutdownScript;
        RemoteShutdownScriptPubKey = remoteShutdownScriptPubKey;
    }
}