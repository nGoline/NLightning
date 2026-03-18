namespace NLightning.Bolt11.Tests.Models.TaggedFields;

using Bolt11.Models.TaggedFields;
using Domain.Node;
using Domain.Utils;
using Enums;

public class FeaturesTaggedFieldTests
{
    [Theory]
    [InlineData(new byte[] { 9, 15 }, 3)]
    [InlineData(new byte[] { 8, 14, 48 }, 10)]
    [InlineData(new byte[] { 8, 14, 99 }, 20)]
    public void Constructor_FromValue_SetsPropertiesCorrectly(byte[] featureBits, short expectedLength)
    {
        // Arrange
        var features = FeatureSet.DeserializeFromBytes([0x00]);
        foreach (var featureBit in featureBits)
        {
            features.SetFeature(featureBit, true);
        }

        // Act
        var taggedField = new FeaturesTaggedField(features);

        // Assert
        Assert.Equal(TaggedFieldTypes.Features, taggedField.Type);
        Assert.True(features.IsCompatible(taggedField.Value, out var _));
        Assert.Equal(expectedLength, taggedField.Length);
    }

    [Theory]
    [InlineData(new byte[] { 9, 15 }, new byte[] { 0x82, 0x00 })]
    [InlineData(new byte[] { 8, 14, 48 }, new byte[] { 0x40, 0x00, 0x00, 0x00, 0x10, 0x40, 0x00 })]
    [InlineData(new byte[] { 8, 14, 99 },
                new byte[] { 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x10, 0x00 })]
    public void WriteToBitWriter_WritesCorrectData(byte[] featureBits, byte[] expectedData)
    {
        // Arrange
        var features = FeatureSet.DeserializeFromBytes([0x00]);
        foreach (var featureBit in featureBits)
        {
            features.SetFeature(featureBit, true);
        }

        var taggedField = new FeaturesTaggedField(features);
        var bitWriter = new BitWriter(taggedField.Length * 5);

        // Act
        taggedField.WriteToBitWriter(bitWriter);

        // Assert
        var result = bitWriter.ToArray();

        Assert.Equal(expectedData, result);
    }

    [Theory]
    [InlineData(new byte[] { 9, 15 }, 3, new byte[] { 0x82, 0x00 })]
    [InlineData(new byte[] { 8, 14, 48 }, 10, new byte[] { 0x40, 0x00, 0x00, 0x00, 0x10, 0x40, 0x00 })]
    [InlineData(new byte[] { 8, 14, 99 }, 20,
                new byte[] { 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x10, 0x00 })]
    public void FromBitReader_CreatesCorrectlyFromBitReader(byte[] featureBits, short bitLength, byte[] bytes)
    {
        // Arrange
        var bitReader = new BitReader(bytes);

        // Act
        var taggedField = FeaturesTaggedField.FromBitReader(bitReader, bitLength);

        // Assert
        foreach (var featureBit in featureBits)
        {
            Assert.True(taggedField.Value.IsFeatureSet(featureBit, false));
        }
    }

    [Fact]
    public void FromBitReader_ThrowsArgumentException_ForInvalidLength()
    {
        // Arrange
        var buffer = new byte[50];
        var bitReader = new BitReader(buffer);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => FeaturesTaggedField.FromBitReader(bitReader, 0));
    }
}