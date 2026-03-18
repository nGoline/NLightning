using System.Text;
using NLightning.Domain.Channels.ValueObjects;

namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class ErrorMessageTests
{
    private readonly ErrorMessageTypeSerializer _errorMessageTypeSerializer;

    public ErrorMessageTests()
    {
        _errorMessageTypeSerializer =
            new ErrorMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsErrorMessageWithCorrectPayload()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var errorMessage = "Error message!";
        var expectedData = Encoding.UTF8.GetBytes(errorMessage);
        var stream = new MemoryStream(Convert.FromHexString(
                                          "0000000000000000000000000000000000000000000000000000000000000000000E4572726F72206D65737361676521"));

        // Act
        var message = await _errorMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(expectedData, message.Payload.Data);
    }

    [Fact]
    public async Task Given_ValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var message = new ErrorMessage(new ErrorPayload("Error message!"));
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString(
                "0000000000000000000000000000000000000000000000000000000000000000000E4572726F72206D65737361676521");

        // Act
        await _errorMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }
}