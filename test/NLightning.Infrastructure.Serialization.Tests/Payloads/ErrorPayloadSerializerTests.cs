using NLightning.Domain.Channels.ValueObjects;

namespace NLightning.Infrastructure.Serialization.Tests.Payloads;

using Converters;
using Domain.Protocol.Payloads;
using Factories;
using Infrastructure.Serialization.Payloads;

public class ErrorPayloadSerializerTests
{
    [Fact]
    public async Task Given_ValidPayload_When_Serializing_Then_ReturnsCorrectValues()
    {
        // Given
        var errorPayloadTypeSerializer = new ErrorPayloadSerializer(new ValueObjectSerializerFactory());
        var errorPayload = new ErrorPayload(ChannelId.Zero);
        using var memoryStream = new MemoryStream();

        // When
        await errorPayloadTypeSerializer.SerializeAsync(errorPayload, memoryStream);

        // Then
        memoryStream.Seek(0, SeekOrigin.Begin);
        var expectedLengthBytes = new byte[2];
        _ = await memoryStream.ReadAsync(expectedLengthBytes.AsMemory(0, 2), TestContext.Current.CancellationToken);

        Assert.Equal(0, EndianBitConverter.ToUInt16BigEndian(expectedLengthBytes));
    }
}