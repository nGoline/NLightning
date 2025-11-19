using Microsoft.Extensions.Logging;

namespace NLightning.Application.Channels.Handlers;

using Domain.Bitcoin.Enums;
using Domain.Bitcoin.Interfaces;
using Domain.Bitcoin.Transactions.Enums;
using Domain.Bitcoin.Transactions.Interfaces;
using Domain.Bitcoin.Transactions.Outputs;
using Domain.Bitcoin.ValueObjects;
using Domain.Channels.Enums;
using Domain.Channels.Interfaces;
using Domain.Channels.Models;
using Domain.Channels.Validators.Parameters;
using Domain.Channels.ValueObjects;
using Domain.Crypto.Hashes;
using Domain.Crypto.ValueObjects;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Node.Options;
using Domain.Persistence.Interfaces;
using Domain.Protocol.Interfaces;
using Domain.Protocol.Messages;
using Domain.Protocol.Models;
using Infrastructure.Bitcoin.Builders.Interfaces;
using Infrastructure.Bitcoin.Wallet.Interfaces;
using Interfaces;

public class AcceptChannel1MessageHandler : IChannelMessageHandler<AcceptChannel1Message>
{
    private readonly IBitcoinWalletService _bitcoinWalletService;
    private readonly IChannelIdFactory _channelIdFactory;
    private readonly IChannelMemoryRepository _channelMemoryRepository;
    private readonly IChannelOpenValidator _channelOpenValidator;
    private readonly ICommitmentTransactionBuilder _commitmentTransactionBuilder;
    private readonly ICommitmentTransactionModelFactory _commitmentTransactionModelFactory;
    private readonly IFundingTransactionBuilder _fundingTransactionBuilder;
    private readonly IFundingTransactionModelFactory _fundingTransactionModelFactory;
    private readonly ILightningSigner _lightningSigner;
    private readonly ILogger<OpenChannel1MessageHandler> _logger;
    private readonly IMessageFactory _messageFactory;
    private readonly ISha256 _sha256;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUtxoMemoryRepository _utxoMemoryRepository;

    public AcceptChannel1MessageHandler(IBitcoinWalletService bitcoinWalletService, IChannelIdFactory channelIdFactory,
                                        IChannelMemoryRepository channelMemoryRepository,
                                        IChannelOpenValidator channelOpenValidator,
                                        ICommitmentTransactionBuilder commitmentTransactionBuilder,
                                        ICommitmentTransactionModelFactory commitmentTransactionModelFactory,
                                        IFundingTransactionBuilder fundingTransactionBuilder,
                                        IFundingTransactionModelFactory fundingTransactionModelFactory,
                                        ILightningSigner lightningSigner, ILogger<OpenChannel1MessageHandler> logger,
                                        IMessageFactory messageFactory, ISha256 sha256, IUnitOfWork unitOfWork,
                                        IUtxoMemoryRepository utxoMemoryRepository)
    {
        _bitcoinWalletService = bitcoinWalletService;
        _channelIdFactory = channelIdFactory;
        _channelMemoryRepository = channelMemoryRepository;
        _channelOpenValidator = channelOpenValidator;
        _commitmentTransactionBuilder = commitmentTransactionBuilder;
        _commitmentTransactionModelFactory = commitmentTransactionModelFactory;
        _fundingTransactionBuilder = fundingTransactionBuilder;
        _fundingTransactionModelFactory = fundingTransactionModelFactory;
        _lightningSigner = lightningSigner;
        _logger = logger;
        _messageFactory = messageFactory;
        _sha256 = sha256;
        _unitOfWork = unitOfWork;
        _utxoMemoryRepository = utxoMemoryRepository;
    }

    public async Task<IChannelMessage?> HandleAsync(AcceptChannel1Message message, ChannelState currentState,
                                                    FeatureOptions negotiatedFeatures, CompactPubKey peerPubKey)
    {
        _logger.LogTrace("Processing AcceptChannel1Message with ChannelId: {ChannelId} from Peer: {PeerPubKey}",
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

        // Get the temporary channel
        if (!_channelMemoryRepository.TryGetTemporaryChannel(peerPubKey, payload.ChannelId, out var tempChannel))
            throw new ChannelErrorException("Temporary channel not found", payload.ChannelId);

        // Check if the channel type was negotiated and the channel type is present
        if (message.ChannelTypeTlv is not null && negotiatedFeatures.ChannelType == FeatureSupport.Compulsory)
            throw new ChannelErrorException("Channel type was negotiated but not provided");

        // Perform optional checks for the channel
        _channelOpenValidator.PerformOptionalChecks(
            ChannelOpenOptionalValidationParameters.FromAcceptChannel1Payload(
                payload, tempChannel.ChannelConfig.ChannelReserveAmount));

        // Perform mandatory checks for the channel
        _channelOpenValidator.PerformMandatoryChecks(ChannelOpenMandatoryValidationParameters.FromAcceptChannel1Payload(
                                                         message.ChannelTypeTlv,
                                                         tempChannel.ChannelConfig.FeeRateAmountPerKw,
                                                         negotiatedFeatures, payload), out var minimumDepth);

        if (minimumDepth != tempChannel.ChannelConfig.MinimumDepth)
            throw new ChannelErrorException("Minimum depth is not acceptable", payload.ChannelId);

        // Check for the upfront shutdown script
        if (message.UpfrontShutdownScriptTlv is null
         && (negotiatedFeatures.UpfrontShutdownScript > FeatureSupport.No || message.ChannelTypeTlv is not null))
            throw new ChannelErrorException("Upfront shutdown script is required but not provided");

        BitcoinScript? remoteUpfrontShutdownScript = null;
        if (message.UpfrontShutdownScriptTlv is not null && message.UpfrontShutdownScriptTlv.Value.Length > 0)
            remoteUpfrontShutdownScript = message.UpfrontShutdownScriptTlv.Value;

        // Create the remote key set from the message
        var remoteKeySet = ChannelKeySetModel.CreateForRemote(message.Payload.FundingPubKey,
                                                              message.Payload.RevocationBasepoint,
                                                              message.Payload.PaymentBasepoint,
                                                              message.Payload.DelayedPaymentBasepoint,
                                                              message.Payload.HtlcBasepoint,
                                                              message.Payload.FirstPerCommitmentPoint);

        tempChannel.AddRemoteKeySet(remoteKeySet);

        // Create a new ChannelConfig with the remote provided values
        var channelConfig = new ChannelConfig(tempChannel.ChannelConfig.ChannelReserveAmount,
                                              tempChannel.ChannelConfig.FeeRateAmountPerKw,
                                              tempChannel.ChannelConfig.HtlcMinimumAmount,
                                              tempChannel.ChannelConfig.LocalDustLimitAmount,
                                              tempChannel.ChannelConfig.MaxAcceptedHtlcs,
                                              tempChannel.ChannelConfig.MaxHtlcAmountInFlight,
                                              tempChannel.ChannelConfig.MinimumDepth,
                                              tempChannel.ChannelConfig.OptionAnchorOutputs,
                                              payload.DustLimitAmount, tempChannel.ChannelConfig.ToSelfDelay,
                                              tempChannel.ChannelConfig.UseScidAlias,
                                              tempChannel.ChannelConfig.LocalUpfrontShutdownScript,
                                              remoteUpfrontShutdownScript);

        tempChannel.UpdateChannelConfig(channelConfig);

        // Generate the correct commitment number
        var commitmentNumber = new CommitmentNumber(tempChannel.LocalKeySet.PaymentCompactBasepoint,
                                                    remoteKeySet.PaymentCompactBasepoint, _sha256);

        tempChannel.AddCommitmentNumber(commitmentNumber);

        try
        {
            var fundingAmount = tempChannel.LocalBalance + tempChannel.RemoteBalance;
            var fundingOutput = new FundingOutputInfo(fundingAmount, tempChannel.LocalKeySet.FundingCompactPubKey,
                                                      remoteKeySet.FundingCompactPubKey);

            tempChannel.AddFundingOutput(fundingOutput);

            // Get the utxos to create the funding transaction
            var utxos = _utxoMemoryRepository.GetLockedUtxosForChannel(tempChannel.ChannelId);

            // Get a change address in case we need one
            var walletAddress = await _bitcoinWalletService.GetUnusedAddressAsync(AddressType.P2Wpkh, true);

            // Create the funding transaction
            var fundingTransactionModel = _fundingTransactionModelFactory.Create(tempChannel, utxos, walletAddress);
            _ = _fundingTransactionBuilder.Build(fundingTransactionModel);
            if (fundingOutput.TransactionId is null || fundingOutput.Index is null)
                throw new ChannelErrorException("Error building the funding transaction");

            // If a change was needed, save the change data to the channel
            if (fundingTransactionModel.ChangeAddress is not null)
                tempChannel.ChangeAddress = fundingTransactionModel.ChangeAddress;

            // Create a new channelId
            var oldChannelId = tempChannel.ChannelId;
            tempChannel.UpdateChannelId(
                _channelIdFactory.CreateV1(fundingOutput.TransactionId.Value, fundingOutput.Index.Value));

            // Register the channel with the signer
            _lightningSigner.RegisterChannel(tempChannel.ChannelId, tempChannel.GetSigningInfo());

            // Generate the base commitment transactions
            var remoteCommitmentTransaction =
                _commitmentTransactionModelFactory.CreateCommitmentTransactionModel(tempChannel, CommitmentSide.Remote);

            // Build the output and the transactions
            var remoteUnsignedCommitmentTransaction = _commitmentTransactionBuilder.Build(remoteCommitmentTransaction);

            // Sign our remote commitment transaction
            var ourSignature =
                _lightningSigner.SignChannelTransaction(tempChannel.ChannelId, remoteUnsignedCommitmentTransaction);

            // Update the channel with the new signature and the new state
            tempChannel.UpdateLastSentSignature(ourSignature);
            tempChannel.UpdateState(ChannelState.V1FundingCreated);

            // Save to the database
            await PersistChannelAsync(tempChannel);

            // Create the funding created message
            var fundingCreatedMessage =
                _messageFactory.CreateFundingCreatedMessage(oldChannelId, fundingOutput.TransactionId.Value,
                                                            fundingOutput.Index.Value, ourSignature);

            // Add the channel to the dictionary
            _channelMemoryRepository.AddChannel(tempChannel);

            // Remove the temporary channel
            _channelMemoryRepository.RemoveTemporaryChannel(peerPubKey, oldChannelId);

            return fundingCreatedMessage;
        }
        catch (Exception e)
        {
            throw new ChannelErrorException("Error creating commitment transaction", e);
        }
    }

    /// <summary>
    /// Persists a channel to the database using a scoped Unit of Work
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