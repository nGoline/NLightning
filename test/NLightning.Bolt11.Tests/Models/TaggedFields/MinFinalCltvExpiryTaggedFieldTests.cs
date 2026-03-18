namespace NLightning.Bolt11.Tests.Models.TaggedFields;

using Bolt11.Models.TaggedFields;
using Domain.Utils;
using Enums;

public class MinFinalCltvExpiryTaggedFieldTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(31, 1)]
    [InlineData(32, 2)]
    [InlineData(1023, 2)]
    [InlineData(1024, 3)]
    public void Constructor_FromValue_SetsPropertiesCorrectly(ushort expiry, short expectedLength)
    {
        // Act
        var taggedField = new MinFinalCltvExpiryTaggedField(expiry);

        // Assert
        Assert.Equal(TaggedFieldTypes.MinFinalCltvExpiry, taggedField.Type);
        Assert.Equal(expiry, taggedField.Value);
        Assert.Equal(expectedLength, taggedField.Length);
    }

    [Theory]
    [InlineData(1, new byte[] { 0x08 })]
    [InlineData(31, new byte[] { 0xF8 })]
    [InlineData(32, new byte[] { 0x08, 0x00 })]
    [InlineData(1023, new byte[] { 0xFF, 0xC0 })]
    [InlineData(1024, new byte[] { 0x08, 0x00 })]
    public void WriteToBitWriter_WritesCorrectData(ushort expiry, byte[] expectedData)
    {
        // Arrange
        var taggedField = new MinFinalCltvExpiryTaggedField(expiry);
        var bitWriter = new BitWriter(taggedField.Length * 5);

        // Act
        taggedField.WriteToBitWriter(bitWriter);

        // Assert
        var result = bitWriter.ToArray();

        Assert.Equal(expectedData, result);
    }

    [Theory]
    [InlineData(new byte[] { 1 }, 2, new byte[] { 1, 0 })]
    [InlineData(new byte[] { 1, 2 }, 4, new byte[] { 1, 2, 0 })]
    [InlineData(new byte[] { 1, 2, 3 }, 5, new byte[] { 1, 2, 3, 0 })]
    [InlineData(new byte[] { 1, 2, 3, 4 }, 7, new byte[] { 1, 2, 3, 4, 0 })]
    [InlineData(new byte[] { 1, 2, 3, 4, 5 }, 8, new byte[] { 1, 2, 3, 4, 5 })]
    public void FromBitReader_CreatesCorrectlyFromBitReader(byte[] expectedMetadata, short bitLength, byte[] bytes)
    {
        // Arrange
        var bitReader = new BitReader(bytes);

        // Act
        var taggedField = MetadataTaggedField.FromBitReader(bitReader, bitLength);

        // Assert
        Assert.Equal(expectedMetadata, taggedField.Value);
    }

    [Fact]
    public void FromBitReader_ThrowsArgumentException_ForInvalidLength()
    {
        // Arrange
        var buffer = new byte[50];
        var bitReader = new BitReader(buffer);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => MetadataTaggedField.FromBitReader(bitReader, 0));
    }
}