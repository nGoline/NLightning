namespace NLightning.Bolt11.Tests.Models.TaggedFields;

using Bolt11.Models.TaggedFields;
using Domain.Utils;
using Enums;

public class DescriptionTaggedFieldTests
{
    [Theory]
    [InlineData("Test Description", 26)]
    [InlineData("Test Description1", 28)]
    [InlineData("Test Description12", 29)]
    [InlineData("Test Description123", 31)]
    [InlineData("Test Description1234", 32)]
    public void Constructor_FromValue_SetsPropertiesCorrectly(string value, short expectedLength)
    {
        // Act
        var taggedField = new DescriptionTaggedField(value);

        // Assert
        Assert.Equal(TaggedFieldTypes.Description, taggedField.Type);
        Assert.Equal(value, taggedField.Value);
        Assert.Equal(expectedLength, taggedField.Length);
    }

    [Theory]
    [InlineData("Test Description",
                new byte[]
                {
                    0x54, 0x65, 0x73, 0x74, 0x20, 0x44, 0x65, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x69, 0x6F, 0x6E, 0x00
                })]
    [InlineData("Test Description1",
                new byte[]
                {
                    0x54, 0x65, 0x73, 0x74, 0x20, 0x44, 0x65, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x69, 0x6F, 0x6E,
                    0x31, 0x00
                })]
    [InlineData("Test Description12",
                new byte[]
                {
                    0x54, 0x65, 0x73, 0x74, 0x20, 0x44, 0x65, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x69, 0x6F, 0x6E,
                    0x31, 0x32, 0x00
                })]
    [InlineData("Test Description123",
                new byte[]
                {
                    0x54, 0x65, 0x73, 0x74, 0x20, 0x44, 0x65, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x69, 0x6F, 0x6E,
                    0x31, 0x32, 0x33, 0x00
                })]
    [InlineData("Test Description1234",
                new byte[]
                {
                    0x54, 0x65, 0x73, 0x74, 0x20, 0x44, 0x65, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x69, 0x6F, 0x6E,
                    0x31, 0x32, 0x33, 0x34
                })]
    public void WriteToBitWriter_WritesCorrectData(string value, byte[] expectedBytes)
    {
        // Arrange
        var taggedField = new DescriptionTaggedField(value);
        var bitWriter = new BitWriter(taggedField.Length * 5);

        // Act
        taggedField.WriteToBitWriter(bitWriter);

        // Assert
        var result = bitWriter.ToArray();

        Assert.Equal(expectedBytes, result);
    }

    [Theory]
    [InlineData("Test Description", 26,
                new byte[]
                {
                    0x54, 0x65, 0x73, 0x74, 0x20, 0x44, 0x65, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x69, 0x6F, 0x6E, 0x00
                })]
    [InlineData("Test Description1", 28,
                new byte[]
                {
                    0x54, 0x65, 0x73, 0x74, 0x20, 0x44, 0x65, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x69, 0x6F, 0x6E,
                    0x31, 0x00
                })]
    [InlineData("Test Description12", 29,
                new byte[]
                {
                    0x54, 0x65, 0x73, 0x74, 0x20, 0x44, 0x65, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x69, 0x6F, 0x6E,
                    0x31, 0x32, 0x00
                })]
    [InlineData("Test Description123", 31,
                new byte[]
                {
                    0x54, 0x65, 0x73, 0x74, 0x20, 0x44, 0x65, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x69, 0x6F, 0x6E,
                    0x31, 0x32, 0x33, 0x00
                })]
    [InlineData("Test Description1234", 32,
                new byte[]
                {
                    0x54, 0x65, 0x73, 0x74, 0x20, 0x44, 0x65, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x69, 0x6F, 0x6E,
                    0x31, 0x32, 0x33, 0x34
                })]
    public void FromBitReader_CreatesCorrectlyFromBitReader(string expectedValue, short bitLength, byte[] bytes)
    {
        // Arrange
        var bitReader = new BitReader(bytes);

        // Act
        var taggedField = DescriptionTaggedField.FromBitReader(bitReader, bitLength);

        // Assert
        Assert.Equal(expectedValue, taggedField.Value);
    }

    [Fact]
    public void FromBitReader_ThrowsArgumentException_ForInvalidLength()
    {
        // Arrange
        var buffer = new byte[50];
        var bitReader = new BitReader(buffer);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => DescriptionTaggedField.FromBitReader(bitReader, -1));
    }

    [Fact]
    public void IsValid_ReturnsTrueForNonEmptyValue()
    {
        // Arrange
        var taggedField = new DescriptionTaggedField("Test");

        // Act & Assert
        Assert.True(taggedField.IsValid());
    }
}