using NLightning.Domain.Protocol.Tlv;
using NLightning.Domain.Protocol.ValueObjects;
using NLightning.Infrastructure.Serialization.Factories;
using NLightning.Infrastructure.Serialization.Tlv;

namespace NLightning.Infrastructure.Serialization.Tests.Tlv;

public class TlvSerializerTests
{
    private readonly TlvSerializer _tlvSerializer;

    public TlvSerializerTests()
    {
        _tlvSerializer = new TlvSerializer(new ValueObjectSerializerFactory());
    }

    [Fact]
    public async Task Given_TlvSerializer_When_SerializingBaseTlv_Then_BufferIsCorrect()
    {
        // Given
        var baseTlv = new BaseTlv(0, 3, [0x01, 0x02, 0x03]);
        var expectedBuffer = new byte[] { 0x00, 0x03, 0x01, 0x02, 0x03 };
        using var stream = new MemoryStream();

        // When
        await _tlvSerializer.SerializeAsync(baseTlv, stream);
        stream.Position = 0;
        var buffer = new byte[stream.Length];
        await stream.ReadExactlyAsync(buffer, 0, (int)stream.Length, TestContext.Current.CancellationToken);

        // Then
        Assert.Equal(expectedBuffer, buffer);
    }

    [Fact]
    public async Task Given_TlvSerializer_When_DeserializingBaseTlv_Then_BufferIsCorrect()
    {
        // Given
        var expectedType = new BigSize(0);
        var expectedLength = new BigSize(3);
        byte[] expectedValue = [0x01, 0x02, 0x03];
        using var stream = new MemoryStream([0x00, 0x03, 0x01, 0x02, 0x03]);

        // When
        var baseTlv = await _tlvSerializer.DeserializeAsync(stream);

        // Then
        Assert.NotNull(baseTlv);
        Assert.Equal(expectedType, baseTlv.Type);
        Assert.Equal(expectedLength, baseTlv.Length);
        Assert.Equal(expectedValue, baseTlv.Value);
    }

    // [Fact]
    // public async Task BlindedPathTlv_SerializationAndDeserialization_WorksCorrectly()
    // {
    //     // Arrange
    //     var pubKey = new PubKey("023da092f6980e58d2c037173180e9a465476026ee50f96695963e8efe436f54eb");
    //     var blindedPathTlv = new BlindedPathTlv(pubKey);
    //
    //     // Act
    //     using var stream = new MemoryStream();
    //     await blindedPathTlv.SerializeAsync(stream);
    //     stream.Position = 0;
    //     var deserializedTlv = BlindedPathTlv.FromTlv(await Tlv.DeserializeAsync(stream));
    //
    //     // Assert
    //     Assert.Equal(blindedPathTlv.PathKey, deserializedTlv.PathKey);
    // }
    //
    // [Fact]
    // public async Task ChannelTypeTlv_SerializationAndDeserialization_WorksCorrectly()
    // {
    //     // Arrange
    //     var channelType = new byte[] { 0x01, 0x02, 0x03 };
    //     var channelTypeTlv = new ChannelTypeTlv(channelType);
    //
    //     // Act
    //     using var stream = new MemoryStream();
    //     await channelTypeTlv.SerializeAsync(stream);
    //     stream.Position = 0;
    //     var deserializedTlv = ChannelTypeTlv.FromTlv(await Tlv.DeserializeAsync(stream));
    //
    //     // Assert
    //     Assert.Equal(channelTypeTlv.ChannelType, deserializedTlv.ChannelType);
    // }
    //
    // [Fact]
    // public async Task FeeRangeTlv_SerializationAndDeserialization_WorksCorrectly()
    // {
    //     // Arrange
    //     var minFee = LightningMoney.Satoshis(1000);
    //     var maxFee = LightningMoney.Satoshis(2000);
    //     var feeRangeTlv = new FeeRangeTlv(minFee, maxFee);
    //
    //     // Act
    //     using var stream = new MemoryStream();
    //     await feeRangeTlv.SerializeAsync(stream);
    //     stream.Position = 0;
    //     var deserializedTlv = FeeRangeTlv.FromTlv(await Tlv.DeserializeAsync(stream));
    //
    //     // Assert
    //     Assert.Equal(feeRangeTlv.MinFeeAmount, deserializedTlv.MinFeeAmount);
    //     Assert.Equal(feeRangeTlv.MaxFeeAmount, deserializedTlv.MaxFeeAmount);
    // }
    //
    // [Fact]
    // public async Task FundingOutputContributionTlv_SerializationAndDeserialization_WorksCorrectly()
    // {
    //     // Arrange
    //     var satoshis = 5000L;
    //     var fundingTlv = new FundingOutputContributionTlv(satoshis);
    //
    //     // Act
    //     using var stream = new MemoryStream();
    //     await fundingTlv.SerializeAsync(stream);
    //     stream.Position = 0;
    //     var deserializedTlv = FundingOutputContributionTlv.FromTlv(await Tlv.DeserializeAsync(stream));
    //
    //     // Assert
    //     Assert.Equal(fundingTlv.Amount, deserializedTlv.Satoshis);
    // }
    //
    // [Fact]
    // public async Task NetworksTlv_SerializationAndDeserialization_WorksCorrectly()
    // {
    //     // Arrange
    //     var chainHashes = new[] { new ChainHash(new byte[32]), new ChainHash(new byte[32]) };
    //     var networksTlv = new NetworksTlv(chainHashes);
    //
    //     // Act
    //     using var stream = new MemoryStream();
    //     await networksTlv.SerializeAsync(stream);
    //     stream.Position = 0;
    //     var deserializedTlv = NetworksTlv.FromTlv(await Tlv.DeserializeAsync(stream));
    //
    //     // Assert
    //     Assert.Equal(networksTlv.ChainHashes?.Count(), deserializedTlv.ChainHashes?.Count());
    // }
    //
    // [Fact]
    // public async Task RequireConfirmedInputsTlv_SerializationAndDeserialization_WorksCorrectly()
    // {
    //     // Arrange
    //     var requireConfirmedInputsTlv = new RequireConfirmedInputsTlv();
    //
    //     // Act
    //     using var stream = new MemoryStream();
    //     await requireConfirmedInputsTlv.SerializeAsync(stream);
    //     stream.Position = 0;
    //     var deserializedTlv = RequireConfirmedInputsTlv.FromTlv(await Tlv.DeserializeAsync(stream));
    //
    //     // Assert
    //     Assert.NotNull(deserializedTlv);
    // }
    //
    // [Fact]
    // public async Task NextFundingTlv_SerializationAndDeserialization_WorksCorrectly()
    // {
    //     // Arrange
    //     var nextFundingTxId = new byte[32];
    //     var nextFundingTlv = new NextFundingTlv(nextFundingTxId);
    //
    //     // Act
    //     using var stream = new MemoryStream();
    //     await nextFundingTlv.SerializeAsync(stream);
    //     stream.Position = 0;
    //     var deserializedTlv = NextFundingTlv.FromTlv(await Tlv.DeserializeAsync(stream));
    //
    //     // Assert
    //     Assert.Equal(nextFundingTlv.NextFundingTxId, deserializedTlv.NextFundingTxId);
    // }
    //
    // [Fact]
    // public async Task ShortChannelIdTlv_SerializationAndDeserialization_WorksCorrectly()
    // {
    //     // Arrange
    //     var shortChannelId = new ShortChannelId(new byte[8]);
    //     var shortChannelIdTlv = new ShortChannelIdTlv(shortChannelId);
    //
    //     // Act
    //     using var stream = new MemoryStream();
    //     await shortChannelIdTlv.SerializeAsync(stream);
    //     stream.Position = 0;
    //     var deserializedTlv = ShortChannelIdTlv.FromTlv(await Tlv.DeserializeAsync(stream));
    //
    //     // Assert
    //     Assert.Equal(shortChannelIdTlv.ShortChannelId, deserializedTlv.ShortChannelId);
    // }
    //
}