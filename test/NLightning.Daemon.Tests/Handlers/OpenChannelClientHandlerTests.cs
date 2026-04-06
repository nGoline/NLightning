using Microsoft.Extensions.Logging;

namespace NLightning.Daemon.Tests.Handlers;

using Daemon.Handlers;
using Domain.Bitcoin.Interfaces;
using Domain.Channels.Enums;
using Domain.Channels.Events;
using Domain.Channels.Interfaces;
using Domain.Channels.Models;
using Domain.Channels.ValueObjects;
using Domain.Client.Constants;
using Domain.Client.Exceptions;
using Domain.Client.Requests;
using Domain.Crypto.ValueObjects;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Money;
using Domain.Node.Events;
using Domain.Node.Interfaces;
using Domain.Node.Models;
using Domain.Node.Options;
using Domain.Node.ValueObjects;
using Domain.Protocol.Interfaces;
using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Domain.Protocol.Tlv;
using Domain.Protocol.ValueObjects;
using Infrastructure.Bitcoin.Wallet.Interfaces;

public class OpenChannelClientHandlerTests
{
    private readonly Mock<IBlockchainMonitor> _blockchainMonitorMock;
    private readonly Mock<IChannelFactory> _channelFactoryMock;
    private readonly Mock<IChannelMemoryRepository> _channelMemoryRepositoryMock;
    private readonly Mock<IMessageFactory> _messageFactoryMock;
    private readonly Mock<IPeerManager> _peerManagerMock;
    private readonly Mock<IUtxoMemoryRepository> _utxoMemoryRepositoryMock;
    private readonly OpenChannelClientHandler _handler;

    public OpenChannelClientHandlerTests()
    {
        _blockchainMonitorMock = new Mock<IBlockchainMonitor>();
        _channelFactoryMock = new Mock<IChannelFactory>();
        _channelMemoryRepositoryMock = new Mock<IChannelMemoryRepository>();
        var loggerMock = new Mock<ILogger<OpenChannelClientHandler>>();
        _messageFactoryMock = new Mock<IMessageFactory>();
        _peerManagerMock = new Mock<IPeerManager>();
        _utxoMemoryRepositoryMock = new Mock<IUtxoMemoryRepository>();

        _handler = new OpenChannelClientHandler(
            _blockchainMonitorMock.Object,
            _channelFactoryMock.Object,
            _channelMemoryRepositoryMock.Object,
            loggerMock.Object,
            _messageFactoryMock.Object,
            _peerManagerMock.Object,
            _utxoMemoryRepositoryMock.Object
        );
    }

    [Fact]
    public async Task GivenValidRequest_WhenHandleAsync_ThenFollowsCompleteFlow()
    {
        // Arrange
        var peerId = CreateDummyPubKey();
        var nodeInfo = $"{peerId}@127.0.0.1:9735";
        var fundingAmount = LightningMoney.Satoshis(100000);
        var request = new OpenChannelClientRequest(nodeInfo, fundingAmount);

        var peerModel = new PeerModel(peerId, "127.0.0.1", 9735, "ipv4");
        var peerServiceMock = new Mock<IPeerService>();
        peerServiceMock.Setup(x => x.Features).Returns(new FeatureOptions());
        peerModel.SetPeerService(peerServiceMock.Object);

        _peerManagerMock.Setup(x => x.GetPeer(peerId)).Returns(peerModel);
        _blockchainMonitorMock.Setup(x => x.LastProcessedBlockHeight).Returns(100u);
        _utxoMemoryRepositoryMock.Setup(x => x.GetConfirmedBalance(100u)).Returns(LightningMoney.Satoshis(200000));

        var channelConfig = new ChannelConfig();
        var localKeySet = new ChannelKeySetModel(0, peerId, peerId, peerId, peerId, peerId, peerId);
        var channelModel = new ChannelModel(channelConfig, CreateRandomChannelId(), null, null, true, null, null,
                                            fundingAmount, localKeySet, 0, 0, LightningMoney.Zero, null, 0, peerId, 0,
                                            ChannelState.V1Opening, ChannelVersion.V1);
        var tempChannelId = channelModel.ChannelId;

        _channelFactoryMock.Setup(x => x.CreateChannelV1AsInitiatorAsync(request, It.IsAny<FeatureOptions>(), peerId))
                           .ReturnsAsync(channelModel);

        var openChannel1Message = CreateDummyOpenChannel1Message(tempChannelId, fundingAmount, peerId);
        _messageFactoryMock.Setup(x => x.CreateOpenChannel1Message(It.IsAny<ChannelId>(), It.IsAny<LightningMoney>(),
                                                                   It.IsAny<CompactPubKey>(),
                                                                   It.IsAny<LightningMoney>(),
                                                                   It.IsAny<LightningMoney>(),
                                                                   It.IsAny<LightningMoney>(), It.IsAny<ushort>(),
                                                                   It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                                                   It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                                                   It.IsAny<CompactPubKey>(), It.IsAny<ChannelFlags>(),
                                                                   It.IsAny<ChannelTypeTlv>(),
                                                                   It.IsAny<UpfrontShutdownScriptTlv>()))
                           .Returns(openChannel1Message);

        peerServiceMock.Setup(x => x.SendMessageAsync(It.IsAny<IChannelMessage>())).Returns(Task.CompletedTask);

        var finalChannelId = CreateRandomChannelId();

        // Act
        var handleTask = _handler.HandleAsync(request, CancellationToken.None);

        // Wait a bit to ensure it reached the `await tsc.Task`
        await Task.Delay(100, TestContext.Current.CancellationToken);

        _channelMemoryRepositoryMock.Raise(x => x.OnChannelUpgraded += null, null!,
                                           new ChannelUpgradedEventArgs(tempChannelId, finalChannelId));

        var response = await handleTask;

        // Assert
        Assert.Equal(finalChannelId, response.ChannelId);
        _peerManagerMock.Verify(x => x.GetPeer(peerId), Times.Once);
        _utxoMemoryRepositoryMock.Verify(x => x.LockUtxosToSpendOnChannel(fundingAmount, tempChannelId), Times.Once);
        _channelMemoryRepositoryMock.Verify(x => x.AddTemporaryChannel(peerId, channelModel), Times.Once);
        peerServiceMock.Verify(x => x.SendMessageAsync(openChannel1Message), Times.Once);
    }

    [Fact]
    public async Task GivenPeerNotConnected_WhenHandleAsync_ThenConnectsToPeer()
    {
        // Arrange
        var peerId = CreateDummyPubKey();
        var nodeInfo = $"{peerId}@127.0.0.1:9735";
        var fundingAmount = LightningMoney.Satoshis(100000);
        var request = new OpenChannelClientRequest(nodeInfo, fundingAmount);

        var peerModel = new PeerModel(peerId, "127.0.0.1", 9735, "ipv4");
        var peerServiceMock = new Mock<IPeerService>();
        peerServiceMock.Setup(x => x.Features).Returns(new FeatureOptions());
        peerModel.SetPeerService(peerServiceMock.Object);

        // Peer is not found initially
        _peerManagerMock.Setup(x => x.GetPeer(peerId)).Returns((PeerModel?)null);
        _peerManagerMock.Setup(x => x.ConnectToPeerAsync(It.IsAny<PeerAddressInfo>())).ReturnsAsync(peerModel);

        _blockchainMonitorMock.Setup(x => x.LastProcessedBlockHeight).Returns(100u);
        _utxoMemoryRepositoryMock.Setup(x => x.GetConfirmedBalance(100u)).Returns(LightningMoney.Satoshis(200000));

        var channelModel = new ChannelModel(new ChannelConfig(), CreateRandomChannelId(), null, null, true, null, null,
                                            fundingAmount,
                                            new ChannelKeySetModel(0, peerId, peerId, peerId, peerId, peerId, peerId),
                                            0, 0, LightningMoney.Zero, null, 0, peerId, 0, ChannelState.V1Opening,
                                            ChannelVersion.V1);
        var tempChannelId = channelModel.ChannelId;
        _channelFactoryMock.Setup(x => x.CreateChannelV1AsInitiatorAsync(request, It.IsAny<FeatureOptions>(), peerId))
                           .ReturnsAsync(channelModel);
        _messageFactoryMock.Setup(x => x.CreateOpenChannel1Message(It.IsAny<ChannelId>(), It.IsAny<LightningMoney>(),
                                                                   It.IsAny<CompactPubKey>(),
                                                                   It.IsAny<LightningMoney>(),
                                                                   It.IsAny<LightningMoney>(),
                                                                   It.IsAny<LightningMoney>(), It.IsAny<ushort>(),
                                                                   It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                                                   It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                                                   It.IsAny<CompactPubKey>(), It.IsAny<ChannelFlags>(),
                                                                   It.IsAny<ChannelTypeTlv>(),
                                                                   It.IsAny<UpfrontShutdownScriptTlv>()))
                           .Returns(CreateDummyOpenChannel1Message(tempChannelId, fundingAmount, peerId));
        peerServiceMock.Setup(x => x.SendMessageAsync(It.IsAny<IChannelMessage>())).Returns(Task.CompletedTask);

        var finalChannelId = CreateRandomChannelId();

        // Act
        var handleTask = _handler.HandleAsync(request, CancellationToken.None);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        _channelMemoryRepositoryMock.Raise(x => x.OnChannelUpgraded += null, null!,
                                           new ChannelUpgradedEventArgs(tempChannelId, finalChannelId));
        await handleTask;

        // Assert
        _peerManagerMock.Verify(x => x.ConnectToPeerAsync(It.Is<PeerAddressInfo>(p => p.Address == nodeInfo)),
                                Times.Once);
    }

    [Fact]
    public async Task GivenInsufficientBalance_WhenHandleAsync_ThenThrowsClientException()
    {
        // Arrange
        var peerId = CreateDummyPubKey();
        var nodeInfo = $"{peerId}@127.0.0.1:9735";
        var fundingAmount = LightningMoney.Satoshis(100000);
        var request = new OpenChannelClientRequest(nodeInfo, fundingAmount);

        var peerModel = new PeerModel(peerId, "127.0.0.1", 9735, "ipv4");
        _peerManagerMock.Setup(x => x.GetPeer(peerId)).Returns(peerModel);
        _blockchainMonitorMock.Setup(x => x.LastProcessedBlockHeight).Returns(100u);
        _utxoMemoryRepositoryMock.Setup(x => x.GetConfirmedBalance(100u)).Returns(LightningMoney.Satoshis(50000));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ClientException>(() => _handler.HandleAsync(request, CancellationToken.None));
        Assert.Equal(ErrorCodes.NotEnoughBalance, ex.ErrorCode);
    }

    [Fact]
    public async Task GivenInvalidNodeInfo_WhenHandleAsync_ThenThrowsClientException()
    {
        // Arrange
        var request = new OpenChannelClientRequest("", LightningMoney.Satoshis(100000));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ClientException>(() => _handler.HandleAsync(request, CancellationToken.None));
        Assert.Equal(ErrorCodes.InvalidAddress, ex.ErrorCode);
    }

    [Fact]
    public async Task GivenPeerDisconnection_WhenHandleAsync_ThenThrowsConnectionException()
    {
        // Arrange
        var peerId = CreateDummyPubKey();
        var nodeInfo = $"{peerId}@127.0.0.1:9735";
        var fundingAmount = LightningMoney.Satoshis(100000);
        var request = new OpenChannelClientRequest(nodeInfo, fundingAmount);

        var peerModel = new PeerModel(peerId, "127.0.0.1", 9735, "ipv4");
        var peerServiceMock = new Mock<IPeerService>();
        peerServiceMock.Setup(x => x.Features).Returns(new FeatureOptions());
        peerModel.SetPeerService(peerServiceMock.Object);

        _peerManagerMock.Setup(x => x.GetPeer(peerId)).Returns(peerModel);
        _blockchainMonitorMock.Setup(x => x.LastProcessedBlockHeight).Returns(100u);
        _utxoMemoryRepositoryMock.Setup(x => x.GetConfirmedBalance(100u)).Returns(LightningMoney.Satoshis(200000));

        var channelModel = new ChannelModel(new ChannelConfig(), CreateRandomChannelId(), null, null, true, null, null,
                                            fundingAmount,
                                            new ChannelKeySetModel(0, peerId, peerId, peerId, peerId, peerId, peerId),
                                            0, 0, LightningMoney.Zero, null, 0, peerId, 0, ChannelState.V1Opening,
                                            ChannelVersion.V1);
        _channelFactoryMock.Setup(x => x.CreateChannelV1AsInitiatorAsync(request, It.IsAny<FeatureOptions>(), peerId))
                           .ReturnsAsync(channelModel);
        _messageFactoryMock.Setup(x => x.CreateOpenChannel1Message(It.IsAny<ChannelId>(), It.IsAny<LightningMoney>(),
                                                                   It.IsAny<CompactPubKey>(),
                                                                   It.IsAny<LightningMoney>(),
                                                                   It.IsAny<LightningMoney>(),
                                                                   It.IsAny<LightningMoney>(), It.IsAny<ushort>(),
                                                                   It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                                                   It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                                                   It.IsAny<CompactPubKey>(), It.IsAny<ChannelFlags>(),
                                                                   It.IsAny<ChannelTypeTlv>(),
                                                                   It.IsAny<UpfrontShutdownScriptTlv>()))
                           .Returns(CreateDummyOpenChannel1Message(channelModel.ChannelId, fundingAmount, peerId));

        // Act
        var handleTask = _handler.HandleAsync(request, CancellationToken.None);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        peerServiceMock.Raise(x => x.OnDisconnect += null, null!, new PeerDisconnectedEventArgs(peerId));

        // Assert
        await Assert.ThrowsAsync<ConnectionException>(() => handleTask);
        _utxoMemoryRepositoryMock.Verify(x => x.ReturnUtxosNotSpentOnChannel(channelModel.ChannelId), Times.Once);
    }

    [Fact]
    public async Task GivenAttentionMessage_WhenHandleAsync_ThenThrowsChannelErrorException()
    {
        // Arrange
        var peerId = CreateDummyPubKey();
        var nodeInfo = $"{peerId}@127.0.0.1:9735";
        var fundingAmount = LightningMoney.Satoshis(100000);
        var request = new OpenChannelClientRequest(nodeInfo, fundingAmount);

        var peerModel = new PeerModel(peerId, "127.0.0.1", 9735, "ipv4");
        var peerServiceMock = new Mock<IPeerService>();
        peerServiceMock.Setup(x => x.Features).Returns(new FeatureOptions());
        peerModel.SetPeerService(peerServiceMock.Object);

        _peerManagerMock.Setup(x => x.GetPeer(peerId)).Returns(peerModel);
        _blockchainMonitorMock.Setup(x => x.LastProcessedBlockHeight).Returns(100u);
        _utxoMemoryRepositoryMock.Setup(x => x.GetConfirmedBalance(100u)).Returns(LightningMoney.Satoshis(200000));

        var channelModel = new ChannelModel(new ChannelConfig(), CreateRandomChannelId(), null, null, true, null, null,
                                            fundingAmount,
                                            new ChannelKeySetModel(0, peerId, peerId, peerId, peerId, peerId, peerId),
                                            0, 0, LightningMoney.Zero, null, 0, peerId, 0, ChannelState.V1Opening,
                                            ChannelVersion.V1);
        _channelFactoryMock.Setup(x => x.CreateChannelV1AsInitiatorAsync(request, It.IsAny<FeatureOptions>(), peerId))
                           .ReturnsAsync(channelModel);
        _messageFactoryMock.Setup(x => x.CreateOpenChannel1Message(It.IsAny<ChannelId>(), It.IsAny<LightningMoney>(),
                                                                   It.IsAny<CompactPubKey>(),
                                                                   It.IsAny<LightningMoney>(),
                                                                   It.IsAny<LightningMoney>(),
                                                                   It.IsAny<LightningMoney>(), It.IsAny<ushort>(),
                                                                   It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                                                   It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                                                   It.IsAny<CompactPubKey>(), It.IsAny<ChannelFlags>(),
                                                                   It.IsAny<ChannelTypeTlv>(),
                                                                   It.IsAny<UpfrontShutdownScriptTlv>()))
                           .Returns(CreateDummyOpenChannel1Message(channelModel.ChannelId, fundingAmount, peerId));

        // Act
        var handleTask = _handler.HandleAsync(request, CancellationToken.None);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        peerServiceMock.Raise(x => x.OnAttentionMessageReceived += null, null!,
                              new AttentionMessageEventArgs("Error Message", peerId,
                                                            channelModel.ChannelId));

        // Assert
        await Assert.ThrowsAsync<ChannelErrorException>(() => handleTask);
        _utxoMemoryRepositoryMock.Verify(x => x.ReturnUtxosNotSpentOnChannel(channelModel.ChannelId), Times.Once);
    }

    [Fact]
    public async Task GivenExceptionRaised_WhenHandleAsync_ThenThrowsException()
    {
        // Arrange
        var peerId = CreateDummyPubKey();
        var nodeInfo = $"{peerId}@127.0.0.1:9735";
        var fundingAmount = LightningMoney.Satoshis(100000);
        var request = new OpenChannelClientRequest(nodeInfo, fundingAmount);

        var peerModel = new PeerModel(peerId, "127.0.0.1", 9735, "ipv4");
        var peerServiceMock = new Mock<IPeerService>();
        peerServiceMock.Setup(x => x.Features).Returns(new FeatureOptions());
        peerModel.SetPeerService(peerServiceMock.Object);

        _peerManagerMock.Setup(x => x.GetPeer(peerId)).Returns(peerModel);
        _blockchainMonitorMock.Setup(x => x.LastProcessedBlockHeight).Returns(100u);
        _utxoMemoryRepositoryMock.Setup(x => x.GetConfirmedBalance(100u)).Returns(LightningMoney.Satoshis(200000));

        var channelModel = new ChannelModel(new ChannelConfig(), CreateRandomChannelId(), null, null, true, null, null,
                                            fundingAmount,
                                            new ChannelKeySetModel(0, peerId, peerId, peerId, peerId, peerId, peerId),
                                            0, 0, LightningMoney.Zero, null, 0, peerId, 0, ChannelState.V1Opening,
                                            ChannelVersion.V1);
        _channelFactoryMock.Setup(x => x.CreateChannelV1AsInitiatorAsync(request, It.IsAny<FeatureOptions>(), peerId))
                           .ReturnsAsync(channelModel);
        _messageFactoryMock.Setup(x => x.CreateOpenChannel1Message(It.IsAny<ChannelId>(), It.IsAny<LightningMoney>(),
                                                                   It.IsAny<CompactPubKey>(),
                                                                   It.IsAny<LightningMoney>(),
                                                                   It.IsAny<LightningMoney>(),
                                                                   It.IsAny<LightningMoney>(), It.IsAny<ushort>(),
                                                                   It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                                                   It.IsAny<CompactPubKey>(), It.IsAny<CompactPubKey>(),
                                                                   It.IsAny<CompactPubKey>(), It.IsAny<ChannelFlags>(),
                                                                   It.IsAny<ChannelTypeTlv>(),
                                                                   It.IsAny<UpfrontShutdownScriptTlv>()))
                           .Returns(CreateDummyOpenChannel1Message(channelModel.ChannelId, fundingAmount, peerId));

        // Act
        var handleTask = _handler.HandleAsync(request, CancellationToken.None);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var expectedException = new ChannelErrorException("Critical error", channelModel.ChannelId);
        peerServiceMock.Raise(x => x.OnExceptionRaised += null, null!, expectedException);

        // Assert
        var ex = await Assert.ThrowsAsync<ChannelErrorException>(() => handleTask);
        Assert.Same(expectedException, ex);
        _utxoMemoryRepositoryMock.Verify(x => x.ReturnUtxosNotSpentOnChannel(channelModel.ChannelId), Times.Once);
    }

    private static ChannelId CreateRandomChannelId()
    {
        var bytes = new byte[32];
        Random.Shared.NextBytes(bytes);
        return new ChannelId(bytes);
    }

    private static CompactPubKey CreateDummyPubKey()
    {
        var bytes = new byte[33];
        bytes[0] = 0x02;
        return new CompactPubKey(bytes);
    }

    private static OpenChannel1Message CreateDummyOpenChannel1Message(ChannelId tempChannelId,
                                                                      LightningMoney fundingAmount,
                                                                      CompactPubKey peerId)
    {
        var payload = new OpenChannel1Payload(new ChainHash(new byte[32]), new ChannelFlags(ChannelFlag.None),
                                              tempChannelId, LightningMoney.Zero, peerId, LightningMoney.Zero,
                                              LightningMoney.Zero, peerId, fundingAmount, peerId, peerId,
                                              LightningMoney.Zero, 483, LightningMoney.Zero, peerId,
                                              LightningMoney.Zero, peerId, 144);
        var channelTypeTlv = new ChannelTypeTlv([]);
        return new OpenChannel1Message(payload, channelTypeTlv);
    }
}