using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace NLightning.Application.Channels.Handlers;

using Domain.Bitcoin.Interfaces;
using Domain.Channels.Enums;
using Domain.Channels.Interfaces;
using Domain.Channels.Models;
using Domain.Channels.ValueObjects;
using Domain.Enums;
using Domain.Persistence.Interfaces;
using Domain.Protocol.Interfaces;

public class FundingConfirmedMessageHandler
{
    private readonly IChannelMemoryRepository _channelMemoryRepository;
    private readonly ILightningSigner _lightningSigner;
    private readonly ILogger<FundingConfirmedMessageHandler> _logger;
    private readonly IMessageFactory _messageFactory;
    private readonly IUnitOfWork _uow;

    public event EventHandler<IChannelMessage>? OnMessageReady;

    public FundingConfirmedMessageHandler(IChannelMemoryRepository channelMemoryRepository,
                                          ILightningSigner lightningSigner,
                                          ILogger<FundingConfirmedMessageHandler> logger,
                                          IMessageFactory messageFactory,
                                          IUnitOfWork uow)
    {
        _channelMemoryRepository = channelMemoryRepository;
        _lightningSigner = lightningSigner;
        _logger = logger;
        _messageFactory = messageFactory;
        _uow = uow;
    }

    public async Task HandleAsync(ChannelModel channel)
    {
        try
        {
            // Check if the channel is in the right state
            if (channel.State is not (ChannelState.V1FundingSigned
                                   or ChannelState.ReadyForThem))
                _logger.LogError(
                    "Received funding confirmation, but the channel {ChannelId} had a wrong state: {State}",
                    channel.ChannelId, Enum.GetName(channel.State));

            var mustUseScidAlias = channel.ChannelConfig.UseScidAlias > FeatureSupport.No;

            // Create our new per-commitment point
            channel.CommitmentNumber.Increment();
            var newPerCommitmentPoint =
                _lightningSigner.GetPerCommitmentPoint(channel.ChannelId, channel.CommitmentNumber.Value);
            channel.LocalKeySet.UpdatePerCommitmentPoint(newPerCommitmentPoint);

            // Handle ScidAlias
            if (mustUseScidAlias)
            {
                // Decide how many SCID aliases we need
                var scidAliasesCount = RandomNumberGenerator.GetInt32(2, 6); // Randomly choose between 2 and 5
                channel.LocalAliases = new List<ShortChannelId>();
                for (var i = 0; i < scidAliasesCount; i++)
                {
                    // Generate a random SCID alias
                    var scidAlias = new ShortChannelId(RandomNumberGenerator.GetBytes(ShortChannelId.Length));
                    channel.LocalAliases.Add(scidAlias);
                }
            }

            if (channel.State == ChannelState.ReadyForThem)
            {
                // Valid transition: ReadyForThem -> Open
                channel.UpdateState(ChannelState.Open);
                await PersistChannelAsync(channel);

                _logger.LogInformation("Channel {ChannelId} is now open", channel.ChannelId);

                // TODO: Notify application layer that channel is fully open
                // TODO: Update routing tables
            }
            else if (channel.State == ChannelState.V1FundingSigned)
            {
                // Valid transition: V1FundingSigned -> ReadyForUs
                channel.UpdateState(ChannelState.ReadyForUs);
                await PersistChannelAsync(channel);

                _logger.LogInformation("Funding confirmed for us for channel {ChannelId}",
                                       channel.ChannelId);
            }

            if (channel.LocalAliases is { Count: > 0 })
            {
                // Create a ChannelReady message with the SCID aliases
                foreach (var alias in channel.LocalAliases)
                {
                    var channelReadyMessage =
                        _messageFactory.CreateChannelReadyMessage(channel.ChannelId, newPerCommitmentPoint, alias);

                    // Raise the event with the message
                    OnMessageReady?.Invoke(this, channelReadyMessage);
                }
            }
            else
            {
                var channelReadyMessage =
                    _messageFactory.CreateChannelReadyMessage(channel.ChannelId, newPerCommitmentPoint,
                                                              channel.ShortChannelId);

                // Raise the event with the message
                OnMessageReady?.Invoke(this, channelReadyMessage);
            }

            _logger.LogInformation("Channel {ChannelId} funding transaction confirmed", channel.ChannelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling funding confirmation for channel {ChannelId}", channel.ChannelId);
            throw;
        }
    }

    private async Task PersistChannelAsync(ChannelModel channel)
    {
        _channelMemoryRepository.UpdateChannel(channel);
        await _uow.ChannelDbRepository.UpdateAsync(channel);

        await _uow.SaveChangesAsync();
    }
}