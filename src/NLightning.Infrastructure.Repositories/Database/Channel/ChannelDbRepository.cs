using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using NLightning.Domain.Protocol.Models;

namespace NLightning.Infrastructure.Repositories.Database.Channel;

using Domain.Bitcoin.Transactions.Outputs;
using Domain.Channels.Enums;
using Domain.Channels.Interfaces;
using Domain.Channels.Models;
using Domain.Channels.ValueObjects;
using Domain.Crypto.Constants;
using Domain.Crypto.Hashes;
using Domain.Crypto.ValueObjects;
using Domain.Money;
using Domain.Serialization.Interfaces;
using Persistence.Contexts;
using Persistence.Entities.Channel;

public class ChannelDbRepository : BaseDbRepository<ChannelEntity>, IChannelDbRepository
{
    private readonly IMessageSerializer _messageSerializer;
    private readonly ISha256 _sha256;

    public ChannelDbRepository(NLightningDbContext context, IMessageSerializer messageSerializer, ISha256 sha256)
        : base(context)
    {
        _messageSerializer = messageSerializer ?? throw new ArgumentNullException(nameof(messageSerializer));
        _sha256 = sha256 ?? throw new ArgumentNullException(nameof(sha256));
    }

    public async Task AddAsync(ChannelModel channelModel)
    {
        var channelEntity = await MapDomainToEntity(channelModel, _messageSerializer);

        Insert(channelEntity);
    }

    public async Task UpdateAsync(ChannelModel channelModel)
    {
        var channelEntity = await MapDomainToEntity(channelModel, _messageSerializer);
        Update(channelEntity);
    }

    public async Task<ChannelModel?> GetByIdAsync(ChannelId channelId)
    {
        var channelEntity = await DbSet
                                 .AsNoTracking()
                                 .Include(c => c.Config)
                                 .Include(c => c.KeySets)
                                 .Include(c => c.Htlcs)
                                 .FirstOrDefaultAsync(c => c.ChannelId == channelId);

        if (channelEntity is null)
            return null;

        return await MapEntityToDomain(channelEntity, _messageSerializer, _sha256);
    }

    public async Task<IEnumerable<ChannelModel>> GetAllAsync()
    {
        var channelEntities = await DbSet
                                   .AsNoTracking()
                                   .Include(c => c.Config)
                                   .Include(c => c.KeySets)
                                   .Include(c => c.Htlcs)
                                   .ToListAsync();

        return await Task.WhenAll(
                   channelEntities.Select(async entity =>
                                              await MapEntityToDomain(entity, _messageSerializer, _sha256)));
    }

    public async Task<IEnumerable<ChannelModel>> GetReadyChannelsAsync()
    {
        byte[] readyStateList =
        [
            (byte)ChannelState.V1FundingCreated,
            (byte)ChannelState.V1FundingSigned,
            (byte)ChannelState.ReadyForThem,
            (byte)ChannelState.ReadyForUs,
            (byte)ChannelState.Open,
            (byte)ChannelState.Closing
        ];

        var channelEntities = await DbSet
                                   .AsNoTracking()
                                   .Include(c => c.Config)
                                   .Include(c => c.KeySets)
                                   .Include(c => c.Htlcs)
                                   .Where(c => readyStateList.Contains(c.State))
                                   .ToListAsync();

        return await Task.WhenAll(
                   channelEntities.Select(async entity =>
                                              await MapEntityToDomain(entity, _messageSerializer, _sha256)));
    }

    public async Task<IEnumerable<ChannelModel?>> GetByPeerIdAsync(CompactPubKey peerNodeId)
    {
        var channelEntities = await DbSet
                                   .AsNoTracking()
                                   .Include(c => c.Config)
                                   .Include(c => c.KeySets)
                                   .Include(c => c.Htlcs)
                                   .Where(c => c.RemoteNodeId.Equals(peerNodeId))
                                   .ToListAsync();

        return await Task.WhenAll(
                   channelEntities.Select(async entity =>
                                              await MapEntityToDomain(entity, _messageSerializer, _sha256)));
    }

    internal static async Task<ChannelEntity> MapDomainToEntity(ChannelModel channelModel,
                                                                IMessageSerializer messageSerializer)
    {
        var config = ChannelConfigDbRepository.MapDomainToEntity(channelModel.ChannelId, channelModel.ChannelConfig);
        ImmutableArray<ChannelKeySetEntity> keySets =
        [
            ChannelKeySetDbRepository.MapDomainToEntity(channelModel.ChannelId, true, channelModel.LocalKeySet),
            ChannelKeySetDbRepository.MapDomainToEntity(channelModel.ChannelId, false, channelModel.RemoteKeySet)
        ];

        var htlcs = new List<Htlc>();
        htlcs.AddRange(GetHtlcsOrNull(channelModel.LocalOfferedHtlcs));
        htlcs.AddRange(GetHtlcsOrNull(channelModel.LocalFulfilledHtlcs));
        htlcs.AddRange(GetHtlcsOrNull(channelModel.LocalOldHtlcs));
        htlcs.AddRange(GetHtlcsOrNull(channelModel.RemoteOfferedHtlcs));
        htlcs.AddRange(GetHtlcsOrNull(channelModel.RemoteFulfilledHtlcs));
        htlcs.AddRange(GetHtlcsOrNull(channelModel.RemoteOldHtlcs));

        List<HtlcEntity>? htlcEntities = null;
        if (htlcs.Count > 0)
        {
            htlcEntities = [];

            foreach (var htlc in htlcs)
                htlcEntities.Add(
                    await HtlcDbRepository.MapDomainToEntityAsync(channelModel.ChannelId, htlc, messageSerializer));
        }

        return new ChannelEntity
        {
            ChannelId = channelModel.ChannelId,

            FundingCreatedAtBlockHeight = channelModel.FundingCreatedAtBlockHeight,
            FundingTxId = channelModel.FundingOutput.TransactionId ?? new byte[CryptoConstants.Sha256HashLen],
            FundingOutputIndex = channelModel.FundingOutput.Index ?? 0,
            FundingAmountSatoshis = channelModel.FundingOutput.Amount.Satoshi,

            IsInitiator = channelModel.IsInitiator,
            RemoteNodeId = channelModel.RemoteNodeId,
            State = (byte)channelModel.State,
            Version = (byte)channelModel.Version,

            LocalBalanceSatoshis = channelModel.LocalBalance.Satoshi,
            RemoteBalanceSatoshis = channelModel.RemoteBalance.Satoshi,

            LocalNextHtlcId = channelModel.LocalNextHtlcId,
            RemoteNextHtlcId = channelModel.RemoteNextHtlcId,
            LocalRevocationNumber = channelModel.LocalRevocationNumber,
            RemoteRevocationNumber = channelModel.RemoteRevocationNumber,
            LastSentSignature = channelModel.LastSentSignature?.Value ?? null,
            LastReceivedSignature = channelModel.LastReceivedSignature?.Value ?? null,

            Config = config,
            KeySets = keySets,
            Htlcs = htlcEntities
        };
    }

    internal static async Task<ChannelModel> MapEntityToDomain(ChannelEntity channelEntity,
                                                               IMessageSerializer messageSerializer, ISha256 sha256)
    {
        if (channelEntity.Config is null)
            throw new InvalidOperationException(
                "Channel config cannot be null when mapping channel entity to domain model.");

        if (channelEntity.KeySets is not { Count: 2 })
            throw new InvalidOperationException(
                "Channel key sets must contain exactly two entries when mapping channel entity to domain model.");

        var localKeySetEntity = channelEntity.KeySets.FirstOrDefault(k => k.IsLocal) ??
                                throw new InvalidOperationException(
                                    "Local key set cannot be null when mapping channel entity to domain model.");
        var remoteKeySetEntity = channelEntity.KeySets.FirstOrDefault(k => !k.IsLocal) ??
                                 throw new InvalidOperationException(
                                     "Remote key set cannot be null when mapping channel entity to domain model.");
        var config = ChannelConfigDbRepository.MapEntityToDomain(channelEntity.Config);
        var localKeySet = ChannelKeySetDbRepository.MapEntityToDomain(localKeySetEntity);
        var remoteKeySet = ChannelKeySetDbRepository.MapEntityToDomain(remoteKeySetEntity);

        var localOfferedHtlcs = new List<Htlc>();
        var localFulfilledHtlcs = new List<Htlc>();
        var localOldHtlcs = new List<Htlc>();
        var remoteOfferedHtlcs = new List<Htlc>();
        var remoteFulfilledHtlcs = new List<Htlc>();
        var remoteOldHtlcs = new List<Htlc>();
        if (channelEntity.Htlcs is { Count: > 0 })
        {
            foreach (var htlc in channelEntity.Htlcs.Where(h => h.State.Equals(HtlcState.Offered)))
            {
                var domainHtlc = await HtlcDbRepository.MapEntityToDomainAsync(htlc, messageSerializer);
                if (htlc.Direction.Equals(HtlcDirection.Outgoing))
                    localOfferedHtlcs.Add(domainHtlc);
                else
                    remoteOfferedHtlcs.Add(domainHtlc);
            }

            foreach (var htlc in channelEntity.Htlcs.Where(h => h.State.Equals(HtlcState.Fulfilled)))
            {
                var domainHtlc = await HtlcDbRepository.MapEntityToDomainAsync(htlc, messageSerializer);
                if (htlc.Direction.Equals(HtlcDirection.Outgoing))
                    localFulfilledHtlcs.Add(domainHtlc);
                else
                    remoteFulfilledHtlcs.Add(domainHtlc);
            }

            byte[] oldStates = [(byte)HtlcState.Expired, (byte)HtlcState.Failed];
            foreach (var htlc in channelEntity.Htlcs.Where(h => oldStates.Contains(h.State)))
            {
                var domainHtlc = await HtlcDbRepository.MapEntityToDomainAsync(htlc, messageSerializer);
                if (htlc.Direction.Equals(HtlcDirection.Outgoing))
                    localOldHtlcs.Add(domainHtlc);
                else
                    remoteOldHtlcs.Add(domainHtlc);
            }
        }

        var fundingOutput = new FundingOutputInfo(LightningMoney.Satoshis(channelEntity.FundingAmountSatoshis),
                                                  localKeySet.FundingCompactPubKey, localKeySet.FundingCompactPubKey)
        {
            Index = channelEntity.FundingOutputIndex,
            TransactionId = channelEntity.FundingTxId
        };

        var commitmentNumber =
            new CommitmentNumber(localKeySet.PaymentCompactBasepoint, remoteKeySet.PaymentCompactBasepoint, sha256,
                                 channelEntity.LocalRevocationNumber + 1);

        var remoteNodeId = channelEntity.RemoteNodeId;

        CompactSignature? lastSentSig = null;
        if (channelEntity.LastSentSignature != null)
            lastSentSig = new CompactSignature(channelEntity.LastSentSignature);

        CompactSignature? lastReceivedSig = null;
        if (channelEntity.LastReceivedSignature != null)
            lastReceivedSig = new CompactSignature(channelEntity.LastReceivedSignature);

        return new ChannelModel(config, channelEntity.ChannelId, commitmentNumber, fundingOutput,
                                channelEntity.IsInitiator, lastSentSig, lastReceivedSig,
                                LightningMoney.Satoshis(channelEntity.LocalBalanceSatoshis), localKeySet,
                                channelEntity.LocalNextHtlcId, channelEntity.LocalRevocationNumber,
                                LightningMoney.Satoshis(channelEntity.RemoteBalanceSatoshis), remoteKeySet,
                                channelEntity.RemoteNextHtlcId, remoteNodeId, channelEntity.RemoteRevocationNumber,
                                (ChannelState)channelEntity.State, (ChannelVersion)channelEntity.Version,
                                localOfferedHtlcs, localFulfilledHtlcs, localOldHtlcs, null, remoteOfferedHtlcs,
                                remoteFulfilledHtlcs, remoteOldHtlcs)
        {
            FundingCreatedAtBlockHeight = channelEntity.FundingCreatedAtBlockHeight
        };
    }

    private static ICollection<Htlc> GetHtlcsOrNull(ICollection<Htlc>? htlcs)
    {
        return htlcs is { Count: > 0 } ? htlcs : [];
    }
}