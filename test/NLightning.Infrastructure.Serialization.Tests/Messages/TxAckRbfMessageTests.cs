using NLightning.Domain.Channels.ValueObjects;
using NLightning.Domain.Money;

namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Domain.Protocol.Tlv;
using Exceptions;
using Helpers;
using Serialization.Messages.Types;

public class TxAckRbfMessageTests
{
    private readonly TxAckRbfMessageTypeSerializer _txAckRbfMessageTypeSerializer;

    public TxAckRbfMessageTests()
    {
        _txAckRbfMessageTypeSerializer =
            new TxAckRbfMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory,
                                              SerializerHelper.TlvConverterFactory,
                                              SerializerHelper.TlvStreamSerializer);
    }

    #region Deserialize

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsTxAckRbfMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var stream =
            new MemoryStream(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));

        // Act
        var message = await _txAckRbfMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Null(message.Extension);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsTxAckRbfMessageWithExtensions()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var expectedTlv = new FundingOutputContributionTlv(LightningMoney.Satoshis(10));
        var stream =
            new MemoryStream(Convert.FromHexString(
                                 "00000000000000000000000000000000000000000000000000000000000000000008000000000000000A0200"));

        // Act
        var message = await _txAckRbfMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.NotNull(message.Extension);
        Assert.NotNull(message.FundingOutputContributionTlv);
        Assert.Equal(expectedTlv, message.FundingOutputContributionTlv);
        Assert.NotNull(message.RequireConfirmedInputsTlv);
    }

    [Fact]
    public async Task Given_InvalidStreamContent_When_DeserializeAsync_Then_ThrowsMessageSerializationException()
    {
        // Arrange
        var invalidStream =
            new MemoryStream(
                Convert.FromHexString("00000000000000000000000000000000000000000000000000000000000000000002"));

        // Act & Assert
        await Assert.ThrowsAsync<MessageSerializationException>(() => _txAckRbfMessageTypeSerializer.DeserializeAsync(
                                                                    invalidStream));
    }

    #endregion

    #region Serialize

    [Fact]
    public async Task Given_ValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        var message = new TxAckRbfMessage(new TxAckRbfPayload(channelId));
        var stream = new MemoryStream();
        var expectedBytes = Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000");

        // Act
        await _txAckRbfMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    [Fact]
    public async Task Given_ValidExtensions_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        var fundingOutputContributionTlv = new FundingOutputContributionTlv(LightningMoney.Satoshis(10));
        var requireConfirmedInputsTlv = new RequireConfirmedInputsTlv();
        var message = new TxAckRbfMessage(new TxAckRbfPayload(channelId), fundingOutputContributionTlv,
                                          requireConfirmedInputsTlv);
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString(
                "00000000000000000000000000000000000000000000000000000000000000000008000000000000000A0200");

        // Act
        await _txAckRbfMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    #endregion
}