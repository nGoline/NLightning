namespace NLightning.Infrastructure.Serialization.Tests.Messages;

using Domain.Channels.ValueObjects;
using Domain.Crypto.ValueObjects;
using Domain.Protocol.Messages;
using Domain.Protocol.Payloads;
using Helpers;
using Serialization.Messages.Types;

public class CommitmentSignedMessageTests
{
    private readonly CommitmentSignedMessageTypeSerializer _commitmentSignedMessageTypeSerializer;

    public CommitmentSignedMessageTests()
    {
        _commitmentSignedMessageTypeSerializer =
            new CommitmentSignedMessageTypeSerializer(SerializerHelper.PayloadSerializerFactory);
    }

    [Fact]
    public async Task Given_ValidStream_When_DeserializeAsync_Then_ReturnsCommitmentSignedMessage()
    {
        // Arrange
        var expectedChannelId = ChannelId.Zero;
        const int num = 2;
        var expectedSignatureBytes =
            Convert.FromHexString(
                "4737AF4C6314905296FD31D3610BD638F92C8A3687D0C6D845E3B9EF4957670733A30A9A81F924CD9F73F46805D0FB60D7C293FB2D8100DD3FA92B10934A7320");

        var stream = new MemoryStream(Convert.FromHexString(
                                          "00000000000000000000000000000000000000000000000000000000000000004737AF4C6314905296FD31D3610BD638F92C8A3687D0C6D845E3B9EF4957670733A30A9A81F924CD9F73F46805D0FB60D7C293FB2D8100DD3FA92B10934A732000024737AF4C6314905296FD31D3610BD638F92C8A3687D0C6D845E3B9EF4957670733A30A9A81F924CD9F73F46805D0FB60D7C293FB2D8100DD3FA92B10934A73204737AF4C6314905296FD31D3610BD638F92C8A3687D0C6D845E3B9EF4957670733A30A9A81F924CD9F73F46805D0FB60D7C293FB2D8100DD3FA92B10934A7320"));

        // Act
        var message = await _commitmentSignedMessageTypeSerializer.DeserializeAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(expectedChannelId, message.Payload.ChannelId);
        Assert.Equal(expectedSignatureBytes, message.Payload.Signature);
        Assert.Equal(num, message.Payload.NumHtlcs);
        Assert.NotEmpty(message.Payload.HtlcSignatures);
        foreach (var htlcSignature in message.Payload.HtlcSignatures)
        {
            Assert.Equal(expectedSignatureBytes, htlcSignature);
        }
    }

    [Fact]
    public async Task Given_GivenValidPayload_When_SerializeAsync_Then_WritesCorrectDataToStream()
    {
        // Arrange
        var channelId = ChannelId.Zero;
        var signature =
            Convert.FromHexString(
                "4737AF4C6314905296FD31D3610BD638F92C8A3687D0C6D845E3B9EF4957670733A30A9A81F924CD9F73F46805D0FB60D7C293FB2D8100DD3FA92B10934A7320");
        var message = new CommitmentSignedMessage(
            new CommitmentSignedPayload(channelId, new List<CompactSignature> { signature, signature }, signature));
        var stream = new MemoryStream();
        var expectedBytes = Convert.FromHexString(
            "00000000000000000000000000000000000000000000000000000000000000004737AF4C6314905296FD31D3610BD638F92C8A3687D0C6D845E3B9EF4957670733A30A9A81F924CD9F73F46805D0FB60D7C293FB2D8100DD3FA92B10934A732000024737AF4C6314905296FD31D3610BD638F92C8A3687D0C6D845E3B9EF4957670733A30A9A81F924CD9F73F46805D0FB60D7C293FB2D8100DD3FA92B10934A73204737AF4C6314905296FD31D3610BD638F92C8A3687D0C6D845E3B9EF4957670733A30A9A81F924CD9F73F46805D0FB60D7C293FB2D8100DD3FA92B10934A7320");

        // Act
        await _commitmentSignedMessageTypeSerializer.SerializeAsync(message, stream);
        stream.Position = 0;
        var result = new byte[stream.Length];
        _ = await stream.ReadAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBytes, result);
    }
}