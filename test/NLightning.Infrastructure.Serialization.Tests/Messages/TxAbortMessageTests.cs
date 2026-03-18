using NLightning.Domain.Channels.ValueObjects;

namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class TxAbortMessageTests
{
    private readonly TxAbortMessageTypeSerializer _txAbortMessageTypeSerializer;

    public TxAbortMessageTests()
    {
        _txAbortMessageTypeSerializer =
            new TxAbortMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsTxAbortMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var expectedData = "Some error"u8.ToArray();
        var stream =
            new MemoryStream(Convert.FromHexString(
                                 "0000000000000000000000000000000000000000000000000000000000000000000A536F6D65206572726F72"));

        // Act
        var message = await _txAbortMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(expectedData, message.Payload.Data);
    }

    [Fact]
    public async Task Given_GivenValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        var data = "Some error"u8.ToArray();
        var message = new TxAbortMessage(new TxAbortPayload(channelId, data));
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString(
                "0000000000000000000000000000000000000000000000000000000000000000000A536F6D65206572726F72");

        // Act
        await _txAbortMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }
}