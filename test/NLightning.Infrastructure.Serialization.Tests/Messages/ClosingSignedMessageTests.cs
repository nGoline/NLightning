using NLightning.Domain.Channels.ValueObjects;

namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Money;
using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Domain.Protocol.Tlv;
using Exceptions;
using Helpers;
using Serialization.Messages.Types;

public class ClosingSignedMessageTests
{
    private readonly ClosingSignedMessageTypeSerializer _closingSignedMessageTypeSerializer;

    public ClosingSignedMessageTests()
    {
        _closingSignedMessageTypeSerializer =
            new ClosingSignedMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory,
                                                   SerializerHelper.TlvConverterFactory,
                                                   SerializerHelper.TlvStreamSerializer);
    }

    #region Deserialize

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsClosingSignedMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var expectedFeeSatoshis = LightningMoney.Satoshis(2);
        var expectedMinFee = LightningMoney.Satoshis(1);
        var expectedMaxFee = LightningMoney.Satoshis(3);
        var expectedSignatureBytes =
            Convert.FromHexString(
                "4737AF4C6314905296FD31D3610BD638F92C8A3687D0C6D845E3B9EF4957670733A30A9A81F924CD9F73F46805D0FB60D7C293FB2D8100DD3FA92B10934A7320");

        var stream = new MemoryStream(Convert.FromHexString(
                                          "000000000000000000000000000000000000000000000000000000000000000000000000000000024737AF4C6314905296FD31D3610BD638F92C8A3687D0C6D845E3B9EF4957670733A30A9A81F924CD9F73F46805D0FB60D7C293FB2D8100DD3FA92B10934A7320011000000000000000010000000000000003"));

        // Act
        var message = await _closingSignedMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(expectedFeeSatoshis, message.Payload.FeeAmount);
        Assert.Equal(expectedSignatureBytes, message.Payload.Signature);
        Assert.NotNull(message.Extension);
        Assert.Equal(expectedMinFee, message.FeeRangeTlv.MinFeeAmount);
        Assert.Equal(expectedMaxFee, message.FeeRangeTlv.MaxFeeAmount);
    }

    [Fact]
    public async Task Given_InvalidStreamContent_When_DeserializeAsync_Then_ThrowsMessageSerializationException()
    {
        // Arrange
        var invalidStream = new MemoryStream(Convert.FromHexString(
                                                 "000000000000000000000000000000000000000000000000000000000000000000000000000000024737AF4C6314905296FD31D3610BD638F92C8A3687D0C6D845E3B9EF4957670733A30A9A81F924CD9F73F46805D0FB60D7C293FB2D8100DD3FA92B10934A73200110"));

        // Act & Assert
        await Assert.ThrowsAsync<MessageSerializationException>(() => _closingSignedMessageTypeSerializer
                                                                   .DeserializeAsync(invalidStream));
    }

    #endregion

    #region Serialize

    [Fact]
    public async Task Given_ValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        var feeSatoshis = LightningMoney.Satoshis(2);
        var minFee = LightningMoney.Satoshis(1);
        var maxFee = LightningMoney.Satoshis(3);
        var signature =
            Convert.FromHexString(
                "4737AF4C6314905296FD31D3610BD638F92C8A3687D0C6D845E3B9EF4957670733A30A9A81F924CD9F73F46805D0FB60D7C293FB2D8100DD3FA92B10934A7320");
        var message = new ClosingSignedMessage(new ClosingSignedPayload(channelId, feeSatoshis, signature),
                                               new FeeRangeTlv(minFee, maxFee));
        var stream = new MemoryStream();
        var expectedBytes = Convert.FromHexString(
            "000000000000000000000000000000000000000000000000000000000000000000000000000000024737AF4C6314905296FD31D3610BD638F92C8A3687D0C6D845E3B9EF4957670733A30A9A81F924CD9F73F46805D0FB60D7C293FB2D8100DD3FA92B10934A7320011000000000000000010000000000000003");

        // Act
        await _closingSignedMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    #endregion
}