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

    public ClientCommand Command => ClientCommand.OpenChannel;

    public OpenChannelClientHandler(IBlockchainMonitor blockchainMonitor,
                                    IChannelMemoryRepository channelMemoryRepository, IChannelFactory channelFactory,
                                    ILogger<OpenChannelClientHandler> logger, IMessageFactory messageFactory,
                                    IPeerManager peerManager, IUnitOfWork unitOfWork,
                                    IUtxoMemoryRepository utxoMemoryRepository)
    {
        _blockchainMonitor = blockchainMonitor;
        _channelMemoryRepository = channelMemoryRepository;
        _channelFactory = channelFactory;
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

        if (isPeerAddressInfo)
            peerId = new PeerAddress(request.NodeInfo).PubKey;

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
            // TODO: Set the channel reserve as 1% of the channel or at least 354 sats
            // Add the channel to dictionaries
            _channelMemoryRepository.AddTemporaryChannel(peerId, channel);

            // Select UTXOs and mark them as toSpend for this channel
            var utxos = _utxoMemoryRepository.LockUtxosToSpendOnChannel(request.FundingAmount, channel.ChannelId);

            // Create a FeatureSet for the ChannelTypeTlv
            var featureSet = new FeatureSet();
            featureSet.SetFeature(Feature.VarOnionOptin, false, false);

            // Set StaticRemoteKey if needed
            if (peer.NegotiatedFeatures.StaticRemoteKey == FeatureSupport.Compulsory)
                featureSet.SetFeature(Feature.OptionStaticRemoteKey, true);

            // Set OptionAnchorOutputs if needed
            if (peer.NegotiatedFeatures.AnchorOutputs == FeatureSupport.Compulsory)
                featureSet.SetFeature(Feature.OptionAnchorOutputs, true);

            // Create UpfrontShutdownScriptTlv if needed
            UpfrontShutdownScriptTlv? upfrontShutdownScriptTlv = null;
            if (channel.LocalUpfrontShutdownScript is not null)
                upfrontShutdownScriptTlv = new UpfrontShutdownScriptTlv(channel.LocalUpfrontShutdownScript.Value);

            // Create the ChannelFlags
            var channelFlags = new ChannelFlags(ChannelFlag.None);
            if (peer.Features.IsFeatureSet(Feature.OptionScidAlias, true))
            {
                featureSet.SetFeature(Feature.OptionScidAlias, true);
                channelFlags = new ChannelFlags(ChannelFlag.AnnounceChannel);
            }

            // Create the ChannelTypeTlv
            ChannelTypeTlv? channelTypeTlv = null;
            var featureSetBytes = featureSet.GetBytes();
            if (featureSetBytes is not null)
                channelTypeTlv = new ChannelTypeTlv(featureSetBytes);

            // Create the openChannel message
            var openChannel1Message = _messageFactory.CreateOpenChannel1Message(
                channel.ChannelId, channel.LocalBalance, channel.LocalKeySet.FundingCompactPubKey,
                channel.RemoteBalance, channel.ChannelConfig.ChannelReserveAmount!,
                channel.ChannelConfig.FeeRateAmountPerKw,
                channel.ChannelConfig.MaxAcceptedHtlcs, channel.LocalKeySet.RevocationCompactBasepoint,
                channel.LocalKeySet.PaymentCompactBasepoint, channel.LocalKeySet.DelayedPaymentCompactBasepoint,
                channel.LocalKeySet.HtlcCompactBasepoint, channel.LocalKeySet.CurrentPerCommitmentCompactPoint,
                channelFlags, upfrontShutdownScriptTlv, channelTypeTlv);

            if (!peer.TryGetPeerService(out var peerService))
                throw new ClientException(ErrorCodes.InvalidOperation, "Error getting peerService from peer");

            var tsc = new TaskCompletionSource<OpenChannelClientResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            peerService.OnChannelMessageReceived += ChannelMessageHandlerEnvelope;

            try
            {
                await peerService.SendMessageAsync(openChannel1Message);
            }
            catch
            {
                //Unsubscribe from the event so we don't have dangling memory
                peerService.OnChannelMessageReceived -= ChannelMessageHandlerEnvelope;

                throw;
            }

            // Since everything went ok so far, let's update the locked utxos on the database
            foreach (var utxo in utxos)
                _unitOfWork.UtxoDbRepository.Update(utxo);

            await _unitOfWork.SaveChangesAsync();

            var response = await tsc.Task;

            // Unsubscribe from the event
            peerService.OnChannelMessageReceived -= ChannelMessageHandlerEnvelope;

            return response;

            // 
            void ChannelMessageHandlerEnvelope(object? _, ChannelMessageEventArgs args)
            {
                HandleChannelMessage(args, channel.ChannelId, tsc);
            }
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

    private void HandleChannelMessage(ChannelMessageEventArgs args, ChannelId channelId,
                                      TaskCompletionSource<OpenChannelClientResponse> tcs)
    {
        if (args.Message.Type == MessageTypes.AcceptChannel)
        {
            Console.WriteLine("Channel accepted");
        }
        else if (args.Message.Type == MessageTypes.FundingSigned)
        {
            Console.WriteLine("Funding signed");
        }
        else
        {
            Console.WriteLine("Unknown message type: {0}", Enum.GetName(args.Message.Type));
        }
    }
}