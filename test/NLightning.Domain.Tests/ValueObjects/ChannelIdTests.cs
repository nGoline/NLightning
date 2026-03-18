namespace NLightning.Domain.Tests.ValueObjects;

using Domain.Channels.ValueObjects;

public class ChannelIdTests
{
    [Fact]
    public void Given_TwoEqualChannelIds_When_Compared_Then_AreEqual()
    {
        // Given
        var value = new byte[32];
        var channelId1 = new ChannelId(value);
        var channelId2 = new ChannelId(value);

        // When & Then
        Assert.True(channelId1 == channelId2);
        Assert.False(channelId1 != channelId2);
        Assert.True(channelId1.Equals(channelId2));
        Assert.True(channelId2.Equals(channelId1));
    }

    [Fact]
    public void Given_TwoDifferentChannelIds_When_Compared_Then_AreNotEqual()
    {
        // Given
        var channelId1 = new ChannelId(new byte[32]);
        var channelId2 = new ChannelId([
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1
        ]);

        // When & Then
        Assert.False(channelId1 == channelId2);
        Assert.True(channelId1 != channelId2);
        Assert.False(channelId1.Equals(channelId2));
        Assert.False(channelId2.Equals(channelId1));
    }

    [Fact]
    public void Given_InvalidByteArray_When_ChannelIdCreated_Then_ThrowsArgumentException()
    {
        // Given
        var invalidValue = new byte[31];

        // When & Then
        Assert.Throws<ArgumentException>(() => new ChannelId(invalidValue));
    }

    [Fact]
    public void Given_ChannelId_When_ConvertedToByteArray_Then_ReturnsCorrectValue()
    {
        // Given
        var value = new byte[32];
        for (var i = 0; i < value.Length; i++) value[i] = (byte)i;
        var channelId = new ChannelId(value);

        // When
        byte[] byteArray = channelId;

        // Then
        Assert.Equal(value, byteArray);
    }

    [Fact]
    public void Given_ByteArray_When_ConvertedToChannelId_Then_ReturnsCorrectChannelId()
    {
        // Given
        var value = new byte[32];
        for (var i = 0; i < value.Length; i++) value[i] = (byte)i;

        // When
        ChannelId channelId = value;

        // Then
        Assert.Equal(value, (byte[])channelId);
    }

    [Fact]
    public void Given_ZeroProperty_When_Accessed_Then_ReturnsAllZeroChannelId()
    {
        // Given
        var zeroChannelId = ChannelId.Zero;

        // When
        var expectedValue = new byte[32];

        // Then
        Assert.Equal(expectedValue, (byte[])zeroChannelId);
    }

    [Fact]
    public void Given_TwoZeroChannelIds_When_Compared_Then_AreEqual()
    {
        // Given
        var zeroChannelId1 = ChannelId.Zero;
        var zeroChannelId2 = ChannelId.Zero;

        // When & Then
        Assert.True(zeroChannelId1 == zeroChannelId2);
        Assert.False(zeroChannelId1 != zeroChannelId2);
        Assert.True(zeroChannelId1.Equals(zeroChannelId2));
    }

    [Fact]
    public void Given_ByteArray_When_ImplicitlyConvertedToChannelId_Then_ReturnsCorrectChannelId()
    {
        // Given
        var value = new byte[32];
        for (var i = 0; i < value.Length; i++) value[i] = (byte)i;

        // When
        ChannelId channelId = value;

        // Then
        Assert.Equal(value, (byte[])channelId);
    }

    [Fact]
    public void Given_ChannelId_When_ImplicitlyConvertedToByteArray_Then_ReturnsCorrectByteArray()
    {
        // Given
        var value = new byte[32];
        for (var i = 0; i < value.Length; i++) value[i] = (byte)i;
        var channelId = new ChannelId(value);

        // When
        byte[] byteArray = channelId;

        // Then
        Assert.Equal(value, byteArray);
    }
}