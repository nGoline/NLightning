namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Channels.ValueObjects;
using Domain.Money;
using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Domain.Protocol.Tlv;
using Exceptions;
using Helpers;
using Serialization.Messages.Types;

public class TxInitRbfMessageTests
{
    private readonly TxInitRbfMessageTypeSerializer _txInitRbfMessageTypeSerializer;

    public TxInitRbfMessageTests()
    {
        _txInitRbfMessageTypeSerializer =
            new TxInitRbfMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory,
                                               SerializerHelper.TlvConverterFactory,
                                               SerializerHelper.TlvStreamSerializer);
    }

    #region Deserialize

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsTxInitRbfMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        const uint expectedLocktime = 1;
        const uint expectedFeerate = 1;
        var stream =
            new MemoryStream(
                Convert.FromHexString(
                    "00000000000000000000000000000000000000000000000000000000000000000000000100000001"));

        // Act
        var message = await _txInitRbfMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(expectedLocktime, message.Payload.Locktime);
        Assert.Equal(expectedFeerate, message.Payload.Feerate);
        Assert.Null(message.Extension);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsTxInitRbfMessageWithExtensions()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        const uint expectedLocktime = 1;
        const uint expectedFeerate = 1;
        var expectedTlv = new FundingOutputContributionTlv(LightningMoney.Satoshis(10));
        var expectedTlv2 = new RequireConfirmedInputsTlv();
        var stream = new MemoryStream(Convert.FromHexString(
                                          "000000000000000000000000000000000000000000000000000000000000000000000001000000010008000000000000000A0200"));

        // Act
        var message = await _txInitRbfMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(expectedLocktime, message.Payload.Locktime);
        Assert.Equal(expectedFeerate, message.Payload.Feerate);
        Assert.NotNull(message.FundingOutputContributionTlv);
        Assert.Equal(expectedTlv, message.FundingOutputContributionTlv);
        Assert.NotNull(message.RequireConfirmedInputsTlv);
        Assert.Equal(expectedTlv2, message.RequireConfirmedInputsTlv);
    }

    [Fact]
    public async Task Given_InvalidStreamContent_When_DeserializeAsync_Then_ThrowsMessageSerializationException()
    {
        // Arrange
        var invalidStream =
            new MemoryStream(
                Convert.FromHexString(
                    "000000000000000000000000000000000000000000000000000000000000000000000001000000010002"));

        // Act & Assert
        await Assert.ThrowsAsync<MessageSerializationException>(() => _txInitRbfMessageTypeSerializer.DeserializeAsync(
                                                                    invalidStream));
    }

    #endregion

    #region Serialize

    [Fact]
    public async Task Given_ValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        const uint locktime = 1;
        const uint feerate = 1;
        var message = new TxInitRbfMessage(new TxInitRbfPayload(channelId, locktime, feerate));
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString("00000000000000000000000000000000000000000000000000000000000000000000000100000001");

        // Act
        await _txInitRbfMessageTypeSerializer.SerializeAsync(message, stream);
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
        const uint locktime = 1;
        const uint feerate = 1;
        var fundingOutputContributionTlv = new FundingOutputContributionTlv(LightningMoney.Satoshis(10));
        var requireConfirmedInputsTlv = new RequireConfirmedInputsTlv();
        var message = new TxInitRbfMessage(new TxInitRbfPayload(channelId, locktime, feerate),
                                           fundingOutputContributionTlv, requireConfirmedInputsTlv);
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString(
                "000000000000000000000000000000000000000000000000000000000000000000000001000000010008000000000000000A0200");

        // Act
        await _txInitRbfMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    #endregion
}