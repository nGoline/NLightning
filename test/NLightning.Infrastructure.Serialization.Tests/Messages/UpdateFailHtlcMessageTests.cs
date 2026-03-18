using NLightning.Domain.Channels.ValueObjects;

namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class UpdateFailHtlcMessageTests
{
    private readonly UpdateFailHtlcMessageTypeSerializer _updateFailHtlcMessageTypeSerializer;

    public UpdateFailHtlcMessageTests()
    {
        _updateFailHtlcMessageTypeSerializer =
            new UpdateFailHtlcMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    #region Deserialize

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsUpdateFailHtlcMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var expectedId = 0UL;
        var expectedReason = Convert.FromHexString("567cbdadb00b825448b2e414487d73");
        var expectedLen = expectedReason.Length;
        var stream = new MemoryStream(Convert.FromHexString(
                                          "00000000000000000000000000000000000000000000000000000000000000000000000000000000000F567CBDADB00B825448B2E414487D73"));

        // Act
        var message = await _updateFailHtlcMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(expectedId, message.Payload.Id);
        Assert.Equal(expectedLen, message.Payload.Len);
        Assert.Equal(expectedReason, message.Payload.Reason);
        Assert.Null(message.Extension);
    }

    #endregion

    #region Serialize

    [Fact]
    public async Task Given_ValidPayloadWith_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        var id = 0UL;
        var expectedReason = Convert.FromHexString("567cbdadb00b825448b2e414487d73");
        var message = new UpdateFailHtlcMessage(new UpdateFailHtlcPayload(channelId, id, expectedReason));
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString(
                "00000000000000000000000000000000000000000000000000000000000000000000000000000000000F567CBDADB00B825448B2E414487D73");

        // Act
        await _updateFailHtlcMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    #endregion
}