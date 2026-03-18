namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class WarningMessageTests
{
    private readonly WarningMessageTypeSerializer _warningMessageTypeSerializer;

    public WarningMessageTests()
    {
        _warningMessageTypeSerializer =
            new WarningMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsWarningMessageWithCorrectPayload()
    {
        // Arrange
        var expectedPayload = new ErrorPayload("Warning message!");
        var stream = new MemoryStream(Convert.FromHexString(
                                          "000000000000000000000000000000000000000000000000000000000000000000105761726E696E67206D65737361676521"));

        // Act
        var errorMessage = await _warningMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(errorMessage);
        Assert.Equal(expectedPayload.ChannelId, errorMessage.Payload.ChannelId);
        Assert.Equal(expectedPayload.Data, errorMessage.Payload.Data);
    }

    [Fact]
    public async Task Given_ValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var message = new WarningMessage(new ErrorPayload("Warning message!"));
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString(
                "000000000000000000000000000000000000000000000000000000000000000000105761726E696E67206D65737361676521");

        // Act
        await _warningMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }
}