using Microsoft.Extensions.Logging;

namespace NLightning.Infrastructure.Tests.Protocol.Services;

using Domain.Exceptions;
using Domain.Protocol.Interfaces;
using Domain.Serialization.Interfaces;
using Domain.Transport;
using Infrastructure.Protocol.Services;

public class MessageServiceTests
{
    private readonly Mock<IMessageSerializer> _messageSerializerMock;

    public MessageServiceTests()
    {
        _messageSerializerMock = new Mock<IMessageSerializer>();
    }

    [Fact]
    public async Task Given_Message_When_SendMessageAsync_IsCalled_Then_TransportServiceWritesMessage()
    {
        // Given
        var loggerMock = new Mock<ILogger<MessageService>>();
        var transportServiceMock = new Mock<ITransportService>();
        var messageService =
            new MessageService(loggerMock.Object, _messageSerializerMock.Object, transportServiceMock.Object);
        var messageMock = new Mock<IMessage>();

        // When
        await messageService.SendMessageAsync(messageMock.Object,
                                              cancellationToken: TestContext.Current.CancellationToken);

        // Then
        transportServiceMock.Verify(t => t.WriteMessageAsync(messageMock.Object, It.IsAny<CancellationToken>()),
                                    Times.Once());
    }

    [Fact]
    public void Given_ReceivedMessage_When_ReceiveMessageAsync_IsInvoked_Then_MessageReceivedEventIsRaised()
    {
        // Given
        var loggerMock = new Mock<ILogger<MessageService>>();
        var transportServiceMock = new Mock<ITransportService>();
        var messageMock = new Mock<IMessage>();
        _messageSerializerMock.Setup(m => m.DeserializeMessageAsync(It.IsAny<Stream>()))
                              .ReturnsAsync(messageMock.Object);

        var messageService =
            new MessageService(loggerMock.Object, _messageSerializerMock.Object, transportServiceMock.Object);
        var stream = new MemoryStream();

        // When & Then
        var receivedMessage = Assert.RaisesAny<IMessage?>(
            h => messageService.OnMessageReceived += h,
            h => messageService.OnMessageReceived -= h,
            () =>
            {
                // Simulate transport service receiving a message
                transportServiceMock.Raise(t => t.MessageReceived += null, messageService, stream);
            });

        Assert.NotNull(receivedMessage.Arguments);
        Assert.Same(messageMock.Object, receivedMessage.Arguments);
    }

    [Fact]
    public void Given_TransportServiceConnectionState_When_CheckingIsConnected_Then_ReturnsCorrectValue()
    {
        // Given
        var loggerMock = new Mock<ILogger<MessageService>>();
        var transportServiceMock = new Mock<ITransportService>();
        transportServiceMock.Setup(t => t.IsConnected).Returns(true);
        var messageService =
            new MessageService(loggerMock.Object, _messageSerializerMock.Object, transportServiceMock.Object);

        // When & Then
        Assert.True(messageService.IsConnected);
    }

    [Fact]
    public void Given_MessageService_When_Dispose_IsCalled_Then_TransportServiceIsDisposed()
    {
        // Given
        var loggerMock = new Mock<ILogger<MessageService>>();
        var transportServiceMock = new Mock<ITransportService>();
        var messageService =
            new MessageService(loggerMock.Object, _messageSerializerMock.Object, transportServiceMock.Object);

        // When
        messageService.Dispose();

        // Then
        transportServiceMock.Verify(t => t.Dispose(), Times.Once());
    }

    [Fact]
    public async Task Given_DisposedMessageService_When_SendMessageAsync_IsCalled_Then_ThrowsConnectionException()
    {
        // Given
        var loggerMock = new Mock<ILogger<MessageService>>();
        var transportServiceMock = new Mock<ITransportService>();
        var messageService =
            new MessageService(loggerMock.Object, _messageSerializerMock.Object, transportServiceMock.Object);
        var messageMock = new Mock<IMessage>();

        // When
        messageService.Dispose();

        // Then
        var exception = await Assert.ThrowsAsync<ConnectionException>(() => messageService.SendMessageAsync(
                                                                          messageMock.Object, true,
                                                                          TestContext.Current.CancellationToken));
        Assert.IsType<ObjectDisposedException>(exception.InnerException);
    }
}