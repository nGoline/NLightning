using Microsoft.Extensions.Logging;

namespace NLightning.Application.Channels.Handlers;

using Domain.Bitcoin.Interfaces;
using Domain.Bitcoin.Transactions.Enums;
using Domain.Bitcoin.Transactions.Interfaces;
using Domain.Channels.Enums;
using Domain.Channels.Interfaces;
using Domain.Channels.Models;
using Domain.Crypto.ValueObjects;
using Domain.Exceptions;
using Domain.Node.Options;
using Domain.Persistence.Interfaces;
using Domain.Protocol.Interfaces;
using Domain.Protocol.Messages;
using Infrastructure.Bitcoin.Builders.Interfaces;
using Infrastructure.Bitcoin.Wallet.Interfaces;
using Interfaces;

public class FundingCreatedMessageHandler : IChannelMessageHandler<FundingCreatedMessage>
{
    private readonly IBlockchainMonitor _blockchainMonitor;
    private readonly IChannelIdFactory _channelIdFactory;
    private readonly IChannelMemoryRepository _channelMemoryRepository;
    private readonly ICommitmentTransactionBuilder _commitmentTransactionBuilder;
    private readonly ICommitmentTransactionModelFactory _commitmentTransactionModelFactory;
    private readonly ILightningSigner _lightningSigner;
    private readonly ILogger<FundingCreatedMessageHandler> _logger;
    private readonly IMessageFactory _messageFactory;
    private readonly IUnitOfWork _unitOfWork;

    public FundingCreatedMessageHandler(IBlockchainMonitor blockchainMonitor, IChannelIdFactory channelIdFactory,
                                        IChannelMemoryRepository channelMemoryRepository,
                                        ICommitmentTransactionBuilder commitmentTransactionBuilder,
                                        ICommitmentTransactionModelFactory commitmentTransactionModelFactory,
                                        ILightningSigner lightningSigner, ILogger<FundingCreatedMessageHandler> logger,
                                        IMessageFactory messageFactory, IUnitOfWork unitOfWork)
    {
        _blockchainMonitor = blockchainMonitor;
        _channelIdFactory = channelIdFactory;
        _channelMemoryRepository = channelMemoryRepository;
        _commitmentTransactionBuilder = commitmentTransactionBuilder;
        _commitmentTransactionModelFactory = commitmentTransactionModelFactory;
        _lightningSigner = lightningSigner;
        _logger = logger;
        _messageFactory = messageFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<IChannelMessage?> HandleAsync(FundingCreatedMessage message, ChannelState currentState,
                                                    FeatureOptions negotiatedFeatures, CompactPubKey peerPubKey)
    {
        _logger.LogTrace("Processing FundingCreatedMessage with ChannelId: {ChannelId} from Peer: {PeerPubKey}",
                         message.Payload.ChannelId, peerPubKey);

        var payload = message.Payload;

        if (currentState != ChannelState.None)
            throw new ChannelErrorException("A channel with this id already exists", payload.ChannelId);

        // Check if there's a temporary channel for this peer
        if (!_channelMemoryRepository.TryGetTemporaryChannelState(peerPubKey, payload.ChannelId, out currentState))
            throw new ChannelErrorException("This channel has never been negotiated", payload.ChannelId);

        if (currentState != ChannelState.V1Opening)
            throw new ChannelErrorException("Channel had the wrong state", payload.ChannelId,
                                            "This channel is already being negotiated with peer");

        // Get the channel and set missing props
        if (!_channelMemoryRepository.TryGetTemporaryChannel(peerPubKey, payload.ChannelId, out var channel))
            throw new ChannelErrorException("Temporary channel not found", payload.ChannelId);

        channel.FundingOutput.TransactionId = payload.FundingTxId;
        channel.FundingOutput.Index = payload.FundingOutputIndex;

        // Create a new channelId
        var oldChannelId = channel.ChannelId;
        channel.UpdateChannelId(_channelIdFactory.CreateV1(payload.FundingTxId, payload.FundingOutputIndex));

        // Register the channel with the signer
        _lightningSigner.RegisterChannel(channel.ChannelId, channel.GetSigningInfo());

        // Generate the base commitment transactions
        var localCommitmentTransaction =
            _commitmentTransactionModelFactory.CreateCommitmentTransactionModel(channel, CommitmentSide.Local);
        var remoteCommitmentTransaction =
            _commitmentTransactionModelFactory.CreateCommitmentTransactionModel(channel, CommitmentSide.Remote);

        // Build the output and the transactions
        var localUnsignedCommitmentTransaction = _commitmentTransactionBuilder.Build(localCommitmentTransaction);
        var remoteUnsignedCommitmentTransaction = _commitmentTransactionBuilder.Build(remoteCommitmentTransaction);

        // Validate remote signature for our local commitment transaction
        _lightningSigner.ValidateSignature(channel.ChannelId, payload.Signature, localUnsignedCommitmentTransaction);

        // Sign our remote commitment transaction
        var ourSignature =
            _lightningSigner.SignChannelTransaction(channel.ChannelId, remoteUnsignedCommitmentTransaction);

        // Update the channel with the new signatures and the new state
        channel.UpdateLastReceivedSignature(payload.Signature);
        channel.UpdateLastSentSignature(ourSignature);
        channel.UpdateState(ChannelState.V1FundingSigned);

        // Save to the database
        await PersistChannelAsync(channel);

        // Create the funding signed message
        var fundingSignedMessage =
            _messageFactory.CreateFundingSignedMessage(channel.ChannelId, ourSignature);

        // Add the channel to the dictionary
        _channelMemoryRepository.AddChannel(channel);

        // Remove the temporary channel
        _channelMemoryRepository.RemoveTemporaryChannel(peerPubKey, oldChannelId);

        await _blockchainMonitor.WatchTransactionAsync(channel.ChannelId, payload.FundingTxId,
                                                       channel.ChannelConfig.MinimumDepth);

        return fundingSignedMessage;
    }

    /// <summary>
    /// Persists a channel to the database using the scoped Unit of Work
    /// </summary>
    private async Task PersistChannelAsync(ChannelModel channel)
    {
        try
        {
            // Check if the channel already exists
            var existingChannel = await _unitOfWork.ChannelDbRepository.GetByIdAsync(channel.ChannelId);
            if (existingChannel is not null)
                throw new ChannelWarningException("Channel already exists", channel.ChannelId,
                                                  "This channel is already in our database");

            await _unitOfWork.ChannelDbRepository.AddAsync(channel);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogDebug("Successfully persisted channel {ChannelId} to database", channel.ChannelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist channel {ChannelId} to database", channel.ChannelId);
            throw;
        }
    }
}