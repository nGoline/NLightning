using NLightning.Domain.Bitcoin.ValueObjects;
using NLightning.Domain.Channels.ValueObjects;

namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class TxSignaturesMessageTests
{
    private readonly TxSignaturesMessageTypeSerializer _txSignaturesMessageTypeSerializer;

    public TxSignaturesMessageTests()
    {
        _txSignaturesMessageTypeSerializer =
            new TxSignaturesMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsTxSignaturesMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        byte[] expectedTxId = ChannelId.Zero; // 32 zeros
        var expectedWitnesses = new List<Witness>
        {
            new([0xFF, 0xFF, 0xFF, 0xFD])
        };
        var stream = new MemoryStream(Convert.FromHexString(
                                          "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000010004FFFFFFFD"));

        // Act
        var message = await _txSignaturesMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(expectedTxId, message.Payload.TxId);
        Assert.Equal(expectedWitnesses.Count, message.Payload.Witnesses.Count);
        Assert.Equal(expectedWitnesses[0], message.Payload.Witnesses[0]);
    }

    [Fact]
    public async Task Given_GivenValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        byte[] txId = ChannelId.Zero; // 32 zeros
        var witnesses = new List<Witness>
        {
            new([0xFF, 0xFF, 0xFF, 0xFD])
        };
        var message = new TxSignaturesMessage(new TxSignaturesPayload(channelId, txId, witnesses));
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString(
                "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000010004FFFFFFFD");

        // Act
        await _txSignaturesMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }
}