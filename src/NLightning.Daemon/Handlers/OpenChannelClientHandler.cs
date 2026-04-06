using Microsoft.Extensions.Logging;

namespace NLightning.Daemon.Handlers;

using Domain.Bitcoin.Interfaces;
using Domain.Channels.Events;
using Domain.Channels.Interfaces;
using Domain.Channels.ValueObjects;
using Domain.Client.Constants;
using Domain.Client.Enums;
using Domain.Client.Exceptions;
using Domain.Client.Requests;
using Domain.Client.Responses;
using Domain.Crypto.ValueObjects;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Node;
using Domain.Node.Events;
using Domain.Node.Interfaces;
using Domain.Node.ValueObjects;
using Domain.Protocol.Interfaces;
using Domain.Protocol.Tlv;
using Infrastructure.Bitcoin.Wallet.Interfaces;
using Infrastructure.Protocol.Models;
using Interfaces;

public sealed class OpenChannelClientHandler
    : IClientCommandHandler<OpenChannelClientRequest, OpenChannelClientResponse>
{
    private readonly IBlockchainMonitor _blockchainMonitor;
    private readonly IChannelMemoryRepository _channelMemoryRepository;
    private readonly IChannelFactory _channelFactory;
    private readonly ILogger<OpenChannelClientHandler> _logger;
    private readonly IMessageFactory _messageFactory;
    private readonly IPeerManager _peerManager;
    private readonly IUtxoMemoryRepository _utxoMemoryRepository;

    private ChannelId _channelId = ChannelId.Zero;
    private IPeerService? _peerService;

    /// <inheritdoc/>
    public ClientCommand Command => ClientCommand.OpenChannel;

    public OpenChannelClientHandler(IBlockchainMonitor blockchainMonitor, IChannelFactory channelFactory,
                                    IChannelMemoryRepository channelMemoryRepository,
                                    ILogger<OpenChannelClientHandler> logger, IMessageFactory messageFactory,
                                    IPeerManager peerManager, IUtxoMemoryRepository utxoMemoryRepository)
    {
        _blockchainMonitor = blockchainMonitor;
        _channelFactory = channelFactory;
        _channelMemoryRepository = channelMemoryRepository;
        _logger = logger;
        _messageFactory = messageFactory;
        _peerManager = peerManager;
        _utxoMemoryRepository = utxoMemoryRepository;
    }

    /// <inheritdoc/>
    public async Task<OpenChannelClientResponse> HandleAsync(OpenChannelClientRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NodeInfo))
            throw new ClientException(ErrorCodes.InvalidAddress, "Address cannot be empty");

        // Check if either a PeerAddressInfo or a CompactPubKey was provided
        var isPeerAddressInfo = request.NodeInfo.Contains('@') && request.NodeInfo.Contains(':');
        CompactPubKey peerId;

        peerId = isPeerAddressInfo
                     ? new PeerAddress(request.NodeInfo).PubKey
                     : new CompactPubKey(Convert.FromHexString(request.NodeInfo)); // Parse as a hex public key

        // Check if we're connected to the peer
        var peer = _peerManager.GetPeer(peerId)
                ?? await _peerManager.ConnectToPeerAsync(new PeerAddressInfo(request.NodeInfo));

        // Let's check if we have enough funds to open this channel
        var currentHeight = _blockchainMonitor.LastProcessedBlockHeight;
        if (_utxoMemoryRepository.GetConfirmedBalance(currentHeight) < request.FundingAmount)
            throw new ClientException(ErrorCodes.NotEnoughBalance, "We don't have enough balance to open this channel");

        // Since we're connected, let's open the channel
        var channel =
            await _channelFactory.CreateChannelV1AsInitiatorAsync(request, peer.NegotiatedFeatures, peerId);

        // Save the channelId for later
        _channelId = channel.ChannelId;

        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("Created Temporary Channel {id} with fundingPubKey: {fundingPubKey}", channel.ChannelId,
                             channel.LocalKeySet.FundingCompactPubKey);

        // Select UTXOs and mark them as toSpend for this channel
        _utxoMemoryRepository.LockUtxosToSpendOnChannel(request.FundingAmount, channel.ChannelId);

        // Create a task completion source for the response
        var tsc = new TaskCompletionSource<OpenChannelClientResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            // Add the channel to dictionaries
            _channelMemoryRepository.AddTemporaryChannel(peerId, channel);

            // Create the channel type Tlv 
            var channelTypeFeatureSet = FeatureSet.NewBasicChannelType();
            if (peer.NegotiatedFeatures.OptionAnchors >= FeatureSupport.Optional)
                channelTypeFeatureSet.SetFeature(Feature.OptionAnchors, true);

            if (channel.ChannelConfig.UseScidAlias >= FeatureSupport.Optional)
                channelTypeFeatureSet.SetFeature(Feature.OptionScidAlias, true);

            if (channel.ChannelConfig.MinimumDepth == 0)
                channelTypeFeatureSet.SetFeature(Feature.OptionZeroconf, true);

            var featureSetBytes = channelTypeFeatureSet.GetBytes() ?? throw new ClientException(
                                      ErrorCodes.InvalidOperation,
                                      $"Error creating {nameof(ChannelTypeTlv)}. This should never happen.");
            var channelTypeTlv = new ChannelTypeTlv(featureSetBytes);

            // Create UpfrontShutdownScriptTlv if needed
            var upfrontShutdownScriptTlv = channel.LocalUpfrontShutdownScript is not null
                                               ? new UpfrontShutdownScriptTlv(channel.LocalUpfrontShutdownScript.Value)
                                               : new UpfrontShutdownScriptTlv(Array.Empty<byte>());

            // Create the ChannelFlags
            var channelFlags = new ChannelFlags(ChannelFlag.None);
            if (peer.NegotiatedFeatures.ScidAlias == FeatureSupport.Compulsory)
                channelFlags = new ChannelFlags(ChannelFlag.AnnounceChannel);

            // Create the openChannel message
            var openChannel1Message = _messageFactory.CreateOpenChannel1Message(
                channel.ChannelId, channel.LocalBalance, channel.LocalKeySet.FundingCompactPubKey,
                channel.RemoteBalance, channel.ChannelConfig.ChannelReserveAmount,
                channel.ChannelConfig.FeeRateAmountPerKw,
                channel.ChannelConfig.MaxAcceptedHtlcs, channel.LocalKeySet.RevocationCompactBasepoint,
                channel.LocalKeySet.PaymentCompactBasepoint, channel.LocalKeySet.DelayedPaymentCompactBasepoint,
                channel.LocalKeySet.HtlcCompactBasepoint, channel.LocalKeySet.CurrentPerCommitmentCompactPoint,
                channelFlags, channelTypeTlv, upfrontShutdownScriptTlv);

            if (!peer.TryGetPeerService(out _peerService))
                throw new ClientException(ErrorCodes.InvalidOperation, "Error getting peerService from peer");

            // Subscribe to the events before sending the message
            _peerService.OnAttentionMessageReceived += AttentionMessageHandlerEnvelope;
            _peerService.OnDisconnect += PeerDisconnectionEnvelope;
            _peerService.OnExceptionRaised += ExceptionRaisedEnvelope;
            _channelMemoryRepository.OnChannelUpgraded += ChannelUpgradedHandlerEnvelope;

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Sending OpenChannel message to peer {peerId} for channel {channelId}",
                                       peerId,
                                       channel.ChannelId);
            await _peerService.SendMessageAsync(openChannel1Message);

            return await tsc.Task;
        }
        catch
        {
            _utxoMemoryRepository.ReturnUtxosNotSpentOnChannel(_channelId);

            throw;
        }
        finally
        {
            //Unsubscribe from the events so we don't have dangling memory
            _peerService?.OnAttentionMessageReceived -= AttentionMessageHandlerEnvelope;
            _peerService?.OnDisconnect -= PeerDisconnectionEnvelope;
            _peerService?.OnExceptionRaised -= ExceptionRaisedEnvelope;
            _channelMemoryRepository.OnChannelUpgraded -= ChannelUpgradedHandlerEnvelope;
        }

        // Envelopes for the events
        void AttentionMessageHandlerEnvelope(object? _, AttentionMessageEventArgs args) =>
            HandleAttentionMessage(args, tsc);

        void PeerDisconnectionEnvelope(object? _, PeerDisconnectedEventArgs args) =>
            HandlePeerDisconnection(args, channel.RemoteNodeId, tsc);

        void ExceptionRaisedEnvelope(object? _, Exception e) =>
            HandleExceptionRaised(e, tsc);

        void ChannelUpgradedHandlerEnvelope(object? _, ChannelUpgradedEventArgs args) =>
            HandleChannelUpgraded(args, tsc);
    }

    private void HandleChannelUpgraded(ChannelUpgradedEventArgs args,
                                       TaskCompletionSource<OpenChannelClientResponse> tsc)
    {
        if (args.OldChannelId != _channelId)
            return;

        tsc.TrySetResult(new OpenChannelClientResponse(args.NewChannelId));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Channel {oldChannelId} has been upgraded to {channelId}", args.OldChannelId,
                                   args.NewChannelId);
    }

    private void HandleAttentionMessage(AttentionMessageEventArgs args,
                                        TaskCompletionSource<OpenChannelClientResponse> tsc)
    {
        if (args.ChannelId != _channelId)
            return;

        _logger.LogError(
            "Received attention message from peer {peerId} for channel {channelId}: {message}",
            args.PeerPubKey, args.ChannelId, args.Message);

        tsc.TrySetException(new ChannelErrorException($"Error opening channel: {args.Message}"));
    }

    private void HandlePeerDisconnection(PeerDisconnectedEventArgs args, CompactPubKey peerPubKey,
                                         TaskCompletionSource<OpenChannelClientResponse> tsc)
    {
        if (args.PeerPubKey != peerPubKey)
            return;

        _logger.LogError("Peer disconnected without notice");
        tsc.TrySetException(new ConnectionException("Error opening channel: Peer disconnected"));
    }

    private void HandleExceptionRaised(Exception e, TaskCompletionSource<OpenChannelClientResponse> tsc)
    {
        if (e is not ChannelErrorException ce || ce.ChannelId != _channelId)
            return;

        _logger.LogError("Exception raised while opening channel: {message}", e.Message);
        tsc.TrySetException(e);
    }
}