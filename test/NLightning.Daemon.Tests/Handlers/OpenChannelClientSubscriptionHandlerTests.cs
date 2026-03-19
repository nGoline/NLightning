using Microsoft.Extensions.Logging;

namespace NLightning.Daemon.Tests.Handlers;

using Daemon.Handlers;
using Domain.Bitcoin.Interfaces;
using Domain.Bitcoin.ValueObjects;
using Domain.Bitcoin.Wallet.Models;
using Domain.Channels.Enums;
using Domain.Channels.Events;
using Domain.Channels.Interfaces;
using Domain.Channels.Models;
using Domain.Channels.ValueObjects;
using Domain.Client.Constants;
using Domain.Client.Exceptions;
using Domain.Client.Requests;
using Domain.Crypto.ValueObjects;
using Domain.Exceptions;
using Domain.Money;
using Domain.Node.Events;
using Domain.Node.Interfaces;
using Domain.Node.Models;
using Domain.Node.Options;

public class OpenChannelClientSubscriptionHandlerTests
{
    private readonly Mock<IChannelMemoryRepository> _channelMemoryRepositoryMock;
    private readonly Mock<IPeerManager> _peerManagerMock;
    private readonly Mock<IUtxoMemoryRepository> _utxoMemoryRepositoryMock;
    private readonly OpenChannelClientSubscriptionHandler _handler;

    public OpenChannelClientSubscriptionHandlerTests()
    {
        _channelMemoryRepositoryMock = new Mock<IChannelMemoryRepository>();
        _peerManagerMock = new Mock<IPeerManager>();
        _utxoMemoryRepositoryMock = new Mock<IUtxoMemoryRepository>();
        var loggerMock = new Mock<ILogger<OpenChannelClientSubscriptionHandler>>();

        _handler = new OpenChannelClientSubscriptionHandler(
            _channelMemoryRepositoryMock.Object,
            loggerMock.Object,
            _peerManagerMock.Object,
            _utxoMemoryRepositoryMock.Object
        );
    }

    [Fact]
    public async Task GivenChannelDoesNotExist_WhenHandleAsync_ThenThrowsClientException()
    {
        // Arrange
        var channelId = CreateRandomChannelId();
        var request = new OpenChannelClientSubscriptionRequest(channelId);
        ChannelModel? channel = null;
        _channelMemoryRepositoryMock.Setup(x => x.TryGetChannel(channelId, out channel)).Returns(false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ClientException>(() => _handler.HandleAsync(request, CancellationToken.None));
        Assert.Equal(ErrorCodes.InvalidChannel, ex.ErrorCode);
    }

    [Fact]
    public async Task GivenPeerNotFound_WhenHandleAsync_ThenThrowsClientException()
    {
        // Arrange
        var channelId = CreateRandomChannelId();
        var request = new OpenChannelClientSubscriptionRequest(channelId);
        var peerId = CreateDummyPubKey();
        var channel = CreateDummyChannel(channelId, peerId);
        _channelMemoryRepositoryMock.Setup(x => x.TryGetChannel(channelId, out channel)).Returns(true);
        _peerManagerMock.Setup(x => x.GetPeer(peerId)).Returns((PeerModel?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ClientException>(() => _handler.HandleAsync(request, CancellationToken.None));
        Assert.Equal(ErrorCodes.InvalidOperation, ex.ErrorCode);
    }

    [Fact]
    public async Task GivenNoLockedUtxos_WhenHandleAsync_ThenThrowsClientException()
    {
        // Arrange
        var channelId = CreateRandomChannelId();
        var request = new OpenChannelClientSubscriptionRequest(channelId);
        var peerId = CreateDummyPubKey();
        var channel = CreateDummyChannel(channelId, peerId);
        var peer = new PeerModel(peerId, "localhost", 9735, "ipv4");

        _channelMemoryRepositoryMock.Setup(x => x.TryGetChannel(channelId, out channel)).Returns(true);
        _peerManagerMock.Setup(x => x.GetPeer(peerId)).Returns(peer);
        _utxoMemoryRepositoryMock.Setup(x => x.GetLockedUtxosForChannel(channelId)).Returns(new List<UtxoModel>());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ClientException>(() => _handler.HandleAsync(request, CancellationToken.None));
        Assert.Equal(ErrorCodes.InvalidOperation, ex.ErrorCode);
    }

    [Fact]
    public async Task GivenValidRequest_WhenChannelUpdatedToFundingSigned_ThenReturnsResponse()
    {
        // Arrange
        var channelId = CreateRandomChannelId();
        var request = new OpenChannelClientSubscriptionRequest(channelId);
        var peerId = CreateDummyPubKey();
        var channel = CreateDummyChannel(channelId, peerId);
        var peer = new PeerModel(peerId, "localhost", 9735, "ipv4");
        var peerServiceMock = new Mock<IPeerService>();
        peerServiceMock.Setup(x => x.Features).Returns(new FeatureOptions());
        peer.SetPeerService(peerServiceMock.Object);

        _channelMemoryRepositoryMock.Setup(x => x.TryGetChannel(channelId, out channel)).Returns(true);
        _peerManagerMock.Setup(x => x.GetPeer(peerId)).Returns(peer);
        _utxoMemoryRepositoryMock.Setup(x => x.GetLockedUtxosForChannel(channelId))
                                 .Returns([CreateDummyUtxo()]);

        // Act
        var handleTask = _handler.HandleAsync(request, CancellationToken.None);

        // Trigger Event
        channel.UpdateState(ChannelState.V1FundingSigned);
        _channelMemoryRepositoryMock.Raise(x => x.OnChannelUpdated += null, this, new ChannelUpdatedEventArgs(channel));

        var response = await handleTask;

        // Assert
        Assert.Equal(channelId, response.ChannelId);
        Assert.Equal(ChannelState.V1FundingSigned, response.ChannelState);
    }

    [Fact]
    public async Task GivenValidRequest_WhenChannelUpdatedToReadyForUs_ThenReturnsResponse()
    {
        // Arrange
        var channelId = CreateRandomChannelId();
        var request = new OpenChannelClientSubscriptionRequest(channelId);
        var peerId = CreateDummyPubKey();
        var channel = CreateDummyChannel(channelId, peerId);
        var peer = new PeerModel(peerId, "localhost", 9735, "ipv4");
        var peerServiceMock = new Mock<IPeerService>();
        peerServiceMock.Setup(x => x.Features).Returns(new FeatureOptions());
        peer.SetPeerService(peerServiceMock.Object);

        _channelMemoryRepositoryMock.Setup(x => x.TryGetChannel(channelId, out channel)).Returns(true);
        _peerManagerMock.Setup(x => x.GetPeer(peerId)).Returns(peer);
        _utxoMemoryRepositoryMock.Setup(x => x.GetLockedUtxosForChannel(channelId))
                                 .Returns([CreateDummyUtxo()]);

        // Act
        var handleTask = _handler.HandleAsync(request, CancellationToken.None);

        // Trigger Event
        channel.UpdateState(ChannelState.ReadyForUs);
        _channelMemoryRepositoryMock.Raise(x => x.OnChannelUpdated += null, this, new ChannelUpdatedEventArgs(channel));

        var response = await handleTask;

        // Assert
        Assert.Equal(channelId, response.ChannelId);
        Assert.Equal(ChannelState.ReadyForUs, response.ChannelState);
    }

    [Fact]
    public async Task GivenValidRequest_WhenChannelUpdatedToReadyForThem_ThenReturnsResponse()
    {
        // Arrange
        var channelId = CreateRandomChannelId();
        var request = new OpenChannelClientSubscriptionRequest(channelId);
        var peerId = CreateDummyPubKey();
        var channel = CreateDummyChannel(channelId, peerId);
        var peer = new PeerModel(peerId, "localhost", 9735, "ipv4");
        var peerServiceMock = new Mock<IPeerService>();
        peerServiceMock.Setup(x => x.Features).Returns(new FeatureOptions());
        peer.SetPeerService(peerServiceMock.Object);

        _channelMemoryRepositoryMock.Setup(x => x.TryGetChannel(channelId, out channel)).Returns(true);
        _peerManagerMock.Setup(x => x.GetPeer(peerId)).Returns(peer);
        _utxoMemoryRepositoryMock.Setup(x => x.GetLockedUtxosForChannel(channelId))
                                 .Returns([CreateDummyUtxo()]);

        // Act
        var handleTask = _handler.HandleAsync(request, CancellationToken.None);

        // Trigger Event
        channel.UpdateState(ChannelState.ReadyForThem);
        _channelMemoryRepositoryMock.Raise(x => x.OnChannelUpdated += null, this, new ChannelUpdatedEventArgs(channel));

        var response = await handleTask;

        // Assert
        Assert.Equal(channelId, response.ChannelId);
        Assert.Equal(ChannelState.ReadyForUs,
                     response.ChannelState); // Note: Handler sets response.ChannelState to ReadyForUs in both cases
    }

    [Fact]
    public async Task GivenValidRequest_WhenAttentionMessageReceived_ThenThrowsChannelErrorException()
    {
        // Arrange
        var channelId = CreateRandomChannelId();
        var request = new OpenChannelClientSubscriptionRequest(channelId);
        var peerId = CreateDummyPubKey();
        var channel = CreateDummyChannel(channelId, peerId);
        var peer = new PeerModel(peerId, "localhost", 9735, "ipv4");
        var peerServiceMock = new Mock<IPeerService>();
        peerServiceMock.Setup(x => x.Features).Returns(new FeatureOptions());
        peer.SetPeerService(peerServiceMock.Object);

        _channelMemoryRepositoryMock.Setup(x => x.TryGetChannel(channelId, out channel)).Returns(true);
        _peerManagerMock.Setup(x => x.GetPeer(peerId)).Returns(peer);
        _utxoMemoryRepositoryMock.Setup(x => x.GetLockedUtxosForChannel(channelId))
                                 .Returns([CreateDummyUtxo()]);

        // Act
        var handleTask = _handler.HandleAsync(request, CancellationToken.None);

        // Trigger Event
        peerServiceMock.Raise(x => x.OnAttentionMessageReceived += null, this,
                              new AttentionMessageEventArgs("Test error", peerId, channelId));

        // Assert
        await Assert.ThrowsAsync<ChannelErrorException>(() => handleTask);
    }

    [Fact]
    public async Task GivenValidRequest_WhenPeerDisconnected_ThenThrowsConnectionException()
    {
        // Arrange
        var channelId = CreateRandomChannelId();
        var request = new OpenChannelClientSubscriptionRequest(channelId);
        var peerId = CreateDummyPubKey();
        var channel = CreateDummyChannel(channelId, peerId);
        var peer = new PeerModel(peerId, "localhost", 9735, "ipv4");
        var peerServiceMock = new Mock<IPeerService>();
        peerServiceMock.Setup(x => x.Features).Returns(new FeatureOptions());
        peer.SetPeerService(peerServiceMock.Object);

        _channelMemoryRepositoryMock.Setup(x => x.TryGetChannel(channelId, out channel)).Returns(true);
        _peerManagerMock.Setup(x => x.GetPeer(peerId)).Returns(peer);
        _utxoMemoryRepositoryMock.Setup(x => x.GetLockedUtxosForChannel(channelId))
                                 .Returns([CreateDummyUtxo()]);

        // Act
        var handleTask = _handler.HandleAsync(request, CancellationToken.None);

        // Trigger Event
        peerServiceMock.Raise(x => x.OnDisconnect += null, this, new PeerDisconnectedEventArgs(peerId));

        // Assert
        await Assert.ThrowsAsync<ConnectionException>(() => handleTask);
    }

    [Fact]
    public async Task GivenValidRequest_WhenExceptionRaised_ThenThrowsException()
    {
        // Arrange
        var channelId = CreateRandomChannelId();
        var request = new OpenChannelClientSubscriptionRequest(channelId);
        var peerId = CreateDummyPubKey();
        var channel = CreateDummyChannel(channelId, peerId);
        var peer = new PeerModel(peerId, "localhost", 9735, "ipv4");
        var peerServiceMock = new Mock<IPeerService>();
        peerServiceMock.Setup(x => x.Features).Returns(new FeatureOptions());
        peer.SetPeerService(peerServiceMock.Object);

        _channelMemoryRepositoryMock.Setup(x => x.TryGetChannel(channelId, out channel)).Returns(true);
        _peerManagerMock.Setup(x => x.GetPeer(peerId)).Returns(peer);
        _utxoMemoryRepositoryMock.Setup(x => x.GetLockedUtxosForChannel(channelId))
                                 .Returns([CreateDummyUtxo()]);

        // Act
        var handleTask = _handler.HandleAsync(request, CancellationToken.None);

        // Trigger Event
        peerServiceMock.Raise(x => x.OnExceptionRaised += null, this,
                              new ChannelErrorException("Test exception", channelId));

        // Assert
        await Assert.ThrowsAsync<ChannelErrorException>(() => handleTask);
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
        for (var i = 1; i < 33; i++) bytes[i] = (byte)i;
        return new CompactPubKey(bytes);
    }

    private static UtxoModel CreateDummyUtxo()
    {
        return new UtxoModel(new TxId(new byte[32]), 0, LightningMoney.Satoshis(1000000), 100, 0, false,
                             Domain.Bitcoin.Enums.AddressType.P2Wpkh);
    }

    private static ChannelModel CreateDummyChannel(ChannelId channelId, CompactPubKey peerId)
    {
        return new ChannelModel(new ChannelConfig(), channelId, null, null, true, null, null,
                                LightningMoney.Satoshis(100000),
                                new ChannelKeySetModel(0, peerId, peerId, peerId, peerId, peerId, peerId), 0, 0,
                                LightningMoney.Zero, null, 0, peerId, 0, ChannelState.V1Opening, ChannelVersion.V1);
    }
}