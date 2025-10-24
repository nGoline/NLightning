using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NLightning.Infrastructure.Protocol.Models;
using NLightning.Tests.Utils.Mocks;

namespace NLightning.Application.Tests.Node.Managers;

using Application.Node.Managers;
using Domain.Channels.Interfaces;
using Domain.Crypto.ValueObjects;
using Domain.Exceptions;
using Domain.Node.Events;
using Domain.Node.Interfaces;
using Domain.Node.Models;
using Domain.Node.Options;
using Domain.Node.ValueObjects;
using Domain.Persistence.Interfaces;
using Domain.Protocol.Constants;
using Domain.Protocol.Interfaces;
using Infrastructure.Node.ValueObjects;
using Infrastructure.Transport.Events;
using Infrastructure.Transport.Interfaces;

// ReSharper disable AccessToDisposedClosure
public class PeerManagerTests
{
    private readonly CompactPubKey _compactPubKey =
        new PubKey("028d7500dd4c12685d1f568b4c2b5048e8534b873319f3a8daa612b469132ec7f7").ToBytes();

    private readonly Mock<IChannelManager> _mockChannelManager = new();
    private readonly Mock<ILogger<PeerManager>> _mockLogger = new();
    private readonly Mock<IPeerServiceFactory> _mockPeerServiceFactory = new();
    private readonly Mock<IPeerService> _mockPeerService = new();
    private readonly PeerModel _mockPeerModel;
    private readonly Mock<ITcpService> _mockTcpService = new();
    private readonly Mock<IChannelMessage> _mockChannelMessage = new();
    private readonly Mock<IChannelMessage> _mockResponseMessage = new();
    private readonly FakeServiceProvider _fakeServiceProvider = new();
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly Mock<IPeerDbRepository> _mockPeerDbRepository = new();

    private const string ExpectedHost = "127.0.0.1";
    private const int ExpectedPort = 9735;
    private const string ExpectedType = "IPv4";

    public PeerManagerTests()
    {
        // Set up the mock peer service
        _mockPeerService.SetupGet(p => p.PeerPubKey).Returns(_compactPubKey);
        _mockPeerService.SetupGet(p => p.Features).Returns(new FeatureOptions());

        // Set up the mock peer model
        _mockPeerModel = new PeerModel(_compactPubKey, ExpectedHost, ExpectedPort, ExpectedType)
        {
            LastSeenAt = DateTime.UtcNow
        };
        _mockPeerModel.SetPeerService(_mockPeerService.Object);

        // Set up the mock channel message
        _mockChannelMessage.SetupGet(m => m.Type).Returns(MessageTypes.OpenChannel);

        // Set up the peer service factory to return our mock peer service
        _mockPeerServiceFactory
           .Setup(f => f.CreateConnectedPeerAsync(It.IsAny<CompactPubKey>(), It.IsAny<TcpClient>()))
           .ReturnsAsync(_mockPeerService.Object);

        _mockPeerServiceFactory
           .Setup(f => f.CreateConnectingPeerAsync(It.IsAny<TcpClient>()))
           .ReturnsAsync(_mockPeerService.Object);

        // Set up the channel manager to return a response
        _mockChannelManager
           .Setup(cm => cm.HandleChannelMessageAsync(
                      It.IsAny<IChannelMessage>(),
                      It.IsAny<FeatureOptions>(),
                      It.IsAny<CompactPubKey>()))
           .ReturnsAsync(_mockResponseMessage.Object);

        // Set up unit of work and repositories
        _mockUnitOfWork.Setup(u => u.PeerDbRepository).Returns(_mockPeerDbRepository.Object);
        _mockUnitOfWork.Setup(u => u.GetPeersForStartupAsync()).ReturnsAsync(() =>
        {
            return new List<PeerModel>();
        });
        _fakeServiceProvider.AddService(typeof(IUnitOfWork), _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task Given_ValidPeerAddress_When_ConnectToPeerAsync_IsCalled_Then_PeerIsAdded()
    {
        // Given
        var peerManager = new PeerManager(_mockChannelManager.Object, _mockLogger.Object,
                                          _mockPeerServiceFactory.Object, _mockTcpService.Object, _fakeServiceProvider);

        var peerAddressInfo = new PeerAddressInfo($"{_compactPubKey}@127.0.0.1:9735");
        var peerAddress = new PeerAddress(peerAddressInfo);

        // Mock the TCP service to return a connected peer
        var mockTcpClient = new Mock<TcpClient>();
        var mockConnectedPeer = new ConnectedPeer(_compactPubKey, ExpectedHost, ExpectedPort, mockTcpClient.Object);
        _mockTcpService.Setup(t => t.ConnectToPeerAsync(peerAddress))
                       .ReturnsAsync(mockConnectedPeer);

        // Setup PeerDbRepository.AddOrUpdateAsync to match the pattern
        _mockPeerDbRepository.Setup(r => r.AddOrUpdateAsync(It.IsAny<PeerModel>()))
                             .Returns(Task.CompletedTask);

        // When
        await peerManager.ConnectToPeerAsync(peerAddressInfo);

        // Then
        var peers = GetPeersFromManager(peerManager);
        Assert.True(peers.ContainsKey(_compactPubKey));

        // Verify the TCP service was called
        _mockTcpService.Verify(t => t.ConnectToPeerAsync(peerAddress), Times.Once);

        // Verify peer service factory was called
        _mockPeerServiceFactory.Verify(f => f.CreateConnectedPeerAsync(_compactPubKey, mockTcpClient.Object),
                                       Times.Once);

        // Verify event handlers were set up
        _mockPeerService.VerifyAdd(p => p.OnDisconnect += It.IsAny<EventHandler<PeerDisconnectedEventArgs>>(),
                                   Times.Once);
        _mockPeerService.VerifyAdd(p => p.OnChannelMessageReceived += It.IsAny<EventHandler<ChannelMessageEventArgs>>(),
                                   Times.Once);

        // Verify repository methods were called
        _mockPeerDbRepository.Verify(r => r.AddOrUpdateAsync(It.IsAny<PeerModel>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Given_ConnectionError_When_ConnectToPeerAsync_IsCalled_Then_ExceptionIsThrown()
    {
        // Given
        var peerManager = new PeerManager(_mockChannelManager.Object, _mockLogger.Object,
                                          _mockPeerServiceFactory.Object, _mockTcpService.Object, _fakeServiceProvider);

        var peerAddressInfo = new PeerAddressInfo($"{_compactPubKey}@127.0.0.1:9735");
        var peerAddress = new PeerAddress(peerAddressInfo);
        var expectedError =
            new ConnectionException("Failed to connect to peer 127.0.0.1:9735");

        // Mock TCP service to throw a connection exception
        _mockTcpService.Setup(t => t.ConnectToPeerAsync(peerAddress))
                       .ThrowsAsync(expectedError);

        // When & Then
        var exception =
            await Assert.ThrowsAsync<ConnectionException>(() => peerManager.ConnectToPeerAsync(peerAddressInfo));
        Assert.Equal(expectedError.Message, exception.Message);

        // Verify no peer was added
        var peers = GetPeersFromManager(peerManager);
        Assert.Empty(peers);
    }

    [Fact]
    public async Task Given_StartAsync_When_Called_Then_TcpServiceStartsListening()
    {
        // Given
        var peerManager = new PeerManager(_mockChannelManager.Object, _mockLogger.Object,
                                          _mockPeerServiceFactory.Object, _mockTcpService.Object, _fakeServiceProvider);
        var cancellationToken = CancellationToken.None;

        // Setup for loading startup peers
        _mockUnitOfWork.Setup(u => u.GetPeersForStartupAsync()).ReturnsAsync(new List<PeerModel>());

        // When
        await peerManager.StartAsync(cancellationToken);

        // Then
        _mockTcpService.Verify(t => t.StartListeningAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockTcpService.VerifyAdd(t => t.OnNewPeerConnected += It.IsAny<EventHandler<NewPeerConnectedEventArgs>>(),
                                  Times.Once);
        _mockUnitOfWork.Verify(u => u.GetPeersForStartupAsync(), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Given_NewPeerConnected_When_EventRaised_Then_PeerIsAddedThroughFactory()
    {
        // Given
        var peerManager = new PeerManager(_mockChannelManager.Object, _mockLogger.Object,
                                          _mockPeerServiceFactory.Object, _mockTcpService.Object, _fakeServiceProvider);

        var mockTcpClient = new Mock<TcpClient>();
        var eventArgs = new NewPeerConnectedEventArgs(ExpectedHost, ExpectedPort, mockTcpClient.Object);

        // When
        await peerManager.StartAsync(CancellationToken.None);

        // Simulate the TCP service raising the event
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        // ReSharper disable once MethodHasAsyncOverload
        _mockTcpService.Raise(t => t.OnNewPeerConnected += null, null, eventArgs);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        // Wait a bit for the async continuation to complete
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Then
        _mockPeerServiceFactory.Verify(f => f.CreateConnectingPeerAsync(mockTcpClient.Object), Times.Once);

        // Verify peer was added (after the async operation completes)
        var peers = GetPeersFromManager(peerManager);
        Assert.Single(peers);
        Assert.True(peers.ContainsKey(_compactPubKey));
    }

    [Fact]
    public void Given_ExistingPeer_When_DisconnectPeer_IsCalled_Then_PeerIsDisconnected()
    {
        // Given
        var peerManager = new PeerManager(_mockChannelManager.Object, _mockLogger.Object,
                                          _mockPeerServiceFactory.Object, _mockTcpService.Object, _fakeServiceProvider);

        // Manually add peer to the internal dictionary
        var peers = GetPeersFromManager(peerManager);
        peers.Add(_compactPubKey, _mockPeerModel);

        // When
        peerManager.DisconnectPeer(_compactPubKey);

        // Then
        _mockPeerService.Verify(p => p.Disconnect(), Times.Once);
    }

    [Fact]
    public void Given_NonExistingPeer_When_DisconnectPeer_IsCalled_Then_LogWarning()
    {
        // Given
        var peerManager = new PeerManager(_mockChannelManager.Object, _mockLogger.Object,
                                          _mockPeerServiceFactory.Object, _mockTcpService.Object, _fakeServiceProvider);

        // When
        peerManager.DisconnectPeer(_compactPubKey);

        // Then
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Peer") && o.ToString()!.Contains("not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Given_PeerChannelMessage_When_EventRaised_Then_ChannelManagerIsInvoked()
    {
        // Given
        var peerManager = new PeerManager(_mockChannelManager.Object, _mockLogger.Object,
                                          _mockPeerServiceFactory.Object, _mockTcpService.Object, _fakeServiceProvider);

        // Add the peer to the manager
        var peers = GetPeersFromManager(peerManager);
        peers.Add(_compactPubKey, _mockPeerModel);

        var handlePeerChannelMessageMethod =
            peerManager.GetType().GetMethod("HandlePeerChannelMessage", BindingFlags.NonPublic | BindingFlags.Instance);
        // Create a delegate that matches the event signature
        var method = handlePeerChannelMessageMethod;
        _ = (EventHandler<ChannelMessageEventArgs>)((sender, args) => method!.Invoke(peerManager, [sender!, args]));

        // Subscribe to the event
        _mockPeerService.SetupAdd(p => p.OnChannelMessageReceived += It.IsAny<EventHandler<ChannelMessageEventArgs>>())
                        .Callback<EventHandler<ChannelMessageEventArgs>>(h => _ = h);

        // Trigger the subscription by calling the internal method that would normally be called during peer creation
        handlePeerChannelMessageMethod =
            peerManager.GetType().GetMethod("HandlePeerChannelMessage", BindingFlags.NonPublic | BindingFlags.Instance);

        var eventArgs = new ChannelMessageEventArgs(_mockChannelMessage.Object, _compactPubKey);

        // When
        handlePeerChannelMessageMethod!.Invoke(peerManager, [null!, eventArgs]);

        // Then
        _mockChannelManager.Verify(cm => cm.HandleChannelMessageAsync(
                                       _mockChannelMessage.Object,
                                       It.IsAny<FeatureOptions>(),
                                       _compactPubKey), Times.Once);
    }

    [Fact]
    public async Task Given_ChannelMessageWithResponse_When_Processed_Then_ResponseIsSentToPeer()
    {
        // Given
        var peerManager = new PeerManager(_mockChannelManager.Object, _mockLogger.Object,
                                          _mockPeerServiceFactory.Object, _mockTcpService.Object, _fakeServiceProvider);

        // Add the peer to the manager
        var peers = GetPeersFromManager(peerManager);
        peers.Add(_compactPubKey, _mockPeerModel);

        var eventArgs = new ChannelMessageEventArgs(_mockChannelMessage.Object, _compactPubKey);

        // When
        var handlePeerChannelMessageMethod =
            peerManager.GetType().GetMethod("HandlePeerChannelMessage", BindingFlags.NonPublic | BindingFlags.Instance);
        handlePeerChannelMessageMethod!.Invoke(peerManager, [null!, eventArgs]);

        // Wait a bit for the async continuation to complete
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Then
        _mockPeerService.Verify(p => p.SendMessageAsync(_mockResponseMessage.Object), Times.Once);
    }

    [Fact]
    public async Task Given_ChannelErrorException_When_ProcessingChannelMessage_Then_PeerIsDisconnected()
    {
        // Given
        var peerManager = new PeerManager(_mockChannelManager.Object, _mockLogger.Object,
                                          _mockPeerServiceFactory.Object, _mockTcpService.Object, _fakeServiceProvider);

        // Add the peer to the manager
        var peers = GetPeersFromManager(peerManager);
        peers.Add(_compactPubKey, _mockPeerModel);

        var channelError = new ChannelErrorException("Test channel error", "Peer error message");
        _mockChannelManager
           .Setup(cm => cm.HandleChannelMessageAsync(It.IsAny<IChannelMessage>(),
                                                     It.IsAny<FeatureOptions>(),
                                                     It.IsAny<CompactPubKey>()))
           .ThrowsAsync(channelError);

        var eventArgs = new ChannelMessageEventArgs(_mockChannelMessage.Object, _compactPubKey);

        // When
        var handlePeerChannelMessageMethod = peerManager.GetType()
                                                        .GetMethod("HandlePeerChannelMessage",
                                                                   BindingFlags.NonPublic | BindingFlags.Instance);
        handlePeerChannelMessageMethod!.Invoke(peerManager, [null!, eventArgs]);

        // Wait a bit for the async continuation to complete
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Then
        _mockPeerService.Verify(p => p.Disconnect(), Times.Once);
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error handling channel message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task
        Given_ChannelWarningException_When_ProcessingChannelMessage_Then_WarningIsLoggedButPeerStaysConnected()
    {
        // Given
        var peerManager = new PeerManager(_mockChannelManager.Object, _mockLogger.Object,
                                          _mockPeerServiceFactory.Object, _mockTcpService.Object, _fakeServiceProvider);

        // Add the peer to the manager
        var peers = GetPeersFromManager(peerManager);
        peers.Add(_compactPubKey, _mockPeerModel);

        var channelWarning = new ChannelWarningException("Test channel warning", "Peer warning message");
        _mockChannelManager
           .Setup(cm => cm.HandleChannelMessageAsync(It.IsAny<IChannelMessage>(),
                                                     It.IsAny<FeatureOptions>(),
                                                     It.IsAny<CompactPubKey>()))
           .ThrowsAsync(channelWarning);

        var eventArgs = new ChannelMessageEventArgs(_mockChannelMessage.Object, _compactPubKey);

        // When
        var handlePeerChannelMessageMethod =
            peerManager.GetType().GetMethod("HandlePeerChannelMessage", BindingFlags.NonPublic | BindingFlags.Instance);
        handlePeerChannelMessageMethod!.Invoke(peerManager, [null!, eventArgs]);

        // Wait a bit for the async continuation to complete
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Then
        _mockPeerService.Verify(p => p.Disconnect(), Times.Never);
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error handling channel message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Given_PeerDisconnection_When_EventRaised_Then_PeerIsRemovedFromManager()
    {
        // Given
        var peerManager = new PeerManager(_mockChannelManager.Object, _mockLogger.Object,
                                          _mockPeerServiceFactory.Object, _mockTcpService.Object, _fakeServiceProvider);

        // Add the peer to the manager
        var peers = GetPeersFromManager(peerManager);
        peers.Add(_compactPubKey, _mockPeerModel);

        // Get the handler method
        var handlePeerDisconnectionMethod =
            peerManager.GetType().GetMethod("HandlePeerDisconnection", BindingFlags.NonPublic | BindingFlags.Instance);

        var eventArgs = new PeerDisconnectedEventArgs(_compactPubKey);

        // When - directly invoke the handler method
        handlePeerDisconnectionMethod!.Invoke(peerManager, [null!, eventArgs]);

        // Then
        Assert.False(peers.ContainsKey(_compactPubKey));
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Peer") && o.ToString()!.Contains("disconnected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Given_StopAsync_When_Called_Then_AllPeersAreDisconnectedAndServiceIsStopped()
    {
        // Given
        var peerManager = new PeerManager(_mockChannelManager.Object, _mockLogger.Object,
                                          _mockPeerServiceFactory.Object, _mockTcpService.Object, _fakeServiceProvider);
        await peerManager.StartAsync(CancellationToken.None);
        var peers = GetPeersFromManager(peerManager);
        peers.Add(_compactPubKey, _mockPeerModel);
        var taskCompletionSource = new TaskCompletionSource();
        _mockPeerService.Setup(x => x.Disconnect()).Callback(taskCompletionSource.SetResult);

        // When
        _ = peerManager.StopAsync();
        await taskCompletionSource.Task;

        // Then
        _mockPeerService.Verify(p => p.Disconnect(), Times.Once);
    }

    [Fact]
    public async Task Given_PeerServiceFactoryThrowsException_When_CreatingConnectingPeer_Then_ExceptionIsLogged()
    {
        // Given
        var peerManager = new PeerManager(_mockChannelManager.Object, _mockLogger.Object,
                                          _mockPeerServiceFactory.Object, _mockTcpService.Object, _fakeServiceProvider);

        var mockTcpClient = new Mock<TcpClient>();
        var eventArgs = new NewPeerConnectedEventArgs(ExpectedHost, ExpectedPort, mockTcpClient.Object);

        _mockPeerServiceFactory
           .Setup(f => f.CreateConnectingPeerAsync(It.IsAny<TcpClient>()))
           .ThrowsAsync(new InvalidOperationException("Test factory error"));

        // When
        await peerManager.StartAsync(CancellationToken.None);
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        // ReSharper disable once MethodHasAsyncOverload
        _mockTcpService.Raise(t => t.OnNewPeerConnected += null, eventArgs);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        // Wait for the async continuation to complete
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Then
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error handling new peer connection")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Helper method to access the private _peers field for testing
    /// </summary>
    private Dictionary<CompactPubKey, PeerModel> GetPeersFromManager(PeerManager peerManager)
    {
        var field = peerManager.GetType().GetField("_peers", BindingFlags.NonPublic | BindingFlags.Instance);
        return (Dictionary<CompactPubKey, PeerModel>)field!.GetValue(peerManager)!;
    }
}
// ReSharper restore AccessToDisposedClosure