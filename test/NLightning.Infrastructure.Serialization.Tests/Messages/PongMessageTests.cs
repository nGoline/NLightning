namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class PongMessageTests
{
    private readonly PongMessageTypeSerializer _pongMessageTypeSerializer;

    public PongMessageTests()
    {
        _pongMessageTypeSerializer =
            new PongMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsPongMessageWithCorrectPayload()
    {
        // Arrange
        var expectedPayload = new PongPayload(10);
        var stream = new MemoryStream(Convert.FromHexString("000A00000000000000000000"));

        // Act
        var pingMessage = await _pongMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(pingMessage);
        Assert.Equal(expectedPayload.BytesLength, pingMessage.Payload.BytesLength);
    }

    [Fact]
    public async Task Given_ValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var payload = new PongPayload(10)
        {
            Ignored = new byte[10]
        };
        var message = new PongMessage(payload);
        var stream = new MemoryStream();
        var expectedBytes = Convert.FromHexString("000A00000000000000000000");

        // Act
        await _pongMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }
}