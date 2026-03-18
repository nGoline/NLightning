namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Channels.ValueObjects;
using Domain.Money;
using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class TxAddOutputMessageTests
{
    private readonly TxAddOutputMessageTypeSerializer _txAddOutputMessageTypeSerializer;

    public TxAddOutputMessageTests()
    {
        _txAddOutputMessageTypeSerializer =
            new TxAddOutputMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsTxAddOutputMessage()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        const ulong serialId = 1;
        var sats = LightningMoney.Satoshis(1_000);
        var script = Convert.FromHexString("002062B6D464DBEFFD3102C03881699D19C833F1C2B114825BF31900F26845C0D6DE");

        var stream = new MemoryStream(Convert.FromHexString(
                                          "0000000000000000000000000000000000000000000000000000000000000000000000000000000100000000000003E80022002062B6D464DBEFFD3102C03881699D19C833F1C2B114825BF31900F26845C0D6DE"));

        // Act
        var message = await _txAddOutputMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(channelId, message.Payload.ChannelId);
        Assert.Equal(serialId, message.Payload.SerialId);
        Assert.Equal(sats, message.Payload.Amount);
        Assert.Equal(script, message.Payload.Script);
    }

    [Fact]
    public async Task Given_GivenValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        const ulong serialId = 1;
        var sats = LightningMoney.Satoshis(1_000);
        var script = Convert.FromHexString("002062B6D464DBEFFD3102C03881699D19C833F1C2B114825BF31900F26845C0D6DE");
        var message = new TxAddOutputMessage(new TxAddOutputPayload(sats, channelId, script, serialId));
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString(
                "0000000000000000000000000000000000000000000000000000000000000000000000000000000100000000000003E80022002062B6D464DBEFFD3102C03881699D19C833F1C2B114825BF31900F26845C0D6DE");

        // Act
        await _txAddOutputMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }
}