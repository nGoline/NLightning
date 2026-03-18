using MessagePack;

namespace NLightning.Node.Tests.Models;

using NLightning.Node.Models;

public class FeeRateCacheDataTests
{
    [Fact]
    public void Given_FeeRateCacheData_When_PropertiesSet_Then_ValuesAreCorrect()
    {
        // Given
        var feeRateCacheData = new FeeRateCacheData
        {
            FeeRate = 1000,
            LastFetchTime = DateTime.UtcNow
        };

        // When & Then
        Assert.Equal(1000UL, feeRateCacheData.FeeRate);
        Assert.True((DateTime.UtcNow - feeRateCacheData.LastFetchTime).TotalSeconds < 1);
    }

    [Fact]
    public void Given_FeeRateCacheData_When_SerializedAndDeserialized_Then_DataIsPreserved()
    {
        // Given
        var originalData = new FeeRateCacheData
        {
            FeeRate = 1500,
            LastFetchTime = DateTime.UtcNow
        };

        // When
        var serializedData =
            MessagePackSerializer.Serialize(originalData, cancellationToken: TestContext.Current.CancellationToken);
        var deserializedData =
            MessagePackSerializer.Deserialize<FeeRateCacheData>(serializedData,
                                                                cancellationToken: TestContext.Current
                                                                   .CancellationToken);

        // Then
        Assert.Equal(originalData.FeeRate, deserializedData.FeeRate);
        Assert.Equal(originalData.LastFetchTime, deserializedData.LastFetchTime);
    }
}