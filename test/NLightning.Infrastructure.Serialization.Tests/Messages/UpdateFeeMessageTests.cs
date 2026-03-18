using NLightning.Domain.Channels.ValueObjects;

namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class UpdateFeeMessageTests
{
    private readonly UpdateFeeMessageTypeSerializer _updateFeeMessageTypeSerializer;

    public UpdateFeeMessageTests()
    {
        _updateFeeMessageTypeSerializer =
            new UpdateFeeMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsUpdateFeeMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var expectedFeeratePerKw = 10U;

        var stream =
            new MemoryStream(
                Convert.FromHexString("00000000000000000000000000000000000000000000000000000000000000000000000A"));

        // Act
        var message = await _updateFeeMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(expectedFeeratePerKw, message.Payload.FeeratePerKw);
    }

    [Fact]
    public async Task Given_GivenValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        var feeratePerKw = 10U;
        var message = new UpdateFeeMessage(new UpdateFeePayload(channelId, feeratePerKw));
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString("00000000000000000000000000000000000000000000000000000000000000000000000A");

        // Act
        await _updateFeeMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }
}