using NLightning.Domain.Channels.ValueObjects;

namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Domain.Protocol.Tlv;
using Exceptions;
using Helpers;
using Serialization.Messages.Types;

public class ChannelReestablishMessageTests
{
    private readonly ChannelReestablishMessageTypeSerializer _channelReestablishMessageTypeSerializer;

    public ChannelReestablishMessageTests()
    {
        _channelReestablishMessageTypeSerializer =
            new ChannelReestablishMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory,
                                                        SerializerHelper.TlvConverterFactory,
                                                        SerializerHelper.TlvStreamSerializer);
    }

    #region Deserialize

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsChannelReestablishMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var expectedNextCommitmentNumber = 1UL;
        var expectedNextRevocationNumber = 2UL;
        var expectedYourLastPerCommitmentSecret =
            Convert.FromHexString("567cbdadb00b825448b2e414487d73a97f657f0634166d3ab3f3a2cc1042eda5");
        var expectedMyCurrentPerCommitmentPoint =
            Convert.FromHexString("02c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");
        var stream = new MemoryStream(Convert.FromHexString(
                                          "000000000000000000000000000000000000000000000000000000000000000000000000000000010000000000000002567CBDADB00B825448B2E414487D73A97F657F0634166D3AB3F3A2CC1042EDA502C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A75"));

        // Act
        var message = await _channelReestablishMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(expectedNextCommitmentNumber, message.Payload.NextCommitmentNumber);
        Assert.Equal(expectedNextRevocationNumber, message.Payload.NextRevocationNumber);
        Assert.Equal(expectedYourLastPerCommitmentSecret, message.Payload.YourLastPerCommitmentSecret);
        Assert.Equal(expectedMyCurrentPerCommitmentPoint, message.Payload.MyCurrentPerCommitmentPoint);
        Assert.Null(message.Extension);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsChannelReestablishMessageWithExtensions()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var expectedNextCommitmentNumber = 1UL;
        var expectedNextRevocationNumber = 2UL;
        var expectedYourLastPerCommitmentSecret =
            Convert.FromHexString("567cbdadb00b825448b2e414487d73a97f657f0634166d3ab3f3a2cc1042eda5");
        var expectedMyCurrentPerCommitmentPoint =
            Convert.FromHexString("02c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");
        var nextFundingTlv =
            new NextFundingTlv(
                Convert.FromHexString("567cbdadb00b825448b2e414487d73a97f657f0634166d3ab3f3a2cc1042eda5"));
        var stream = new MemoryStream(Convert.FromHexString(
                                          "000000000000000000000000000000000000000000000000000000000000000000000000000000010000000000000002567CBDADB00B825448B2E414487D73A97F657F0634166D3AB3F3A2CC1042EDA502C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A750020567CBDADB00B825448B2E414487D73A97F657F0634166D3AB3F3A2CC1042EDA5"));

        // Act
        var message = await _channelReestablishMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(expectedNextCommitmentNumber, message.Payload.NextCommitmentNumber);
        Assert.Equal(expectedNextRevocationNumber, message.Payload.NextRevocationNumber);
        Assert.Equal(expectedYourLastPerCommitmentSecret, message.Payload.YourLastPerCommitmentSecret);
        Assert.Equal(expectedMyCurrentPerCommitmentPoint, message.Payload.MyCurrentPerCommitmentPoint);
        Assert.NotNull(message.Extension);
        Assert.NotNull(message.NextFundingTlv);
        Assert.Equal(nextFundingTlv, message.NextFundingTlv);
    }

    [Fact]
    public async Task Given_InvalidStreamContent_When_DeserializeAsync_Then_ThrowsMessageSerializationException()
    {
        // Arrange
        var invalidStream = new MemoryStream(Convert.FromHexString(
                                                 "000000000000000000000000000000000000000000000000000000000000000000000000000000010000000000000002567CBDADB00B825448B2E414487D73A97F657F0634166D3AB3F3A2CC1042EDA502C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A750002"));

        // Act & Assert
        await Assert.ThrowsAsync<MessageSerializationException>(() => _channelReestablishMessageTypeSerializer
                                                                   .DeserializeAsync(invalidStream));
    }

    #endregion

    #region Serialize

    [Fact]
    public async Task Given_ValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        var nextCommitmentNumber = 1UL;
        var nextRevocationNumber = 2UL;
        var yourLastPerCommitmentSecret =
            Convert.FromHexString("567cbdadb00b825448b2e414487d73a97f657f0634166d3ab3f3a2cc1042eda5");
        var myCurrentPerCommitmentPoint =
            Convert.FromHexString("02c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");
        var message = new ChannelReestablishMessage(new ChannelReestablishPayload(
                                                        channelId, myCurrentPerCommitmentPoint, nextCommitmentNumber,
                                                        nextRevocationNumber, yourLastPerCommitmentSecret));
        var stream = new MemoryStream();
        var expectedBytes = Convert.FromHexString(
            "000000000000000000000000000000000000000000000000000000000000000000000000000000010000000000000002567CBDADB00B825448B2E414487D73A97F657F0634166D3AB3F3A2CC1042EDA502C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A75");

        // Act
        await _channelReestablishMessageTypeSerializer.SerializeAsync(message, stream);
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
        var nextCommitmentNumber = 1UL;
        var nextRevocationNumber = 2UL;
        var yourLastPerCommitmentSecret =
            Convert.FromHexString("567cbdadb00b825448b2e414487d73a97f657f0634166d3ab3f3a2cc1042eda5");
        var myCurrentPerCommitmentPoint =
            Convert.FromHexString("02c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");
        var nextFundingTlv =
            new NextFundingTlv(
                Convert.FromHexString("567cbdadb00b825448b2e414487d73a97f657f0634166d3ab3f3a2cc1042eda5"));
        var message = new ChannelReestablishMessage(
            new ChannelReestablishPayload(channelId, myCurrentPerCommitmentPoint, nextCommitmentNumber,
                                          nextRevocationNumber, yourLastPerCommitmentSecret), nextFundingTlv);
        var stream = new MemoryStream();
        var expectedBytes = Convert.FromHexString(
            "000000000000000000000000000000000000000000000000000000000000000000000000000000010000000000000002567CBDADB00B825448B2E414487D73A97F657F0634166D3AB3F3A2CC1042EDA502C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A750020567CBDADB00B825448B2E414487D73A97F657F0634166D3AB3F3A2CC1042EDA5");

        // Act
        await _channelReestablishMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    #endregion
}