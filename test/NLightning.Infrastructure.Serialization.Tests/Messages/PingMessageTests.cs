namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class PingMessageTests
{
    private readonly PingMessageTypeSerializer _pingMessageTypeSerializer;

    public PingMessageTests()
    {
        _pingMessageTypeSerializer =
            new PingMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsPingMessageWithCorrectPayload()
    {
        // Arrange
        var expectedPayload = new PingPayload
        {
            BytesLength = 1,
            Ignored = new byte[2],
            NumPongBytes = 3
        };
        var stream = new MemoryStream(Convert.FromHexString("000300010000"));

        // Act
        var pingMessage = await _pingMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(pingMessage);
        Assert.Equal(expectedPayload.BytesLength, pingMessage.Payload.BytesLength);
        Assert.Equal(expectedPayload.NumPongBytes, pingMessage.Payload.NumPongBytes);
    }

    [Fact]
    public async Task Given_ValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var message = new PingMessage(new PingPayload
        {
            BytesLength = 1,
            Ignored = new byte[2],
            NumPongBytes = 3
        });
        var stream = new MemoryStream();
        var expectedBytes = Convert.FromHexString("000300010000");

        // Act
        await _pingMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }
}