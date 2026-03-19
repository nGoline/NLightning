using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace NLightning.Application.Channels.Handlers;

using Domain.Channels.Enums;
using Domain.Channels.Interfaces;
using Domain.Channels.Models;
using Domain.Crypto.ValueObjects;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Node.Options;
using Domain.Persistence.Interfaces;
using Domain.Protocol.Interfaces;
using Domain.Protocol.Messages;
using Interfaces;

public class ChannelReadyMessageHandler : IChannelMessageHandler<ChannelReadyMessage>
{
    private readonly IChannelMemoryRepository _channelMemoryRepository;
    private readonly ILogger<ChannelReadyMessageHandler> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public ChannelReadyMessageHandler(IChannelMemoryRepository channelMemoryRepository,
                                      ILogger<ChannelReadyMessageHandler> logger, IUnitOfWork unitOfWork)
    {
        _channelMemoryRepository = channelMemoryRepository;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<IChannelMessage?> HandleAsync(ChannelReadyMessage message, ChannelState currentState,
                                                    FeatureOptions negotiatedFeatures, CompactPubKey peerPubKey)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("Processing ChannelReadyMessage with ChannelId: {ChannelId} from Peer: {PeerPubKey}",
                             message.Payload.ChannelId, peerPubKey);

        var payload = message.Payload;

        if (currentState is not (ChannelState.V1FundingSigned
                              or ChannelState.ReadyForThem
                              or ChannelState.ReadyForUs
                              or ChannelState.Open))
            throw new ChannelErrorException(
                $"Unexpected ChannelReady message in state {Enum.GetName(currentState)}",
                payload.ChannelId,
                "Protocol violation: unexpected ChannelReady message");

        // Check if there's a channel for this peer
        if (!_channelMemoryRepository.TryGetChannel(payload.ChannelId, out var channel))
            throw new ChannelErrorException("Channel not found", payload.ChannelId,
                                            "This channel is not ready to be opened");

        var mustUseScidAlias = channel.ChannelConfig.UseScidAlias > FeatureSupport.No;
        if (mustUseScidAlias && message.ShortChannelIdTlv is null)
            throw new ChannelWarningException("No ShortChannelIdTlv provided",
                                              payload.ChannelId,
                                              "This channel requires a ShortChannelIdTlv to be provided");

        // Store their new per-commitment point
        if (channel.RemoteKeySet!.CurrentPerCommitmentIndex == 0)
            channel.RemoteKeySet.UpdatePerCommitmentPoint(payload.SecondPerCommitmentPoint);

        switch (currentState)
        {
            case ChannelState.Open or ChannelState.ReadyForThem: // Handle ScidAlias
                {
                    if (mustUseScidAlias)
                    {
                        if (ShouldReplaceAlias())
                        {
                            var oldAlias = channel.RemoteAlias;
                            channel.RemoteAlias = message.ShortChannelIdTlv!.ShortChannelId;

                            if (_logger.IsEnabled(LogLevel.Debug))
                                _logger.LogDebug(
                                    "Updated remote alias for channel {ChannelId} from {OldAlias} to {NewAlias}",
                                    payload.ChannelId, oldAlias, channel.RemoteAlias);

                            await PersistChannelAsync(channel);
                        }
                        else if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug(
                                "Keeping existing remote alias {ExistingAlias} for channel {ChannelId}",
                                channel.RemoteAlias,
                                payload.ChannelId);
                        }
                    }
                    else if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Received duplicate ChannelReady message for channel {ChannelId} in Open state",
                                         payload.ChannelId);

                    break;
                }
            case ChannelState.ReadyForUs: // We already sent our ChannelReady, now they sent theirs
                {
                    // Valid transition: ReadyForUs -> Open
                    channel.UpdateState(ChannelState.Open);
                    await PersistChannelAsync(channel);

                    if (_logger.IsEnabled(LogLevel.Information))
                        _logger.LogInformation("Channel {ChannelId} is now open", payload.ChannelId);

                    // TODO: Notify application layer that channel is fully open
                    // TODO: Update routing tables

                    break;
                }
            case ChannelState.V1FundingSigned: // First ChannelReady
                {
                    // Valid transition: V1FundingSigned -> ReadyForThem
                    channel.UpdateState(ChannelState.ReadyForThem);
                    await PersistChannelAsync(channel);

                    if (_logger.IsEnabled(LogLevel.Information))
                        _logger.LogInformation(
                            "Received ChannelReady from peer for channel {ChannelId}, waiting for funding confirmation",
                            payload.ChannelId);

                    break;
                }
        }

        return null; // No further action needed
    }

    /// <summary>
    /// Persists a channel to the database using a scoped Unit of Work
    /// </summary>
    private async Task PersistChannelAsync(ChannelModel channel)
    {
        try
        {
            // Check if the channel already exists
            _ = await _unitOfWork.ChannelDbRepository.GetByIdAsync(channel.ChannelId)
             ?? throw new ChannelWarningException("Channel not found in database", channel.ChannelId,
                                                  "Sorry, we had an internal error");
            await _unitOfWork.ChannelDbRepository.UpdateAsync(channel);
            await _unitOfWork.SaveChangesAsync();

            _channelMemoryRepository.UpdateChannel(channel);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Successfully persisted channel {ChannelId} to database", channel.ChannelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist channel {ChannelId} to database", channel.ChannelId);
            throw;
        }
    }

    private static bool ShouldReplaceAlias()
    {
        return RandomNumberGenerator.GetInt32(0, 2) switch
        {
            0 => true,
            _ => false
        };
    }
}