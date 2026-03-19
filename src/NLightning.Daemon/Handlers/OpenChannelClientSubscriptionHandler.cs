using Microsoft.Extensions.Logging;

namespace NLightning.Daemon.Handlers;

using Domain.Bitcoin.Interfaces;
using Domain.Channels.Enums;
using Domain.Channels.Events;
using Domain.Channels.Interfaces;
using Domain.Channels.ValueObjects;
using Domain.Client.Constants;
using Domain.Client.Enums;
using Domain.Client.Exceptions;
using Domain.Client.Requests;
using Domain.Client.Responses;
using Domain.Crypto.ValueObjects;
using Domain.Exceptions;
using Domain.Node.Events;
using Domain.Node.Interfaces;
using Interfaces;

public class OpenChannelClientSubscriptionHandler :
    IClientCommandHandler<OpenChannelClientSubscriptionRequest, OpenChannelClientSubscriptionResponse>
{
    private readonly IChannelMemoryRepository _channelMemoryRepository;
    private readonly ILogger<OpenChannelClientSubscriptionHandler> _logger;
    private readonly IPeerManager _peerManager;
    private readonly IUtxoMemoryRepository _utxoMemoryRepository;

    private ChannelId _channelId;
    private IPeerService? _peerService;

    /// <inheritdoc/>
    public ClientCommand Command => ClientCommand.OpenChannelSubscription;

    public OpenChannelClientSubscriptionHandler(IChannelMemoryRepository channelMemoryRepository,
                                                ILogger<OpenChannelClientSubscriptionHandler> logger,
                                                IPeerManager peerManager, IUtxoMemoryRepository utxoMemoryRepository)
    {
        _channelMemoryRepository = channelMemoryRepository;
        _logger = logger;
        _peerManager = peerManager;
        _utxoMemoryRepository = utxoMemoryRepository;
    }

    /// <inheritdoc/>
    public async Task<OpenChannelClientSubscriptionResponse> HandleAsync(OpenChannelClientSubscriptionRequest request,
                                                                         CancellationToken ct)
    {
        if (request.ChannelId == ChannelId.Zero)
            throw new ClientException(ErrorCodes.InvalidChannel, "ChannelId cannot be empty");

        _channelId = request.ChannelId;

        if (!_channelMemoryRepository.TryGetChannel(_channelId, out var channel))
        {
            if (!_channelMemoryRepository.TryGetChannel(_channelId, out channel))
                throw new ClientException(ErrorCodes.InvalidChannel, $"Channel with Id {_channelId} not found");
        }

        var peer = _peerManager.GetPeer(channel.RemoteNodeId) ?? throw new ClientException(ErrorCodes.InvalidOperation,
                                      $"Peer with NodeId {channel.RemoteNodeId} is not connected");
        var lockedUtxos = _utxoMemoryRepository.GetLockedUtxosForChannel(_channelId);
        if (lockedUtxos.Count == 0)
            throw new ClientException(ErrorCodes.InvalidOperation,
                                      $"No locked UTXOs found for channel {_channelId}");

        // Create a task completion source for the response
        var tsc = new TaskCompletionSource<OpenChannelClientSubscriptionResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Check if the channel is already in a state we care about
        var shouldPersistChannel = channel.State is ChannelState.V1FundingSigned
                                                 or ChannelState.ReadyForUs
                                                 or ChannelState.ReadyForThem;

        try
        {
            if (!peer.TryGetPeerService(out _peerService))
                throw new ClientException(ErrorCodes.InvalidOperation, "Error getting peerService from peer");

            // Subscribe to the events
            _peerService.OnAttentionMessageReceived += AttentionMessageHandlerEnvelope;
            _peerService.OnDisconnect += PeerDisconnectionEnvelope;
            _peerService.OnExceptionRaised += ExceptionRaisedEnvelope;
            _channelMemoryRepository.OnChannelUpdated += ChannelUpdatedHandlerEnvelope;

            return await tsc.Task;
        }
        catch
        {
            if (!shouldPersistChannel)
                _utxoMemoryRepository.ReturnUtxosNotSpentOnChannel(request.ChannelId);

            throw;
        }
        finally
        {
            //Unsubscribe from the events so we don't have dangling memory
            _peerService?.OnAttentionMessageReceived -= AttentionMessageHandlerEnvelope;
            _peerService?.OnDisconnect -= PeerDisconnectionEnvelope;
            _peerService?.OnExceptionRaised -= ExceptionRaisedEnvelope;
            _channelMemoryRepository.OnChannelUpdated -= ChannelUpdatedHandlerEnvelope;
        }

        // Envelopes for the events
        void AttentionMessageHandlerEnvelope(object? _, AttentionMessageEventArgs args) =>
            HandleAttentionMessage(args, tsc);

        void PeerDisconnectionEnvelope(object? _, PeerDisconnectedEventArgs args) =>
            HandlePeerDisconnection(args, channel.RemoteNodeId, tsc);

        void ExceptionRaisedEnvelope(object? _, Exception e) =>
            HandleExceptionRaised(e, tsc);

        void ChannelUpdatedHandlerEnvelope(object? _, ChannelUpdatedEventArgs args) =>
            HandleChannelUpdated(args, tsc);
    }

    private void HandleAttentionMessage(AttentionMessageEventArgs args,
                                        TaskCompletionSource<OpenChannelClientSubscriptionResponse> tsc)
    {
        if (args.ChannelId != _channelId)
            return;

        _logger.LogError(
            "Received attention message from peer {peerId} for channel {channelId}: {message}",
            args.PeerPubKey, args.ChannelId, args.Message);

        tsc.TrySetException(new ChannelErrorException($"Error opening channel: {args.Message}"));
    }

    private void HandlePeerDisconnection(PeerDisconnectedEventArgs args, CompactPubKey peerPubKey,
                                         TaskCompletionSource<OpenChannelClientSubscriptionResponse> tsc)
    {
        if (args.PeerPubKey != peerPubKey)
            return;

        if (args.Exception is null)
        {
            _logger.LogError("Peer disconnected without notice");
            tsc.TrySetException(new ConnectionException("Error opening channel: Peer disconnected"));
        }
        else
        {
            _logger.LogError(args.Exception, "Peer disconnected. Error: {message}", args.Exception.Message);
            tsc.TrySetException(new ConnectionException("Error opening channel: Peer disconnected", args.Exception));
        }
    }

    private void HandleExceptionRaised(Exception e, TaskCompletionSource<OpenChannelClientSubscriptionResponse> tsc)
    {
        if (e is not ChannelErrorException ce || ce.ChannelId != _channelId)
            return;

        _logger.LogError("Exception raised while opening channel: {message}", e.Message);
        tsc.TrySetException(e);
    }

    private void HandleChannelUpdated(ChannelUpdatedEventArgs args,
                                      TaskCompletionSource<OpenChannelClientSubscriptionResponse> tsc)
    {
        if (args.Channel.ChannelId != _channelId)
            return;

        if (args.Channel.State == ChannelState.V1FundingSigned)
        {
            tsc.TrySetResult(new OpenChannelClientSubscriptionResponse(args.Channel.ChannelId)
            {
                ChannelState = ChannelState.V1FundingSigned,
                TxId = args.Channel.FundingOutput?.TransactionId,
                Index = args.Channel.FundingOutput?.Index
            });
        }
        else if (args.Channel.State is ChannelState.ReadyForUs or ChannelState.ReadyForThem)
        {
            tsc.TrySetResult(new OpenChannelClientSubscriptionResponse(args.Channel.ChannelId)
            {
                ChannelState = ChannelState.ReadyForUs,
                TxId = args.Channel.FundingOutput?.TransactionId,
                Index = args.Channel.FundingOutput?.Index
            });
        }
    }
}