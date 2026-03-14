namespace NLightning.Infrastructure.Serialization.Tests.Node;

using Converters;
using Domain.Enums;
using Domain.Node;
using Serialization.Node;

public class FeatureSetSerializerTests
{
    private readonly FeatureSetSerializer _featureSetSerializer;

    public FeatureSetSerializerTests()
    {
        _featureSetSerializer = new FeatureSetSerializer();
    }

    #region Serialization

    [Theory]
    [InlineData(Feature.OptionZeroconf, false, 7)]
    [InlineData(Feature.OptionZeroconf, true, 7)]
    [InlineData(Feature.OptionScidAlias, false, 6)]
    [InlineData(Feature.OptionScidAlias, true, 6)]
    [InlineData(Feature.OptionOnionMessages, false, 5)]
    [InlineData(Feature.OptionOnionMessages, true, 5)]
    [InlineData(Feature.OptionDualFund, false, 4)]
    [InlineData(Feature.OptionDualFund, true, 4)]
    [InlineData(Feature.OptionAnchors, false, 3)]
    [InlineData(Feature.OptionAnchors, true, 3)]
    [InlineData(Feature.OptionStaticRemoteKey, false, 2)]
    [InlineData(Feature.OptionStaticRemoteKey, true, 2)]
    [InlineData(Feature.GossipQueries, false, 1)]
    [InlineData(Feature.GossipQueries, true, 1)]
    public async Task Given_Features_When_Serialize_Then_BytesAreTrimmed(
        Feature feature, bool isCompulsory, int expectedLength)
    {
        // Arrange
        var features = new FeatureSet();
        features.SetFeature(feature, isCompulsory);
        // Clean default features
        features.SetFeature(Feature.VarOnionOptin, false, false);

        using var stream = new MemoryStream();

        // Act
        await _featureSetSerializer.SerializeAsync(features, stream);
        var bytes = stream.ToArray();
        var length = EndianBitConverter.ToUInt16BigEndian(bytes[..2]);

        // Assert
        Assert.Equal(expectedLength, length);
    }

    [Theory]
    [InlineData(Feature.OptionZeroconf, false, 7)]
    [InlineData(Feature.OptionZeroconf, true, 7)]
    [InlineData(Feature.OptionScidAlias, false, 6)]
    [InlineData(Feature.OptionScidAlias, true, 6)]
    [InlineData(Feature.OptionOnionMessages, false, 5)]
    [InlineData(Feature.OptionOnionMessages, true, 5)]
    [InlineData(Feature.OptionDualFund, false, 4)]
    [InlineData(Feature.OptionDualFund, true, 4)]
    [InlineData(Feature.OptionAnchors, false, 3)]
    [InlineData(Feature.OptionAnchors, true, 3)]
    [InlineData(Feature.OptionStaticRemoteKey, false, 2)]
    [InlineData(Feature.OptionStaticRemoteKey, true, 2)]
    [InlineData(Feature.GossipQueries, false, 1)]
    [InlineData(Feature.GossipQueries, true, 1)]
    public async Task Given_Features_When_SerializeWithoutLength_Then_LengthIsKnown(
        Feature feature, bool isCompulsory, int expectedLength)
    {
        // Arrange
        var features = new FeatureSet();
        features.SetFeature(feature, isCompulsory);
        // Clean default features
        features.SetFeature(Feature.VarOnionOptin, false, false);

        using var stream = new MemoryStream();

        // Act
        await _featureSetSerializer.SerializeAsync(features, stream, includeLength: false);
        var bytes = stream.ToArray();

        // Assert
        Assert.Equal(expectedLength, bytes.Length);
    }

    [Theory]
    [InlineData(Feature.OptionZeroconf, false, new byte[] { 8, 128, 0, 0, 0, 0, 0 })]
    [InlineData(Feature.OptionZeroconf, true, new byte[] { 4, 64, 0, 0, 0, 0, 0 })]
    [InlineData(Feature.OptionScidAlias, false, new byte[] { 128, 0, 0, 0, 0, 0 })]
    [InlineData(Feature.OptionScidAlias, true, new byte[] { 64, 0, 0, 0, 0, 0 })]
    [InlineData(Feature.OptionOnionMessages, false, new byte[] { 128, 0, 0, 0, 0 })]
    [InlineData(Feature.OptionOnionMessages, true, new byte[] { 64, 0, 0, 0, 0 })]
    [InlineData(Feature.OptionDualFund, false, new byte[] { 32, 0, 0, 0 })]
    [InlineData(Feature.OptionDualFund, true, new byte[] { 16, 0, 0, 0 })]
    [InlineData(Feature.OptionAnchors, false, new byte[] { 128, 32, 0 })]
    [InlineData(Feature.OptionAnchors, true, new byte[] { 64, 16, 0 })]
    [InlineData(Feature.OptionStaticRemoteKey, false, new byte[] { 32, 0 })]
    [InlineData(Feature.OptionStaticRemoteKey, true, new byte[] { 16, 0 })]
    [InlineData(Feature.GossipQueries, false, new byte[] { 128 })]
    [InlineData(Feature.GossipQueries, true, new byte[] { 64 })]
    public async Task Given_Features_When_Serialize_Then_BytesAreKnown(Feature feature, bool isCompulsory,
                                                                       byte[] expected)
    {
        // Arrange
        var features = new FeatureSet();
        features.SetFeature(feature, isCompulsory);
        // Clean default features
        features.SetFeature(Feature.VarOnionOptin, false, false);

        using var stream = new MemoryStream();

        // Act
        await _featureSetSerializer.SerializeAsync(features, stream);
        var bytes = stream.ToArray();

        // Assert
        Assert.Equal(expected, bytes[2..]);
    }

    [Fact]
    public async Task Given_Features_When_SerializeWithoutLength_Then_BytesAreKnown()
    {
        // Arrange
        var features = new FeatureSet();
        // Sets bit 0
        features.SetFeature(Feature.OptionDataLossProtect, true);
        // Clean default features
        features.SetFeature(Feature.VarOnionOptin, false, false);

        using var stream = new MemoryStream();

        // Act
        await _featureSetSerializer.SerializeAsync(features, stream, includeLength: false);
        var bytes = stream.ToArray();

        // Assert
        Assert.Equal([1], bytes);
    }

    [Fact]
    public async Task Given_Features_When_SerializeAsGlobal_Then_NoFeaturesGreaterThan13ArePresent()
    {
        // Arrange
        var features = new FeatureSet();
        // Sets bit 0
        features.SetFeature(Feature.OptionSupportLargeChannel, true);
        // Clean default features
        features.SetFeature(Feature.VarOnionOptin, false, false);

        using var stream = new MemoryStream();

        // Act
        await _featureSetSerializer.SerializeAsync(features, stream, true);
        var bytes = stream.ToArray();

        // Assert
        Assert.Equal(2, bytes.Length);
    }

    #endregion

    #region Deserialization

    [Theory]
    [InlineData(new byte[] { 0, 7, 8, 128, 0, 0, 0, 0, 0 }, false, Feature.OptionZeroconf)]
    [InlineData(new byte[] { 0, 7, 4, 64, 0, 0, 0, 0, 0 }, true, Feature.OptionZeroconf)]
    [InlineData(new byte[] { 0, 6, 128, 0, 0, 0, 0, 0 }, false, Feature.OptionScidAlias)]
    [InlineData(new byte[] { 0, 6, 64, 0, 0, 0, 0, 0 }, true, Feature.OptionScidAlias)]
    [InlineData(new byte[] { 0, 5, 128, 0, 0, 0, 0 }, false, Feature.OptionOnionMessages)]
    [InlineData(new byte[] { 0, 5, 64, 0, 0, 0, 0 }, true, Feature.OptionOnionMessages)]
    [InlineData(new byte[] { 0, 4, 32, 0, 0, 0 }, false, Feature.OptionDualFund)]
    [InlineData(new byte[] { 0, 4, 16, 0, 0, 0 }, true, Feature.OptionDualFund)]
    [InlineData(new byte[] { 0, 3, 128, 32, 0 }, false, Feature.OptionAnchors)]
    [InlineData(new byte[] { 0, 3, 64, 16, 0 }, true, Feature.OptionAnchors)]
    [InlineData(new byte[] { 0, 2, 32, 0 }, false, Feature.OptionStaticRemoteKey)]
    [InlineData(new byte[] { 0, 2, 16, 0 }, true, Feature.OptionStaticRemoteKey)]
    [InlineData(new byte[] { 0, 1, 128 }, false, Feature.GossipQueries)]
    [InlineData(new byte[] { 0, 1, 64 }, true, Feature.GossipQueries)]
    public async Task Given_Buffer_When_Deserialize_Then_FeatureIsSet(byte[] buffer, bool isCompulsory,
                                                                      Feature expected)
    {
        // Arrange
        using var stream = new MemoryStream(buffer);

        // Act
        var features = await _featureSetSerializer.DeserializeAsync(stream);

        // Assert
        Assert.True(features.IsFeatureSet(expected, isCompulsory));
        Assert.False(features.IsFeatureSet(expected, !isCompulsory));
    }

    [Fact]
    public async Task Given_Buffer_When_DeserializeWithoutLength_Then_FeatureIsSet()
    {
        // Arrange
        using var stream = new MemoryStream([0, 0, 0, 0, 0, 0, 0, 1]);

        // Act
        var features = await _featureSetSerializer.DeserializeAsync(stream, false);

        // Assert
        Assert.True(features.IsFeatureSet(Feature.OptionDataLossProtect, true));
        Assert.False(features.IsFeatureSet(Feature.OptionDataLossProtect, false));
    }

    [Fact]
    public async Task Given_WrongLengthBuffer_When_Deserialize_Then_FeatureIsNotSet()
    {
        // Arrange
        using var stream = new MemoryStream([0, 6, 8, 128, 0, 0, 0, 0, 0]);

        // Act
        var features = await _featureSetSerializer.DeserializeAsync(stream);

        // Assert
        Assert.False(features.IsFeatureSet(Feature.OptionZeroconf, false));
        Assert.False(features.IsFeatureSet(Feature.OptionZeroconf, true));
    }

    #endregion
}