using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NLightning.Domain.Serialization.Interfaces;
using NLightning.Tests.Utils;
using NLightning.Tests.Utils.Mocks;

namespace NLightning.Infrastructure.Tests.Transport.Services;

using Domain.Transport;
using Exceptions;
using Infrastructure.Transport.Services;

// ReSharper disable AccessToDisposedClosure
public class TransportServiceTests
{
    private readonly Mock<ILogger> _mockLogger = new();

    [Fact]
    public async Task
        Given_TransportServiceAsInitiator_When_InitializeIsCalled_Then_HandshakeServicePerformStepIsCalledTwice()
    {
        // Given
        var handshakeServiceMock = new Mock<FakeHandshakeService>();
        handshakeServiceMock.Object.SetIsInitiator(true);
        var messageSerializerMock = new Mock<IMessageSerializer>();
        var availablePort = await PortPoolUtil.GetAvailablePortAsync();
        var tcpListener = new TcpListener(IPAddress.Loopback, availablePort);
        tcpListener.Start();

        try
        {
            var steps = 2;
            handshakeServiceMock
               .Setup(x => x.PerformStep(It.IsAny<byte[]>(), out It.Ref<byte[]>.IsAny))
               .Returns((byte[] inMessage, out byte[] outMessage) =>
                {
                    ITransport? transport = null;
                    switch (steps)
                    {
                        case 2:
                            {
                                steps--;
                                if (inMessage.Length != 50 && inMessage.Length != 0)
                                {
                                    throw new InvalidOperationException("Expected 50 bytes");
                                }

                                outMessage = new byte[50];
                                return (50, transport);
                            }
                        case 1:
                            {
                                steps--;
                                if (inMessage.Length != 50 && inMessage.Length != 66 && inMessage.Length != 0)
                                {
                                    throw new InvalidOperationException("Expected 66 bytes");
                                }

                                outMessage = new byte[66];

                                return (66, new FakeTransport());
                            }
                        default:
                            throw new InvalidOperationException("There's no more steps to complete");
                    }
                });
            var tcpClient1 = new TcpClient();
            var acceptTask = Task.Run(async () =>
            {
                var tcpClient2 = await tcpListener.AcceptTcpClientAsync();
                var stream = tcpClient2.GetStream();
                var buffer = new byte[50];
                await stream.ReadExactlyAsync(buffer);

                await stream.WriteAsync(buffer);

                buffer = new byte[66];
                await stream.ReadExactlyAsync(buffer);
            }, TestContext.Current.CancellationToken);
            await tcpClient1.ConnectAsync(IPEndPoint.Parse(tcpListener.LocalEndpoint.ToEndpointString()),
                                          TestContext.Current.CancellationToken);
            var transportService = new TransportService(_mockLogger.Object, messageSerializerMock.Object,
                                                        TimeSpan.FromSeconds(30), handshakeServiceMock.Object,
                                                        tcpClient1);

            // When
            await transportService.InitializeAsync();
            await acceptTask;

            // Then
            handshakeServiceMock.Verify(x => x.PerformStep(It.IsAny<byte[]>(), out It.Ref<byte[]>.IsAny),
                                        Times.Exactly(2));
        }
        finally
        {
            tcpListener.Dispose();
            PortPoolUtil.ReleasePort(availablePort);
        }
    }

    [Fact]
    public async Task
        Given_TransportServiceAsInitiator_When_InitializeIsCalledAndTcpClinetIsDisconnected_Then_ThrowsInvalidOperationException()
    {
        // Arrange
        var handshakeServiceMock = new Mock<FakeHandshakeService>();
        var messageSerializerMock = new Mock<IMessageSerializer>();
        var tcpClient1 = new TcpClient();
        var transportService = new TransportService(_mockLogger.Object, messageSerializerMock.Object,
                                                    TimeSpan.FromSeconds(30), handshakeServiceMock.Object, tcpClient1);

        // Act
        var exception = await Assert
                           .ThrowsAnyAsync<InvalidOperationException>(() => transportService.InitializeAsync());

        // Assert
        Assert.Equal("TcpClient is not connected", exception.Message);
    }

    [Fact]
    public async Task Given_TransportService_When_TimeoutOccurs_Then_ThrowsConnectionTimeoutException()
    {
        // Arrange
        var handshakeServiceMock = new Mock<FakeHandshakeService>();
        handshakeServiceMock.Object.SetIsInitiator(true);
        var messageSerializerMock = new Mock<IMessageSerializer>();
        var availablePort = await PortPoolUtil.GetAvailablePortAsync();
        var tcpListener = new TcpListener(IPAddress.Loopback, availablePort);
        tcpListener.Start();

        try
        {
            var steps = 2;
            handshakeServiceMock
               .Setup(x => x.PerformStep(It.IsAny<byte[]>(), out It.Ref<byte[]>.IsAny))
               .Returns((byte[] inMessage, out byte[] outMessage) =>
                {
                    if (steps != 2)
                    {
                        throw new InvalidOperationException("There's no more steps to complete");
                    }

                    steps--;
                    if (inMessage.Length != 50 && inMessage.Length != 0)
                    {
                        throw new InvalidOperationException("Expected 50 bytes");
                    }

                    outMessage = new byte[50];
                    ITransport? transport = null;
                    return (50, transport);
                });
            var tcpClient1 = new TcpClient();
            var acceptTask = Task.Run(async () =>
            {
                var tcpClient2 = await tcpListener.AcceptTcpClientAsync();
                var stream = tcpClient2.GetStream();
                var buffer = new byte[50];
                await stream.ReadExactlyAsync(buffer);

                await stream.WriteAsync(buffer);
            }, TestContext.Current.CancellationToken);
            await tcpClient1.ConnectAsync(IPEndPoint.Parse(tcpListener.LocalEndpoint.ToEndpointString()),
                                          TestContext.Current.CancellationToken);
            var transportService = new TransportService(_mockLogger.Object, messageSerializerMock.Object, TimeSpan.Zero,
                                                        handshakeServiceMock.Object, tcpClient1);

            // Act
            var exception = await Assert
                               .ThrowsAnyAsync<ConnectionTimeoutException>(() => transportService.InitializeAsync());
            await acceptTask;

            // Assert
            Assert.Contains("Timeout while reading Handshake's Act 2 from host", exception.Message);
        }
        finally
        {
            tcpListener.Dispose();
            PortPoolUtil.ReleasePort(availablePort);
        }
    }
}
// ReSharper restore AccessToDisposedClosure