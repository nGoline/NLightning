namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Channels.ValueObjects;
using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class TxRemoveOutputMessageTests
{
    private readonly TxRemoveOutputMessageTypeSerializer _txRemoveOutputMessageTypeSerializer;

    public TxRemoveOutputMessageTests()
    {
        _txRemoveOutputMessageTypeSerializer =
            new TxRemoveOutputMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsTxRemoveOutputMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        const ulong expectedSerialId = 1;
        var stream =
            new MemoryStream(
                Convert.FromHexString(
                    "00000000000000000000000000000000000000000000000000000000000000000000000000000001"));

        // Act
        var message = await _txRemoveOutputMessageTypeSerializer.DeserializeAsync(stream);

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
        var message = new TxRemoveOutputMessage(new TxRemoveOutputPayload(channelId, serialId));
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString("00000000000000000000000000000000000000000000000000000000000000000000000000000001");

        // Act
        await _txRemoveOutputMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }
}