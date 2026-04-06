namespace NLightning.Bolt11.Tests.Models.TaggedFields;

using Bolt11.Models.TaggedFields;
using Domain.Utils;
using Enums;

public class ExpiryTimeTaggedFieldTests
{
    [Theory]
    [InlineData(15, 1)]
    [InlineData(30, 1)]
    [InlineData(60, 2)]
    [InlineData(3600, 3)]
    [InlineData(7200, 3)]
    public void Constructor_FromValue_SetsPropertiesCorrectly(int value, short expectedLength)
    {
        // Act
        var taggedField = new ExpiryTimeTaggedField(value);

        // Assert
        Assert.Equal(TaggedFieldTypes.ExpiryTime, taggedField.Type);
        Assert.Equal(value, taggedField.Value);
        Assert.Equal(expectedLength, taggedField.Length);
    }

    [Theory]
    [InlineData(15, new byte[] { 0x78 })]
    [InlineData(30, new byte[] { 0xF0 })]
    [InlineData(60, new byte[] { 0x0F, 0x00 })]
    [InlineData(3600, new byte[] { 0x1C, 0x20 })]
    [InlineData(7200, new byte[] { 0x38, 0x40 })]
    public void WriteToBitWriter_WritesCorrectData(int value, byte[] expectedBytes)
    {
        // Arrange
        var taggedField = new ExpiryTimeTaggedField(value);
        var bitWriter = new BitWriter(taggedField.Length * 5);

        // Act
        taggedField.WriteToBitWriter(bitWriter);

        // Assert
        var result = bitWriter.ToArray();

        Assert.Equal(expectedBytes, result);
    }

    [Theory]
    [InlineData(15, 1, new byte[] { 0x78 })]
    [InlineData(30, 1, new byte[] { 0xF0 })]
    [InlineData(60, 2, new byte[] { 0x0F, 0x00 })]
    [InlineData(3600, 3, new byte[] { 0x1C, 0x20 })]
    [InlineData(7200, 3, new byte[] { 0x38, 0x40 })]
    public void FromBitReader_CreatesCorrectlyFromBitReader(int expectedValue, short bitLength, byte[] bytes)
    {
        // Arrange
        var bitReader = new Domain.Utils.BitReader(bytes);

        // Act
        var taggedField = ExpiryTimeTaggedField.FromBitReader(bitReader, bitLength);

        // Assert
        Assert.Equal(expectedValue, taggedField.Value);
    }

    [Fact]
    public void FromBitReader_ThrowsArgumentException_ForInvalidLength()
    {
        // Arrange
        var buffer = new byte[50];
        var bitReader = new Domain.Utils.BitReader(buffer);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ExpiryTimeTaggedField.FromBitReader(bitReader, 0));
    }

    [Fact]
    public void IsValid_ReturnsTrueForPositiveValue()
    {
        // Arrange
        var taggedField = new ExpiryTimeTaggedField(1);

        // Act & Assert
        Assert.True(taggedField.IsValid());
    }

    [Fact]
    public void IsValid_ReturnsFalseForNonPositiveValue()
    {
        // Arrange
        var taggedField = new ExpiryTimeTaggedField(-1);

        // Act & Assert
        Assert.False(taggedField.IsValid());
    }
}