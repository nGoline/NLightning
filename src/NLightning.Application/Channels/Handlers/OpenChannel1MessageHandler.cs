using Microsoft.Extensions.Logging;

namespace NLightning.Application.Channels.Handlers;

using Domain.Channels.Enums;
using Domain.Channels.Interfaces;
using Domain.Crypto.ValueObjects;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Node;
using Domain.Node.Options;
using Domain.Protocol.Interfaces;
using Domain.Protocol.Messages;
using Domain.Protocol.Tlv;
using Interfaces;

public class OpenChannel1MessageHandler : IChannelMessageHandler<OpenChannel1Message>
{
    private readonly IChannelFactory _channelFactory;
    private readonly IChannelMemoryRepository _channelMemoryRepository;
    private readonly ILogger<OpenChannel1MessageHandler> _logger;
    private readonly IMessageFactory _messageFactory;

    public OpenChannel1MessageHandler(IChannelFactory channelFactory, IChannelMemoryRepository channelMemoryRepository,
                                      ILogger<OpenChannel1MessageHandler> logger, IMessageFactory messageFactory)
    {
        _channelFactory = channelFactory;
        _channelMemoryRepository = channelMemoryRepository;
        _logger = logger;
        _messageFactory = messageFactory;
    }

    public async Task<IChannelMessage?> HandleAsync(OpenChannel1Message message, ChannelState currentState,
                                                    FeatureOptions negotiatedFeatures, CompactPubKey peerPubKey)
    {
        _logger.LogTrace("Processing OpenChannel1Message with ChannelId: {ChannelId} from Peer: {PeerPubKey}",
                         message.Payload.ChannelId, peerPubKey);

        var payload = message.Payload;

        if (currentState != ChannelState.None)
            throw new ChannelErrorException("A channel with this id already exists", payload.ChannelId);

        // Check if there's a temporary channel for this peer
        if (_channelMemoryRepository.TryGetTemporaryChannelState(peerPubKey, payload.ChannelId, out currentState))
        {
            if (currentState != ChannelState.V1Opening)
            {
                throw new ChannelErrorException("Channel had the wrong state", payload.ChannelId,
                                                "This channel is already being negotiated with peer");
            }
        }

        // Create the channel
        var channel = await _channelFactory.CreateChannelV1AsNonInitiatorAsync(message, negotiatedFeatures, peerPubKey);

        _logger.LogTrace("Created Channel with fundingPubKey: {fundingPubKey}",
                         channel.LocalKeySet.FundingCompactPubKey);

        // Add the channel to dictionaries
        _channelMemoryRepository.AddTemporaryChannel(peerPubKey, channel);

        // Create UpfrontShutdownScriptTlv if needed
        UpfrontShutdownScriptTlv? upfrontShutdownScriptTlv = null;
        if (channel.LocalUpfrontShutdownScript is not null)
            upfrontShutdownScriptTlv = new UpfrontShutdownScriptTlv(channel.LocalUpfrontShutdownScript.Value);
        else
            upfrontShutdownScriptTlv = new UpfrontShutdownScriptTlv(Array.Empty<byte>());

        var channelTypeFeatureSet = FeatureSet.NewBasicChannelType();
        if (negotiatedFeatures.OptionAnchors >= FeatureSupport.Optional)
            channelTypeFeatureSet.SetFeature(Feature.OptionAnchors,
                                             negotiatedFeatures.OptionAnchors == FeatureSupport.Compulsory);

        if (channel.ChannelConfig.UseScidAlias >= FeatureSupport.Optional)
            channelTypeFeatureSet.SetFeature(Feature.OptionScidAlias,
                                             channel.ChannelConfig.UseScidAlias == FeatureSupport.Compulsory);

        if (channel.ChannelConfig.MinimumDepth == 0)
            channelTypeFeatureSet.SetFeature(Feature.OptionZeroconf, true);

        var featureSetBytes = channelTypeFeatureSet.GetBytes() ?? throw new ChannelErrorException("The channel type is not supported", payload.ChannelId,
                                            "Sorry, we had an internal error");
        var channelTypeTlv = new ChannelTypeTlv(featureSetBytes);

        // Create the reply message
        var acceptChannel1ReplyMessage = _messageFactory
           .CreateAcceptChannel1Message(channel.ChannelConfig.ChannelReserveAmount, channelTypeTlv,
                                        channel.LocalKeySet.DelayedPaymentCompactBasepoint,
                                        channel.LocalKeySet.CurrentPerCommitmentCompactPoint,
                                        channel.LocalKeySet.FundingCompactPubKey,
                                        channel.LocalKeySet.HtlcCompactBasepoint,
                                        channel.ChannelConfig.MaxAcceptedHtlcs,
                                        channel.ChannelConfig.MaxHtlcAmountInFlight, channel.ChannelConfig.MinimumDepth,
                                        channel.LocalKeySet.PaymentCompactBasepoint,
                                        channel.LocalKeySet.RevocationCompactBasepoint, channel.ChannelId,
                                        channel.ChannelConfig.ToSelfDelay, upfrontShutdownScriptTlv);

        return acceptChannel1ReplyMessage;
    }
}