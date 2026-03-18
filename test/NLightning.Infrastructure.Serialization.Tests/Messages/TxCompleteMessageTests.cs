using NLightning.Domain.Channels.ValueObjects;

namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class TxCompleteMessageTests
{
    private readonly TxCompleteMessageTypeSerializer _txCompleteMessageTypeSerializer;

    public TxCompleteMessageTests()
    {
        _txCompleteMessageTypeSerializer =
            new TxCompleteMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    [Fact]
    public async Task DeserializeAsync_GivenValidStream_ReturnsTxCompleteMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var stream =
            new MemoryStream(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));

        // Act
        var message = await _txCompleteMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
    }

    [Fact]
    public async Task SerializeAsync_GivenValidPayload_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        var message = new TxCompleteMessage(new TxCompletePayload(channelId));
        var stream = new MemoryStream();
        var expectedBytes = Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000");

        // Act
        await _txCompleteMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }
}