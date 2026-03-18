using NLightning.Domain.Channels.ValueObjects;

namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class StfuMessageTests
{
    private readonly StfuMessageTypeSerializer _stfuMessageTypeSerializer;

    public StfuMessageTests()
    {
        _stfuMessageTypeSerializer =
            new StfuMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsStfuMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var expectedInitiator = true;

        var stream =
            new MemoryStream(
                Convert.FromHexString("000000000000000000000000000000000000000000000000000000000000000001"));

        // Act
        var message = await _stfuMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(expectedInitiator, message.Payload.Initiator);
    }

    [Fact]
    public async Task Given_GivenValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        var message = new StfuMessage(new StfuPayload(channelId, true));
        var stream = new MemoryStream();
        var expectedBytes = Convert.FromHexString("000000000000000000000000000000000000000000000000000000000000000001");

        // Act
        await _stfuMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }
}