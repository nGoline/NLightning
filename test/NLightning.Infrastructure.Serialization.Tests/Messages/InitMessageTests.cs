namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Node;
using Domain.Protocol.Constants;
using Domain.Protocol.Messages;
using Domain.Protocol.Models;
using Domain.Protocol.Payloads;
using Domain.Protocol.Tlv;
using Exceptions;
using Helpers;
using Serialization.Messages.Types;

public class InitMessageTests
{
    private readonly InitMessageTypeSerializer _initMessageTypeSerializer;

    public InitMessageTests()
    {
        _initMessageTypeSerializer =
            new InitMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory,
                                          SerializerHelper.TlvConverterFactory,
                                          SerializerHelper.TlvStreamSerializer);
    }

    [Fact]
    public async Task
        Given_ValidStreamWithPayloadAndExtension_When_DeserializeAsync_Then_ReturnsInitMessageWithCorrectData()
    {
        // Arrange
        var expectedPayload = new InitPayload(new FeatureSet());
        var expectedExtension = new TlvStream();
        var expectedTlv = new NetworksTlv([ChainConstants.Main]);
        expectedExtension.Add(expectedTlv);
        var stream =
            new MemoryStream(
                Convert.FromHexString(
                    "00025101000610000000510101206fe28c0ab6f1b372c1a6a246ae63f74f931e8365e15a089c68d6190000000000"));

        // Act
        var initMessage = await _initMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        BaseTlv? tlv = null;
        Assert.NotNull(initMessage);
        Assert.Equal(expectedPayload.FeatureSet.ToString(), initMessage.Payload.FeatureSet.ToString());
        var hasTlv = initMessage.Extension?.TryGetTlv(TlvConstants.Networks, out tlv);
        Assert.True(hasTlv);
        Assert.Equal(expectedTlv.Value, tlv!.Value);
    }

    [Fact]
    public async Task Given_ValidStreamWithOnlyPayload_When_DeserializeAsync_Then_ReturnsInitMessageWithNullExtension()
    {
        // Arrange
        var expectedPayload = new InitPayload(new FeatureSet());
        var stream = new MemoryStream(Convert.FromHexString("000251010006100000005101"));

        // Act
        var initMessage = await _initMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(initMessage);
        Assert.Equal(expectedPayload.FeatureSet.ToString(), initMessage.Payload.FeatureSet.ToString());
        Assert.Null(initMessage.Extension);
    }

    [Fact]
    public async Task Given_InvalidStreamContent_When_DeserializeAsync_Then_ThrowsMessageSerializationException()
    {
        // Arrange
        var invalidStream = new MemoryStream(Convert.FromHexString("00020200000202000102"));

        // Act & Assert
        await Assert.ThrowsAsync<MessageSerializationException>(() => _initMessageTypeSerializer.DeserializeAsync(
                                                                    invalidStream));
    }

    [Fact]
    public async Task Given_ValidPayloadAndExtension_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var message = new InitMessage(new InitPayload(new FeatureSet()), new NetworksTlv([ChainConstants.Main]));
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString(
                "00025101000610000000510101206fe28c0ab6f1b372c1a6a246ae63f74f931e8365e15a089c68d6190000000000");

        // Act
        await _initMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    [Fact]
    public async Task Given_ValidPayloadOnly_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var message = new InitMessage(new InitPayload(new FeatureSet()));
        var stream = new MemoryStream();
        var expectedBytes = Convert.FromHexString("000251010006100000005101");

        // Act
        await _initMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }
}