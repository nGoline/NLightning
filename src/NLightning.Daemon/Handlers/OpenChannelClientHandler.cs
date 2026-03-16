using Microsoft.Extensions.Logging;

namespace NLightning.Daemon.Handlers;

using Domain.Bitcoin.Interfaces;
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
using Domain.Persistence.Interfaces;
using Domain.Protocol.Constants;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUtxoMemoryRepository _utxoMemoryRepository;

    internal event EventHandler<EventArgs>? OnWaitingConfirmation;

    public ClientCommand Command => ClientCommand.OpenChannel;

    public OpenChannelClientHandler(IBlockchainMonitor blockchainMonitor, IChannelFactory channelFactory,
                                    IChannelMemoryRepository channelMemoryRepository,
                                    ILogger<OpenChannelClientHandler> logger, IMessageFactory messageFactory,
                                    IPeerManager peerManager, IUnitOfWork unitOfWork,
                                    IUtxoMemoryRepository utxoMemoryRepository)
    {
        _blockchainMonitor = blockchainMonitor;
        _channelFactory = channelFactory;
        _channelMemoryRepository = channelMemoryRepository;
        _logger = logger;
        _messageFactory = messageFactory;
        _peerManager = peerManager;
        _unitOfWork = unitOfWork;
        _utxoMemoryRepository = utxoMemoryRepository;
    }

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

        _logger.LogTrace("Created Channel {id} with fundingPubKey: {fundingPubKey}", channel.ChannelId,
                         channel.LocalKeySet.FundingCompactPubKey);

        try
        {
            // Select UTXOs and mark them as toSpend for this channel
            var utxos = _utxoMemoryRepository.LockUtxosToSpendOnChannel(request.FundingAmount, channel.ChannelId);

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
            UpfrontShutdownScriptTlv? upfrontShutdownScriptTlv = null;
            if (channel.LocalUpfrontShutdownScript is not null)
                upfrontShutdownScriptTlv = new UpfrontShutdownScriptTlv(channel.LocalUpfrontShutdownScript.Value);
            else
                upfrontShutdownScriptTlv = new UpfrontShutdownScriptTlv(Array.Empty<byte>());

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

            if (!peer.TryGetPeerService(out var peerService))
                throw new ClientException(ErrorCodes.InvalidOperation, "Error getting peerService from peer");

            var tsc = new TaskCompletionSource<OpenChannelClientResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            peerService.OnChannelMessageReceived += ChannelMessageHandlerEnvelope;
            peerService.OnAttentionMessageReceived += AttentionMessageHandlerEnvelope;
            peerService.OnDisconnect += PeerDisconnectionEnvelope;
            peerService.OnExceptionRaised += ExceptionRaisedEnvelope;

            try
            {
                await peerService.SendMessageAsync(openChannel1Message);
            }
            catch
            {
                //Unsubscribe from the event so we don't have dangling memory
                peerService.OnChannelMessageReceived -= ChannelMessageHandlerEnvelope;
                peerService.OnAttentionMessageReceived -= AttentionMessageHandlerEnvelope;
                peerService.OnDisconnect -= PeerDisconnectionEnvelope;
                peerService.OnExceptionRaised -= ExceptionRaisedEnvelope;

                throw;
            }

            var response = await tsc.Task;

            // Unsubscribe from the event
            peerService.OnChannelMessageReceived -= ChannelMessageHandlerEnvelope;
            peerService.OnAttentionMessageReceived -= AttentionMessageHandlerEnvelope;
            peerService.OnDisconnect -= PeerDisconnectionEnvelope;
            peerService.OnExceptionRaised -= ExceptionRaisedEnvelope;

            return response;

            // Envelopes for the events
            void ChannelMessageHandlerEnvelope(object? _, ChannelMessageEventArgs args) =>
                HandleChannelMessage(args, channel.ChannelId, tsc);

            void AttentionMessageHandlerEnvelope(object? _, AttentionMessageEventArgs args) =>
                HandleAttentionMessage(args, channel.ChannelId, tsc);

            void PeerDisconnectionEnvelope(object? _, PeerDisconnectedEventArgs args) =>
                HandlePeerDisconnection(args, channel.ChannelId, tsc);

            void ExceptionRaisedEnvelope(object? _, Exception e) =>
                HandleExceptionRaised(e, channel.ChannelId, tsc);
        }
        catch
        {
            var utxos = _utxoMemoryRepository.ReturnUtxosNotSpentOnChannel(channel.ChannelId);

            // Since something went wrong, let's unlock the utxos on the database
            foreach (var utxo in utxos)
                _unitOfWork.UtxoDbRepository.Update(utxo);

            await _unitOfWork.SaveChangesAsync();

            throw;
        }
    }

    private void HandleChannelMessage(ChannelMessageEventArgs args, ChannelId _,
                                      TaskCompletionSource<OpenChannelClientResponse> tsc)
    {
        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (args.Message.Type)
        {
            case MessageTypes.AcceptChannel:
                Console.WriteLine("Channel accepted");
                break;
            case MessageTypes.FundingSigned:
                Console.WriteLine("Funding signed");
                OnWaitingConfirmation?.Invoke(this, EventArgs.Empty);
                break;
            case MessageTypes.ChannelReady:
                {
                    Console.WriteLine("Channel ready");
                    if (_channelMemoryRepository.TryGetChannel(args.Message.Payload.ChannelId, out var channel)
                     && channel.FundingOutput?.TransactionId is not null
                     && channel.FundingOutput?.Index is not null)
                    {
                        tsc.TrySetResult(new OpenChannelClientResponse(channel.FundingOutput.TransactionId.Value,
                                                                       channel.FundingOutput.Index.Value,
                                                                       channel.ChannelId));
                    }
                    else
                    {
                        Console.Error.WriteLine("Channel not found in memory repository");
                    }

                    break;
                }
            default:
                Console.WriteLine("Unknown message type: {0}", Enum.GetName(args.Message.Type));
                break;
        }
    }

    private static void HandleAttentionMessage(AttentionMessageEventArgs args, ChannelId _,
                                               TaskCompletionSource<OpenChannelClientResponse> tsc)
    {
        Console.Error.WriteLine($"Error opening channel: {args.Message}");
        tsc.TrySetException(new ChannelErrorException($"Error opening channel: {args.Message}"));
    }

    private static void HandlePeerDisconnection(PeerDisconnectedEventArgs _, ChannelId __,
                                                TaskCompletionSource<OpenChannelClientResponse> tsc)
    {
        Console.Error.WriteLine("Peer disconnected");
        tsc.TrySetException(new ChannelErrorException("Error opening channel: Peer disconnected"));
    }

    private static void HandleExceptionRaised(Exception e, ChannelId _,
                                              TaskCompletionSource<OpenChannelClientResponse> tsc)
    {
        Console.Error.WriteLine(e.ToString());
        tsc.TrySetException(e);
    }
}