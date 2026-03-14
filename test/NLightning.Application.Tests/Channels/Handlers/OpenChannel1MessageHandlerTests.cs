using Microsoft.Extensions.Logging;
using NLightning.Application.Channels.Handlers;
using NLightning.Domain.Bitcoin.Transactions.Outputs;
using NLightning.Domain.Bitcoin.ValueObjects;
using NLightning.Domain.Channels.Enums;
using NLightning.Domain.Channels.Interfaces;
using NLightning.Domain.Channels.Models;
using NLightning.Domain.Channels.ValueObjects;
using NLightning.Domain.Crypto.ValueObjects;
using NLightning.Domain.Enums;
using NLightning.Domain.Exceptions;
using NLightning.Domain.Money;
using NLightning.Domain.Node;
using NLightning.Domain.Node.Options;
using NLightning.Domain.Protocol.Interfaces;
using NLightning.Domain.Protocol.Messages;
using NLightning.Domain.Protocol.Models;
using NLightning.Domain.Protocol.Payloads;
using NLightning.Domain.Protocol.Tlv;
using NLightning.Domain.Protocol.ValueObjects;
using NLightning.Tests.Utils.Mocks;

namespace NLightning.Application.Tests.Channels.Handlers;

public class OpenChannel1MessageHandlerTests
{
    private readonly Mock<IChannelFactory> _mockChannelFactory;
    private readonly Mock<IChannelMemoryRepository> _mockChannelMemoryRepository;
    private readonly Mock<IMessageFactory> _mockMessageFactory;
    private readonly OpenChannel1MessageHandler _handler;
    private readonly CompactPubKey _peerPubKey;
    private readonly OpenChannel1Message _validMessage;
    private readonly FeatureOptions _negotiatedFeatures;
    private readonly ChannelModel _channel;

    public OpenChannel1MessageHandlerTests()
    {
        _mockChannelFactory = new Mock<IChannelFactory>();
        _mockChannelMemoryRepository = new Mock<IChannelMemoryRepository>();
        _mockMessageFactory = new Mock<IMessageFactory>();

        _handler = new OpenChannel1MessageHandler(_mockChannelFactory.Object, _mockChannelMemoryRepository.Object,
                                                  new Mock<ILogger<OpenChannel1MessageHandler>>().Object,
                                                  _mockMessageFactory.Object);

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

        var channelReserveAmount = LightningMoney.Satoshis(1_000);
        var dustLimitAmount = LightningMoney.Satoshis(354);
        var feeRateAmountPerKw = LightningMoney.Zero;
        var htlcMinimumAmount = LightningMoney.Satoshis(1);
        var maxAcceptedHtlcs = (ushort)10;
        var maxHtlcAmountInFlight = LightningMoney.Satoshis(10_000);
        var toSelfDelay = (ushort)144;
        var fundingAmount = LightningMoney.Satoshis(10_000);

        // Create a valid OpenChannel1Message
        var channelId = ChannelId.Zero;
        var payload =
            new OpenChannel1Payload(BitcoinNetwork.Mainnet.ChainHash, new ChannelFlags(ChannelFlag.AnnounceChannel),
                                    channelId, channelReserveAmount, emptyPubKey, dustLimitAmount, feeRateAmountPerKw,
                                    emptyPubKey, fundingAmount, emptyPubKey, emptyPubKey, htlcMinimumAmount,
                                    maxAcceptedHtlcs, maxHtlcAmountInFlight, emptyPubKey, LightningMoney.Zero,
                                    emptyPubKey, toSelfDelay);
        _validMessage =
            new OpenChannel1Message(payload, new ChannelTypeTlv(FeatureSet.NewBasicChannelType().GetBytes()!));

        // Setup ChannelConfig
        var channelConfig = new ChannelConfig(channelReserveAmount, feeRateAmountPerKw, htlcMinimumAmount,
                                              dustLimitAmount, maxAcceptedHtlcs, maxHtlcAmountInFlight, 3, false,
                                              dustLimitAmount, toSelfDelay, FeatureSupport.No);

        // Create a real ChannelKeySetModel instance instead of mocking it
        var keySet = new ChannelKeySetModel(0, emptyPubKey, emptyPubKey, emptyPubKey, emptyPubKey, emptyPubKey,
                                            emptyPubKey);

        // Setup channel and related objects
        _channel = new ChannelModel(channelConfig, channelId,
                                    new CommitmentNumber(emptyPubKey, emptyPubKey, new FakeSha256()),
                                    new FundingOutputInfo(fundingAmount, emptyPubKey, emptyPubKey), false, null,
                                    null, LightningMoney.Zero, keySet, 1, 0, fundingAmount, keySet, 1,
                                    _peerPubKey, 0, ChannelState.V1Opening, ChannelVersion.V1);

        // Setup factory to return our mocked channel
        _mockChannelFactory
           .Setup(x => x.CreateChannelV1AsNonInitiatorAsync(It.IsAny<OpenChannel1Message>(), It.IsAny<FeatureOptions>(),
                                                            It.IsAny<CompactPubKey>()))
           .ReturnsAsync(_channel);

        // Setup message factory
        _mockMessageFactory
           .Setup(x => x.CreateAcceptChannel1Message(It.IsAny<LightningMoney>(), It.IsAny<ChannelTypeTlv>(),
                                                     It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                                     It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                                     It.IsAny<ushort>(), It.IsAny<LightningMoney>(), It.IsAny<uint>(),
                                                     It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                                     It.IsAny<ChannelId>(), It.IsAny<ushort>(),
                                                     It.IsAny<UpfrontShutdownScriptTlv>()))
           .Returns(new AcceptChannel1Message(
                        new AcceptChannel1Payload(channelId, channelReserveAmount, emptyPubKey, dustLimitAmount,
                                                  emptyPubKey, emptyPubKey, emptyPubKey, htlcMinimumAmount,
                                                  maxAcceptedHtlcs, maxHtlcAmountInFlight, 3, emptyPubKey,
                                                  emptyPubKey, toSelfDelay),
                        new ChannelTypeTlv(FeatureSet.NewBasicChannelType().GetBytes()!)));
    }

    [Fact]
    public async Task HandleAsync_ValidMessage_CreatesChannelAndReturnsAcceptChannelMessage()
    {
        // Arrange
        _mockChannelMemoryRepository
           .Setup(x => x.TryGetTemporaryChannelState(It.IsAny<CompactPubKey>(), It.IsAny<ChannelId>(),
                                                     out It.Ref<ChannelState>.IsAny))
           .Returns(false);

        // Act
        var result = await _handler.HandleAsync(_validMessage, ChannelState.None, _negotiatedFeatures, _peerPubKey);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AcceptChannel1Message>(result);

        _mockChannelFactory.Verify(
            x => x.CreateChannelV1AsNonInitiatorAsync(_validMessage, _negotiatedFeatures, _peerPubKey),
            Times.Once);

        _mockChannelMemoryRepository.Verify(x => x.AddTemporaryChannel(_peerPubKey, _channel), Times.Once);

        _mockMessageFactory.Verify(
            x => x.CreateAcceptChannel1Message(
                _channel.ChannelConfig.ChannelReserveAmount!, null,
                _channel.LocalKeySet.DelayedPaymentCompactBasepoint,
                _channel.LocalKeySet.CurrentPerCommitmentCompactPoint,
                _channel.LocalKeySet.FundingCompactPubKey, _channel.LocalKeySet.HtlcCompactBasepoint,
                _channel.ChannelConfig.MaxAcceptedHtlcs, _channel.ChannelConfig.MaxHtlcAmountInFlight,
                _channel.ChannelConfig.MinimumDepth, _channel.LocalKeySet.PaymentCompactBasepoint,
                _channel.LocalKeySet.RevocationCompactBasepoint, _channel.ChannelId,
                _channel.ChannelConfig.ToSelfDelay, It.IsAny<UpfrontShutdownScriptTlv>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithUpfrontShutdownScript_IncludesItInResponse()
    {
        // Arrange
        _mockChannelMemoryRepository
           .Setup(x => x.TryGetTemporaryChannelState(It.IsAny<CompactPubKey>(), It.IsAny<ChannelId>(),
                                                     out It.Ref<ChannelState>.IsAny))
           .Returns(false);

        // Setup channel with upfront shutdown script
        var channel = new ChannelModel(_channel.ChannelConfig, _channel.ChannelId,
                                       _channel.CommitmentNumber, _channel.FundingOutput, _channel.IsInitiator,
                                       _channel.LastSentSignature, _channel.LastReceivedSignature,
                                       _channel.LocalBalance, _channel.LocalKeySet, _channel.LocalNextHtlcId,
                                       _channel.LocalRevocationNumber, _channel.RemoteBalance,
                                       _channel.RemoteKeySet, _channel.RemoteNextHtlcId, _peerPubKey,
                                       _channel.RemoteRevocationNumber, ChannelState.V1Opening,
                                       ChannelVersion.V1, localUpfrontShutdownScript: new BitcoinScript([1, 2, 3]));

        _mockChannelFactory
           .Setup(x => x.CreateChannelV1AsNonInitiatorAsync(It.IsAny<OpenChannel1Message>(), It.IsAny<FeatureOptions>(),
                                                            It.IsAny<CompactPubKey>()))
           .ReturnsAsync(channel);

        // Act
        var result = await _handler.HandleAsync(_validMessage, ChannelState.None, _negotiatedFeatures, _peerPubKey);

        // Assert
        Assert.NotNull(result);

        _mockMessageFactory.Verify(
            x => x.CreateAcceptChannel1Message(It.IsAny<LightningMoney>(), It.IsAny<ChannelTypeTlv>(),
                                               It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                               It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                               It.IsAny<ushort>(), It.IsAny<LightningMoney>(), It.IsAny<uint>(),
                                               It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                               It.IsAny<ChannelId>(), It.IsAny<ushort>(),
                                               It.IsNotNull<UpfrontShutdownScriptTlv>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelStateIsNotNone_ThrowsChannelErrorException()
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
    public async Task HandleAsync_WithExistingTemporaryChannelInWrongState_ThrowsChannelErrorException()
    {
        // Arrange
        var outState = ChannelState.None;
        _mockChannelMemoryRepository
           .Setup(x => x.TryGetTemporaryChannelState(It.IsAny<CompactPubKey>(), It.IsAny<ChannelId>(), out outState))
           .Callback((CompactPubKey _, ChannelId _, out ChannelState state) =>
            {
                state = ChannelState.Open; // Wrong state
                outState = state;
            })
           .Returns(true);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<ChannelErrorException>(() => _handler.HandleAsync(
                                                                _validMessage, ChannelState.None, _negotiatedFeatures,
                                                                _peerPubKey));

        Assert.Equal("Channel had the wrong state", exception.Message);
        Assert.Equal("This channel is already being negotiated with peer", exception.PeerMessage);
    }

    [Fact]
    public async Task HandleAsync_WithExistingTemporaryChannelInCorrectState_ProcessesNormally()
    {
        // Arrange
        var outState = ChannelState.None;
        _mockChannelMemoryRepository
           .Setup(x => x.TryGetTemporaryChannelState(It.IsAny<CompactPubKey>(), It.IsAny<ChannelId>(), out outState))
           .Callback((CompactPubKey _, ChannelId _, out ChannelState state) =>
            {
                state = ChannelState.V1Opening; // Correct state
                outState = state;
            })
           .Returns(true);

        // Act
        var result = await _handler.HandleAsync(_validMessage, ChannelState.None, _negotiatedFeatures, _peerPubKey);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AcceptChannel1Message>(result);
    }
}