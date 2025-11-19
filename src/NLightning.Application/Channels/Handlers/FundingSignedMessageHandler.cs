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

public class FundingSignedMessageHandler : IChannelMessageHandler<FundingSignedMessage>
{
    private readonly IBlockchainMonitor _blockchainMonitor;
    private readonly IBitcoinWalletService _bitcoinWalletService;
    private readonly IChannelMemoryRepository _channelMemoryRepository;
    private readonly ICommitmentTransactionBuilder _commitmentTransactionBuilder;
    private readonly ICommitmentTransactionModelFactory _commitmentTransactionModelFactory;
    private readonly IFundingTransactionBuilder _fundingTransactionBuilder;
    private readonly IFundingTransactionModelFactory _fundingTransactionModelFactory;
    private readonly ILightningSigner _lightningSigner;
    private readonly ILogger<FundingSignedMessageHandler> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUtxoMemoryRepository _utxoMemoryRepository;

    public FundingSignedMessageHandler(IBlockchainMonitor blockchainMonitor, IBitcoinWalletService bitcoinWalletService,
                                       IChannelMemoryRepository channelMemoryRepository,
                                       ICommitmentTransactionBuilder commitmentTransactionBuilder,
                                       ICommitmentTransactionModelFactory commitmentTransactionModelFactory,
                                       IFundingTransactionBuilder fundingTransactionBuilder,
                                       IFundingTransactionModelFactory fundingTransactionModelFactory,
                                       ILightningSigner lightningSigner, ILogger<FundingSignedMessageHandler> logger,
                                       IUnitOfWork unitOfWork, IUtxoMemoryRepository utxoMemoryRepository)
    {
        _blockchainMonitor = blockchainMonitor;
        _bitcoinWalletService = bitcoinWalletService;
        _channelMemoryRepository = channelMemoryRepository;
        _commitmentTransactionBuilder = commitmentTransactionBuilder;
        _commitmentTransactionModelFactory = commitmentTransactionModelFactory;
        _fundingTransactionBuilder = fundingTransactionBuilder;
        _fundingTransactionModelFactory = fundingTransactionModelFactory;
        _lightningSigner = lightningSigner;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _utxoMemoryRepository = utxoMemoryRepository;
    }

    public async Task<IChannelMessage?> HandleAsync(FundingSignedMessage message, ChannelState currentState,
                                                    FeatureOptions negotiatedFeatures, CompactPubKey peerPubKey)
    {
        _logger.LogTrace("Processing FundingCreatedMessage with ChannelId: {ChannelId} from Peer: {PeerPubKey}",
                         message.Payload.ChannelId, peerPubKey);

        var payload = message.Payload;

        if (currentState != ChannelState.V1FundingCreated)
            throw new ChannelErrorException(
                $"Received funding signed, but the channel {payload.ChannelId} had the wrong state: {Enum.GetName(currentState)}");

        // Check if there's a temporary channel for this peer
        if (!_channelMemoryRepository.TryGetChannel(payload.ChannelId, out var channel))
            throw new ChannelErrorException("This channel has never been negotiated", payload.ChannelId);

        // Generate the base commitment transactions
        var localCommitmentTransaction =
            _commitmentTransactionModelFactory.CreateCommitmentTransactionModel(channel, CommitmentSide.Local);

        // Build the output and the transactions
        var localUnsignedCommitmentTransaction = _commitmentTransactionBuilder.Build(localCommitmentTransaction);

        // Validate remote signature for our local commitment transaction
        _lightningSigner.ValidateSignature(channel.ChannelId, payload.Signature, localUnsignedCommitmentTransaction);

        // Update the channel with the new signatures and the new state
        channel.UpdateLastReceivedSignature(payload.Signature);
        channel.UpdateState(ChannelState.V1FundingSigned);

        // Save to the database
        await PersistChannelAsync(channel);

        // Get the locked utxos to create the funding transaction
        var utxos = _utxoMemoryRepository.GetLockedUtxosForChannel(channel.ChannelId);

        // Get a change address in case we need one
        var fundingTransactionModel = _fundingTransactionModelFactory.Create(channel, utxos, channel.ChangeAddress);
        var unsignedFundingTransaction = _fundingTransactionBuilder.Build(fundingTransactionModel);

        // Sign the transaction
        var allSigned = _lightningSigner.SignFundingTransaction(channel.ChannelId, unsignedFundingTransaction);
        if (!allSigned)
            throw new ChannelErrorException("Unable to sign all inputs for the funding transaction");

        await _blockchainMonitor.PublishAndWatchTransactionAsync(channel.ChannelId, unsignedFundingTransaction,
                                                                 channel.ChannelConfig.MinimumDepth);

        return null;
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