using NLightning.Domain.Channels.ValueObjects;

namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Domain.Protocol.Tlv;
using Exceptions;
using Helpers;
using Serialization.Messages.Types;

public class ChannelReadyMessageTests
{
    private readonly ChannelReadyMessageTypeSerializer _channelReadyMessageTypeSerializer;

    public ChannelReadyMessageTests()
    {
        _channelReadyMessageTypeSerializer =
            new ChannelReadyMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory,
                                                  SerializerHelper.TlvConverterFactory,
                                                  SerializerHelper.TlvStreamSerializer);
    }

    #region Deserialize

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsChannelReadyMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var stream = new MemoryStream(Convert.FromHexString(
                                          "000000000000000000000000000000000000000000000000000000000000000003A92B07CBAE641DCFD482825233AECC2D5012913B48040131DB3222670C2BFFCD"));

        // Act
        var message = await _channelReadyMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Null(message.Extension);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsChannelReadyMessageWithExtension()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var expectedTlv = new ShortChannelIdTlv(new ShortChannelId(1234, 0, 1));
        var stream = new MemoryStream(Convert.FromHexString(
                                          "000000000000000000000000000000000000000000000000000000000000000003A92B07CBAE641DCFD482825233AECC2D5012913B48040131DB3222670C2BFFCD01080004D20000000001"));

        // Act
        var message = await _channelReadyMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.NotNull(message.Extension);
        Assert.NotNull(message.ShortChannelIdTlv);
        Assert.Equal(expectedTlv, message.ShortChannelIdTlv);
    }

    [Fact]
    public async Task Given_InvalidStreamContent_When_DeserializeAsync_Then_ThrowsMessageSerializationException()
    {
        // Arrange
        var invalidStream = new MemoryStream(Convert.FromHexString(
                                                 "000000000000000000000000000000000000000000000000000000000000000003A92B07CBAE641DCFD482825233AECC2D5012913B48040131DB3222670C2BFFCD010800"));

        // Act & Assert
        await Assert.ThrowsAsync<MessageSerializationException>(() =>
                                                                    _channelReadyMessageTypeSerializer.DeserializeAsync(
                                                                        invalidStream));
    }

    #endregion

    #region Serialize

    [Fact]
    public async Task Given_ValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        var secondPerCommitmentPoint =
            Convert.FromHexString("03a92b07cbae641dcfd482825233aecc2d5012913b48040131db3222670c2bffcd");
        var message = new ChannelReadyMessage(new ChannelReadyPayload(channelId, secondPerCommitmentPoint));
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString(
                "000000000000000000000000000000000000000000000000000000000000000003A92B07CBAE641DCFD482825233AECC2D5012913B48040131DB3222670C2BFFCD");

        // Act
        await _channelReadyMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    [Fact]
    public async Task Given_ValidExtension_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        var secondPerCommitmentPoint =
            Convert.FromHexString("03a92b07cbae641dcfd482825233aecc2d5012913b48040131db3222670c2bffcd");
        var shortChannelIdTlv = new ShortChannelIdTlv(new ShortChannelId(1234, 0, 1));
        var message = new ChannelReadyMessage(new ChannelReadyPayload(channelId, secondPerCommitmentPoint),
                                              shortChannelIdTlv);
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString(
                "000000000000000000000000000000000000000000000000000000000000000003A92B07CBAE641DCFD482825233AECC2D5012913B48040131DB3222670C2BFFCD01080004D20000000001");

        // Act
        await _channelReadyMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    #endregion
}