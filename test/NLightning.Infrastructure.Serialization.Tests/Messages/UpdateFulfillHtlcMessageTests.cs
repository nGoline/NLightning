using NLightning.Domain.Channels.ValueObjects;

namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class UpdateFulfillHtlcMessageTests
{
    private readonly UpdateFulfillHtlcMessageTypeSerializer _fulfillHtlcMessageTypeSerializer;

    public UpdateFulfillHtlcMessageTests()
    {
        _fulfillHtlcMessageTypeSerializer =
            new UpdateFulfillHtlcMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    #region Deserialize

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsUpdateFulfillHtlcMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var expectedId = 0UL;
        var expectedPaymentPreimage =
            Convert.FromHexString("567cbdadb00b825448b2e414487d73a97f657f0634166d3ab3f3a2cc1042eda5");
        var stream = new MemoryStream(Convert.FromHexString(
                                          "00000000000000000000000000000000000000000000000000000000000000000000000000000000567CBDADB00B825448B2E414487D73A97F657F0634166D3AB3F3A2CC1042EDA5"));

        // Act
        var message = await _fulfillHtlcMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(expectedId, message.Payload.Id);
        Assert.Equal(expectedPaymentPreimage, message.Payload.PaymentPreimage);
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
        var paymentPreimage = Convert.FromHexString("567cbdadb00b825448b2e414487d73a97f657f0634166d3ab3f3a2cc1042eda5");
        var message = new UpdateFulfillHtlcMessage(new UpdateFulfillHtlcPayload(channelId, id, paymentPreimage));
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString(
                "00000000000000000000000000000000000000000000000000000000000000000000000000000000567CBDADB00B825448B2E414487D73A97F657F0634166D3AB3F3A2CC1042EDA5");

        // Act
        await _fulfillHtlcMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    #endregion
}