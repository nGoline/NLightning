using NLightning.Domain.Bitcoin.ValueObjects;
using NLightning.Domain.Channels.Interfaces;
using NLightning.Domain.Channels.ValueObjects;
using NLightning.Domain.Enums;
using NLightning.Domain.Money;
using NLightning.Infrastructure.Persistence.Contexts;
using NLightning.Infrastructure.Persistence.Entities.Channel;

namespace NLightning.Infrastructure.Repositories.Database.Channel;

public class ChannelConfigDbRepository(NLightningDbContext context)
    : BaseDbRepository<ChannelConfigEntity>(context), IChannelConfigDbRepository
{
    public void Add(ChannelId channelId, ChannelConfig config)
    {
        var configEntity = MapDomainToEntity(channelId, config);
        Insert(configEntity);
    }

    public void Update(ChannelId channelId, ChannelConfig config)
    {
        var configEntity = MapDomainToEntity(channelId, config);
        base.Update(configEntity);
    }

    public Task DeleteAsync(ChannelId channelId)
    {
        return DeleteByIdAsync(channelId);
    }

    public async Task<ChannelConfig?> GetByChannelIdAsync(ChannelId channelId)
    {
        var configEntity = await GetByIdAsync(channelId);

        return configEntity == null ? null : MapEntityToDomain(configEntity);
    }

    internal static ChannelConfigEntity MapDomainToEntity(ChannelId channelId, ChannelConfig config)
    {
        return new ChannelConfigEntity
        {
            ChannelId = channelId,
            ChannelReserveAmountSats = config.ChannelReserveAmount?.Satoshi,
            FeeRatePerKwSatoshis = config.FeeRateAmountPerKw.Satoshi,
            HtlcMinimumMsat = config.HtlcMinimumAmount.MilliSatoshi,
            LocalDustLimitAmountSats = config.LocalDustLimitAmount.Satoshi,
            LocalUpfrontShutdownScript = config.LocalUpfrontShutdownScript,
            MaxAcceptedHtlcs = config.MaxAcceptedHtlcs,
            MaxHtlcAmountInFlight = config.MaxHtlcAmountInFlight.MilliSatoshi,
            MinimumDepth = config.MinimumDepth,
            OptionAnchorOutputs = config.OptionAnchorOutputs,
            RemoteDustLimitAmountSats = config.RemoteDustLimitAmount.Satoshi,
            RemoteUpfrontShutdownScript = config.RemoteShutdownScriptPubKey,
            ToSelfDelay = config.ToSelfDelay,
            UseScidAlias = (byte)config.UseScidAlias
        };
    }

    internal static ChannelConfig MapEntityToDomain(ChannelConfigEntity entity)
    {
        LightningMoney? channelReserveAmount = null;
        if (entity.ChannelReserveAmountSats.HasValue)
            channelReserveAmount = LightningMoney.Satoshis(entity.ChannelReserveAmountSats.Value);

        BitcoinScript? localUpfrontShutdownScript = null;
        if (entity.LocalUpfrontShutdownScript is not null)
            localUpfrontShutdownScript = entity.LocalUpfrontShutdownScript;

        BitcoinScript? remoteUpfrontShutdownScript = null;
        if (entity.RemoteUpfrontShutdownScript is not null)
            remoteUpfrontShutdownScript = entity.RemoteUpfrontShutdownScript;

        return new ChannelConfig(channelReserveAmount ?? LightningMoney.Zero,
                                 LightningMoney.Satoshis(entity.FeeRatePerKwSatoshis),
                                 LightningMoney.MilliSatoshis(entity.HtlcMinimumMsat),
                                 LightningMoney.Satoshis(entity.LocalDustLimitAmountSats), entity.MaxAcceptedHtlcs,
                                 LightningMoney.MilliSatoshis(entity.MaxHtlcAmountInFlight), entity.MinimumDepth,
                                 entity.OptionAnchorOutputs, LightningMoney.Satoshis(entity.RemoteDustLimitAmountSats),
                                 entity.ToSelfDelay, (FeatureSupport)entity.UseScidAlias, localUpfrontShutdownScript,
                                 remoteUpfrontShutdownScript
        );
    }
}