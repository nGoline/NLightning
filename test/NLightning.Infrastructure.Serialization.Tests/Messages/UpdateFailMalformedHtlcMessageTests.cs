using NLightning.Domain.Channels.ValueObjects;

namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class UpdateFailMalformedHtlcMessageTests
{
    private readonly UpdateFailMalformedHtlcMessageTypeSerializer _updateFailMalformedHtlcMessageTypeSerializer;

    public UpdateFailMalformedHtlcMessageTests()
    {
        _updateFailMalformedHtlcMessageTypeSerializer =
            new UpdateFailMalformedHtlcMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    #region Deserialize

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsUpdateFailMalformedHtlcMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var expectedId = 0UL;
        var expectedSha256OfOnion = new byte[32];
        ushort expectedFailureCode = 1;
        var stream = new MemoryStream(Convert.FromHexString(
                                          "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001"));

        // Act
        var message = await _updateFailMalformedHtlcMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(expectedId, message.Payload.Id);
        Assert.Equal(expectedSha256OfOnion, message.Payload.Sha256OfOnion);
        Assert.Equal(expectedFailureCode, message.Payload.FailureCode);
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
        var sha256OfOnion = new byte[32];
        ushort failureCode = 1;
        var message = new UpdateFailMalformedHtlcMessage(new UpdateFailMalformedHtlcPayload(channelId, failureCode, id,
                                                             sha256OfOnion));
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString(
                "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001");

        // Act
        await _updateFailMalformedHtlcMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    #endregion
}