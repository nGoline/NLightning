using NLightning.Domain.Channels.ValueObjects;

namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class TxRemoveInputMessageTests
{
    private readonly TxRemoveInputMessageTypeSerializer _txRemoveInputMessageTypeSerializer;

    public TxRemoveInputMessageTests()
    {
        _txRemoveInputMessageTypeSerializer =
            new TxRemoveInputMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsTxRemoveInputMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        const ulong expectedSerialId = 1;
        var stream =
            new MemoryStream(
                Convert.FromHexString(
                    "00000000000000000000000000000000000000000000000000000000000000000000000000000001"));

        // Act
        var message = await _txRemoveInputMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(expectedSerialId, message.Payload.SerialId);
    }

    [Fact]
    public async Task Given_GivenValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        ulong serialId = 1;
        var message = new TxRemoveInputMessage(new TxRemoveInputPayload(channelId, serialId));
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString("00000000000000000000000000000000000000000000000000000000000000000000000000000001");

        // Act
        await _txRemoveInputMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }
}