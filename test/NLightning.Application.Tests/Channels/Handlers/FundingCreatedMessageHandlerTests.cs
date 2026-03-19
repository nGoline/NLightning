using Microsoft.Extensions.Logging;
using NLightning.Tests.Utils.Mocks;

namespace NLightning.Application.Tests.Channels.Handlers;

using Application.Channels.Handlers;
using Domain.Bitcoin.Interfaces;
using Domain.Bitcoin.Transactions.Enums;
using Domain.Bitcoin.Transactions.Interfaces;
using Domain.Bitcoin.Transactions.Models;
using Domain.Bitcoin.Transactions.Outputs;
using Domain.Bitcoin.ValueObjects;
using Domain.Channels.Enums;
using Domain.Channels.Interfaces;
using Domain.Channels.Models;
using Domain.Channels.ValueObjects;
using Domain.Crypto.ValueObjects;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Money;
using Domain.Node.Options;
using Domain.Persistence.Interfaces;
using Domain.Protocol.Interfaces;
using Domain.Protocol.Messages;
using Domain.Protocol.Models;
using Domain.Protocol.Payloads;
using Infrastructure.Bitcoin.Builders.Interfaces;
using Infrastructure.Bitcoin.Wallet.Interfaces;

public class FundingCreatedMessageHandlerTests
{
    private readonly Mock<IBlockchainMonitor> _mockBlockchainMonitor;
    private readonly Mock<IChannelMemoryRepository> _mockChannelMemoryRepository;
    private readonly Mock<ILightningSigner> _mockLightningSigner;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IChannelDbRepository> _mockChannelDbRepository;
    private readonly FundingCreatedMessageHandler _handler;
    private readonly CompactPubKey _peerPubKey;
    private readonly FundingCreatedMessage _validMessage;
    private readonly FeatureOptions _negotiatedFeatures;
    private readonly ChannelModel _channel;
    private readonly ChannelId _tempChannelId;
    private readonly ChannelId _newChannelId;
    private readonly TxId _fundingTxId;
    private readonly ushort _fundingOutputIndex;
    private readonly CompactSignature _remoteSignature;

    public FundingCreatedMessageHandlerTests()
    {
        _mockBlockchainMonitor = new Mock<IBlockchainMonitor>();
        var mockChannelIdFactory = new Mock<IChannelIdFactory>();
        _mockChannelMemoryRepository = new Mock<IChannelMemoryRepository>();
        var mockCommitmentTransactionBuilder = new Mock<ICommitmentTransactionBuilder>();
        var mockCommitmentTransactionModelFactory = new Mock<ICommitmentTransactionModelFactory>();
        _mockLightningSigner = new Mock<ILightningSigner>();
        var mockLogger = new Mock<ILogger<FundingCreatedMessageHandler>>();
        var mockMessageFactory = new Mock<IMessageFactory>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockChannelDbRepository = new Mock<IChannelDbRepository>();

        _mockUnitOfWork.Setup(x => x.ChannelDbRepository).Returns(_mockChannelDbRepository.Object);

        _handler = new FundingCreatedMessageHandler(_mockBlockchainMonitor.Object, mockChannelIdFactory.Object,
                                                    _mockChannelMemoryRepository.Object,
                                                    mockCommitmentTransactionBuilder.Object,
                                                    mockCommitmentTransactionModelFactory.Object,
                                                    _mockLightningSigner.Object, mockLogger.Object,
                                                    mockMessageFactory.Object, _mockUnitOfWork.Object);
        // Setup test data
        CompactPubKey emptyPubKey = new byte[]
        {
            0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00
        };
        _peerPubKey = emptyPubKey;
        _negotiatedFeatures = new FeatureOptions();
        _tempChannelId = ChannelId.Zero;
        byte[] newChannelBytes = _tempChannelId;
        newChannelBytes[0] = 1;
        _newChannelId = newChannelBytes;
        _fundingTxId = TxId.One;
        byte[] initialFundingTxIdBytes = _fundingTxId;
        initialFundingTxIdBytes[0] = 0x03;
        var initialFundingTxId = new TxId(initialFundingTxIdBytes);
        _fundingOutputIndex = 0;
        _remoteSignature = new CompactSignature(new byte[64]);
        byte[] localSignatureBytes = _remoteSignature;
        localSignatureBytes[0] = 1;
        var localSignature = new CompactSignature(localSignatureBytes);
        var fundingAmount = LightningMoney.Satoshis(10_000);
        var commitmentNumber = new CommitmentNumber(emptyPubKey, emptyPubKey, new FakeSha256());
        var fundingOutputInfo = new FundingOutputInfo(fundingAmount, emptyPubKey, emptyPubKey)
        {
            TransactionId = initialFundingTxId,
            Index = 5
        };

        // Create a valid FundingCreatedMessage
        var payload = new FundingCreatedPayload(_tempChannelId, _fundingTxId, _fundingOutputIndex, _remoteSignature);
        _validMessage = new FundingCreatedMessage(payload);

        // Setup mock channel
        var channelConfig = new ChannelConfig(LightningMoney.Zero, LightningMoney.Zero, LightningMoney.Zero,
                                              LightningMoney.Zero, 0, LightningMoney.Zero, 3, false,
                                              LightningMoney.Zero, 144, FeatureSupport.No);
        var keySet = new ChannelKeySetModel(0, emptyPubKey, emptyPubKey, emptyPubKey, emptyPubKey, emptyPubKey,
                                            emptyPubKey);
        _channel = new ChannelModel(channelConfig, _tempChannelId, commitmentNumber, fundingOutputInfo, false, null,
                                    null, LightningMoney.Zero, keySet, 1, 0, fundingAmount, keySet, 1,
                                    _peerPubKey, 0, ChannelState.V1Opening, ChannelVersion.V1);

        // Setup ChannelIdFactory
        mockChannelIdFactory.Setup(x => x.CreateV1(It.IsAny<TxId>(), It.IsAny<ushort>()))
                            .Returns(_newChannelId);

        // Setup mock commitment transactions
        var mockLocalCommitmentTx =
            new CommitmentTransactionModel(commitmentNumber, LightningMoney.Zero, fundingOutputInfo);
        var mockRemoteCommitmentTx =
            new CommitmentTransactionModel(commitmentNumber, LightningMoney.Zero, fundingOutputInfo);

        mockCommitmentTransactionModelFactory
           .Setup(x => x.CreateCommitmentTransactionModel(It.IsAny<ChannelModel>(), CommitmentSide.Local))
           .Returns(mockLocalCommitmentTx);

        mockCommitmentTransactionModelFactory
           .Setup(x => x.CreateCommitmentTransactionModel(It.IsAny<ChannelModel>(), CommitmentSide.Remote))
           .Returns(mockRemoteCommitmentTx);

        // Setup mock transactions
        var mockLocalUnsignedTx = new SignedTransaction(TxId.Zero, [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07]);
        var mockRemoteUnsignedTx = new SignedTransaction(TxId.One, [0x01, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07]);

        mockCommitmentTransactionBuilder
           .Setup(x => x.Build(mockLocalCommitmentTx))
           .Returns(mockLocalUnsignedTx);

        mockCommitmentTransactionBuilder
           .Setup(x => x.Build(mockRemoteCommitmentTx))
           .Returns(mockRemoteUnsignedTx);

        // Setup LightningSigner
        _mockLightningSigner
           .Setup(x => x.SignChannelTransaction(It.IsAny<ChannelId>(), It.IsAny<SignedTransaction>()))
           .Returns(localSignature);

        // Setup MessageFactory
        mockMessageFactory
           .Setup(x => x.CreateFundingSignedMessage(It.IsAny<ChannelId>(), It.IsAny<CompactSignature>()))
           .Returns(new FundingSignedMessage(new FundingSignedPayload(_newChannelId, localSignature)));

        // Setup ChannelDbRepository
        _mockChannelDbRepository
           .Setup(x => x.GetByIdAsync(It.IsAny<ChannelId>()))
           .ReturnsAsync((ChannelModel?)null);
    }

    [Fact]
    public async Task HandleAsync_ValidMessage_ProcessesChannelAndReturnsFundingSignedMessage()
    {
        // Arrange
        _mockChannelMemoryRepository
           .Setup(x => x.TryGetTemporaryChannelState(It.IsAny<CompactPubKey>(), It.IsAny<ChannelId>(),
                                                     out It.Ref<ChannelState>.IsAny))
           .Callback((CompactPubKey _, ChannelId _, out ChannelState state) =>
            {
                state = ChannelState.V1Opening;
            })
           .Returns(true);

        _mockChannelMemoryRepository
           .Setup(x => x.TryGetTemporaryChannel(It.IsAny<CompactPubKey>(), It.IsAny<ChannelId>(),
#pragma warning disable CS8601 // Possible null reference assignment.
                                                out It.Ref<ChannelModel>.IsAny))
#pragma warning restore CS8601 // Possible null reference assignment.
           .Callback((CompactPubKey _, ChannelId _, out ChannelModel? channel) =>
            {
                channel = _channel;
            })
           .Returns(true);

        // Act
        var result = await _handler.HandleAsync(_validMessage, ChannelState.None, _negotiatedFeatures, _peerPubKey);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<FundingSignedMessage>(result);

        // Verify transaction ID and output index were set on the channel
        Assert.Equal(_fundingTxId, _channel.FundingOutput?.TransactionId);
        Assert.Equal(_fundingOutputIndex, _channel.FundingOutput?.Index);

        // Verify channel ID was updated
        Assert.Equal(_channel.ChannelId, _newChannelId);

        // Verify the channel was registered with the signer
        _mockLightningSigner.Verify(
            x => x.RegisterChannel(_newChannelId, It.IsAny<ChannelSigningInfo>()),
            Times.Once);

        // Verify signature validation was performed
        _mockLightningSigner.Verify(
            x => x.ValidateSignature(_newChannelId, _remoteSignature, It.IsAny<SignedTransaction>()),
            Times.Once);

        // Verify our signature was generated
        _mockLightningSigner.Verify(
            x => x.SignChannelTransaction(_newChannelId, It.IsAny<SignedTransaction>()),
            Times.Once);

        // Verify channel state was updated
        Assert.Equal(ChannelState.V1FundingSigned, _channel.State);

        // Verify if the channel was persisted
        _mockChannelDbRepository.Verify(x => x.AddAsync(_channel), Times.Once);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Once);

        // Verify blockchain monitoring was started
        _mockBlockchainMonitor.Verify(
            x => x.WatchTransactionAsync(_newChannelId, _fundingTxId, It.IsAny<uint>()),
            Times.Once);

        // Verify channel management operations
        _mockChannelMemoryRepository.Verify(x => x.AddChannel(_channel), Times.Once);
        _mockChannelMemoryRepository.Verify(x => x.TryRemoveTemporaryChannel(_peerPubKey, _tempChannelId), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenCurrentStateIsNotNone_ThrowsChannelErrorException()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<ChannelErrorException>(() => _handler.HandleAsync(
                                                                _validMessage, ChannelState.Open, _negotiatedFeatures,
                                                                _peerPubKey));

        Assert.Equal("A channel with this id already exists", exception.Message);
        Assert.Equal(_validMessage.Payload.ChannelId, exception.ChannelId);
    }

    [Fact]
    public async Task HandleAsync_WhenTemporaryChannelStateNotFound_ThrowsChannelErrorException()
    {
        // Arrange
        _mockChannelMemoryRepository
           .Setup(x => x.TryGetTemporaryChannelState(It.IsAny<CompactPubKey>(), It.IsAny<ChannelId>(),
                                                     out It.Ref<ChannelState>.IsAny))
           .Returns(false);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<ChannelErrorException>(() => _handler.HandleAsync(
                                                                _validMessage, ChannelState.None, _negotiatedFeatures,
                                                                _peerPubKey));

        Assert.Equal("This channel has never been negotiated", exception.Message);
        Assert.Equal(_validMessage.Payload.ChannelId, exception.ChannelId);
    }

    [Fact]
    public async Task HandleAsync_WhenTemporaryChannelStateIsWrong_ThrowsChannelErrorException()
    {
        // Arrange
        _mockChannelMemoryRepository
           .Setup(x => x.TryGetTemporaryChannelState(It.IsAny<CompactPubKey>(), It.IsAny<ChannelId>(),
                                                     out It.Ref<ChannelState>.IsAny))
           .Callback((CompactPubKey _, ChannelId _, out ChannelState state) =>
            {
                state = ChannelState.Open; // Wrong state
            })
           .Returns(true);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<ChannelErrorException>(() => _handler.HandleAsync(
                                                                _validMessage, ChannelState.None, _negotiatedFeatures,
                                                                _peerPubKey));

        Assert.Equal("Channel had the wrong state", exception.Message);
        Assert.Equal("This channel is already being negotiated with peer", exception.PeerMessage);
        Assert.Equal(_validMessage.Payload.ChannelId, exception.ChannelId);
    }

    [Fact]
    public async Task HandleAsync_WhenTemporaryChannelNotFound_ThrowsChannelErrorException()
    {
        // Arrange
        _mockChannelMemoryRepository
           .Setup(x => x.TryGetTemporaryChannelState(It.IsAny<CompactPubKey>(), It.IsAny<ChannelId>(),
                                                     out It.Ref<ChannelState>.IsAny))
           .Callback((CompactPubKey _, ChannelId _, out ChannelState state) =>
            {
                state = ChannelState.V1Opening;
            })
           .Returns(true);

        _mockChannelMemoryRepository
           .Setup(x => x.TryGetTemporaryChannel(It.IsAny<CompactPubKey>(), It.IsAny<ChannelId>(),
                                                out It.Ref<ChannelModel>.IsAny!))
           .Returns(false);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<ChannelErrorException>(() => _handler.HandleAsync(
                                                                _validMessage, ChannelState.None, _negotiatedFeatures,
                                                                _peerPubKey));

        Assert.Equal("Temporary channel not found", exception.Message);
        Assert.Equal(_validMessage.Payload.ChannelId, exception.ChannelId);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelAlreadyExists_ThrowsChannelWarningException()
    {
        // Arrange
        _mockChannelMemoryRepository
           .Setup(x => x.TryGetTemporaryChannelState(It.IsAny<CompactPubKey>(), It.IsAny<ChannelId>(),
                                                     out It.Ref<ChannelState>.IsAny))
           .Callback((CompactPubKey _, ChannelId _, out ChannelState state) =>
            {
                state = ChannelState.V1Opening;
            })
           .Returns(true);

        _mockChannelMemoryRepository
           .Setup(x => x.TryGetTemporaryChannel(It.IsAny<CompactPubKey>(), It.IsAny<ChannelId>(),
                                                out It.Ref<ChannelModel>.IsAny!))
           .Callback((CompactPubKey _, ChannelId _, out ChannelModel? channel) =>
            {
                channel = _channel;
            })
           .Returns(true);

        // Channel already exists in database
        _mockChannelDbRepository
           .Setup(x => x.GetByIdAsync(It.IsAny<ChannelId>()))
           .ReturnsAsync(_channel);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<ChannelWarningException>(() => _handler.HandleAsync(
                                                                  _validMessage, ChannelState.None, _negotiatedFeatures,
                                                                  _peerPubKey));

        Assert.Equal("Channel already exists", exception.Message);
        Assert.Equal(_newChannelId, exception.ChannelId);
        Assert.Equal("This channel is already in our database", exception.PeerMessage);
    }
}