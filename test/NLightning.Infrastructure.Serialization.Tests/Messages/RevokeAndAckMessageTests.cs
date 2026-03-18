namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Channels.ValueObjects;
using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class RevokeAndAckMessageTests
{
    private readonly RevokeAndAckMessageTypeSerializer _revokeAndAckMessageTypeSerializer;

    public RevokeAndAckMessageTests()
    {
        _revokeAndAckMessageTypeSerializer =
            new RevokeAndAckMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsRevokeAndAckMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        var perCommitmentSecret =
            Convert.FromHexString("c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");
        var nextPerCommitmentPoint =
            Convert.FromHexString("02c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");

        var stream = new MemoryStream(Convert.FromHexString(
                                          "0000000000000000000000000000000000000000000000000000000000000000C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A7502C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A75"));

        // Act
        var message = await _revokeAndAckMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(perCommitmentSecret, message.Payload.PerCommitmentSecret);
        Assert.Equal(nextPerCommitmentPoint, message.Payload.NextPerCommitmentPoint);
    }

    [Fact]
    public async Task Given_GivenValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        var perCommitmentSecret =
            Convert.FromHexString("c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");
        var nextPerCommitmentPoint =
            Convert.FromHexString("02c93ca7dca44d2e45e3cc5419d92750f7fb3a0f180852b73a621f4051c0193a75");
        var message =
            new RevokeAndAckMessage(new RevokeAndAckPayload(channelId, nextPerCommitmentPoint, perCommitmentSecret));
        var stream = new MemoryStream();
        var expectedBytes =
            Convert.FromHexString(
                "0000000000000000000000000000000000000000000000000000000000000000C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A7502C93CA7DCA44D2E45E3CC5419D92750F7FB3A0F180852B73A621F4051C0193A75");

        // Act
        await _revokeAndAckMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }
}