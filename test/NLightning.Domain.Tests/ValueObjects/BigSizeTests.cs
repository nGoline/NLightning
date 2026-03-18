namespace NLightning.Domain.Tests.ValueObjects;

using Domain.Protocol.ValueObjects;

public class BigSizeTests
{
    [Theory]
    [InlineData(ulong.MaxValue, typeof(long))]
    [InlineData(ulong.MaxValue, typeof(uint))]
    [InlineData(ulong.MaxValue, typeof(int))]
    [InlineData(ulong.MaxValue, typeof(ushort))]
    [InlineData(ulong.MaxValue, typeof(short))]
    [InlineData(ulong.MaxValue, typeof(byte))]
    [InlineData(long.MaxValue, typeof(uint))]
    [InlineData(long.MaxValue, typeof(int))]
    [InlineData(long.MaxValue, typeof(ushort))]
    [InlineData(long.MaxValue, typeof(short))]
    [InlineData(long.MaxValue, typeof(byte))]
    [InlineData(uint.MaxValue, typeof(int))]
    [InlineData(uint.MaxValue, typeof(ushort))]
    [InlineData(uint.MaxValue, typeof(short))]
    [InlineData(uint.MaxValue, typeof(byte))]
    [InlineData(int.MaxValue, typeof(ushort))]
    [InlineData(int.MaxValue, typeof(short))]
    [InlineData(int.MaxValue, typeof(byte))]
    [InlineData(ushort.MaxValue, typeof(short))]
    [InlineData(ushort.MaxValue, typeof(byte))]
    [InlineData(short.MaxValue, typeof(byte))]
    public void Given_ImplicitConversion_When_CastToSmallerSize_Then_ThrowsException(ulong value, Type type)
    {
        // Arrange
        var bigSize = new BigSize(value);

        // Assert
        switch (type)
        {
            case not null when type == typeof(long):
                var longException = Assert.Throws<OverflowException>(() => (long)bigSize);
                Assert.Equal($"Cannot convert {bigSize.Value} to long because it's too large.", longException.Message);
                break;
            case not null when type == typeof(uint):
                var uintException = Assert.Throws<OverflowException>(() => (uint)bigSize);
                Assert.Equal($"Cannot convert {bigSize.Value} to uint because it's too large.", uintException.Message);
                break;
            case not null when type == typeof(int):
                var intException = Assert.Throws<OverflowException>(() => (int)bigSize);
                Assert.Equal($"Cannot convert {bigSize.Value} to int because it's too large.", intException.Message);
                break;
            case not null when type == typeof(ushort):
                var ushortException = Assert.Throws<OverflowException>(() => (ushort)bigSize);
                Assert.Equal($"Cannot convert {bigSize.Value} to ushort because it's too large.",
                             ushortException.Message);
                break;
            case not null when type == typeof(short):
                var shortException = Assert.Throws<OverflowException>(() => (short)bigSize);
                Assert.Equal($"Cannot convert {bigSize.Value} to short because it's too large.",
                             shortException.Message);
                break;
            case not null when type == typeof(byte):
                var byteException = Assert.Throws<OverflowException>(() => (byte)bigSize);
                Assert.Equal($"Cannot convert {bigSize.Value} to byte because it's too large.", byteException.Message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    [Theory]
    [InlineData(byte.MaxValue, typeof(byte))]
    [InlineData(byte.MaxValue, typeof(short))]
    [InlineData(byte.MaxValue, typeof(ushort))]
    [InlineData(byte.MaxValue, typeof(int))]
    [InlineData(byte.MaxValue, typeof(uint))]
    [InlineData(byte.MaxValue, typeof(long))]
    [InlineData(byte.MaxValue, typeof(ulong))]
    [InlineData(short.MaxValue, typeof(short))]
    [InlineData(short.MaxValue, typeof(ushort))]
    [InlineData(short.MaxValue, typeof(int))]
    [InlineData(short.MaxValue, typeof(uint))]
    [InlineData(short.MaxValue, typeof(long))]
    [InlineData(short.MaxValue, typeof(ulong))]
    [InlineData(ushort.MaxValue, typeof(ushort))]
    [InlineData(ushort.MaxValue, typeof(int))]
    [InlineData(ushort.MaxValue, typeof(uint))]
    [InlineData(ushort.MaxValue, typeof(long))]
    [InlineData(ushort.MaxValue, typeof(ulong))]
    [InlineData(int.MaxValue, typeof(int))]
    [InlineData(int.MaxValue, typeof(uint))]
    [InlineData(int.MaxValue, typeof(long))]
    [InlineData(int.MaxValue, typeof(ulong))]
    [InlineData(uint.MaxValue, typeof(uint))]
    [InlineData(uint.MaxValue, typeof(long))]
    [InlineData(uint.MaxValue, typeof(ulong))]
    [InlineData(long.MaxValue, typeof(long))]
    [InlineData(long.MaxValue, typeof(ulong))]
    [InlineData(ulong.MaxValue, typeof(ulong))]
    public void Given_ImplicitConversion_When_CastToBiggerSize_Then_CastIsValid(ulong value, Type type)
    {
        // Arrange
        var bigSize = new BigSize(value);

        // Assert
        switch (type)
        {
            case not null when type == typeof(ulong):
                Assert.Equal(value, (ulong)bigSize);
                break;
            case not null when type == typeof(long):
                Assert.Equal((long)value, (long)bigSize);
                break;
            case not null when type == typeof(uint):
                Assert.Equal((uint)value, (uint)bigSize);
                break;
            case not null when type == typeof(int):
                Assert.Equal((int)value, (int)bigSize);
                break;
            case not null when type == typeof(ushort):
                Assert.Equal((ushort)value, (ushort)bigSize);
                break;
            case not null when type == typeof(short):
                Assert.Equal((short)value, (short)bigSize);
                break;
            case not null when type == typeof(byte):
                Assert.Equal((byte)value, (byte)bigSize);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 1, true)]
    public void Given_BigSize_When_CompareEqual_Then_ResultIsKnown(ulong value1, ulong value2, bool expected)
    {
        // Arrange
        var bigSize1 = new BigSize(value1);
        var bigSize2 = new BigSize(value2);

        // Act
        var result = bigSize1 == bigSize2;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, true)]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, false)]
    public void Given_BigSize_When_CompareNotEqual_Then_ResultIsKnown(ulong value1, ulong value2, bool expected)
    {
        // Arrange
        var bigSize1 = new BigSize(value1);
        var bigSize2 = new BigSize(value2);

        // Act
        var result = bigSize1 != bigSize2;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, true)]
    [InlineData(1, 0, false)]
    [InlineData(1, 1, false)]
    public void Given_BigSize_When_CompareLessThan_Then_ResultIsKnown(ulong value1, ulong value2, bool expected)
    {
        // Arrange
        var bigSize1 = new BigSize(value1);
        var bigSize2 = new BigSize(value2);

        // Act
        var result = bigSize1 < bigSize2;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, true)]
    [InlineData(1, 0, false)]
    [InlineData(1, 1, true)]
    public void Given_BigSize_When_CompareLessThanOrEqual_Then_ResultIsKnown(ulong value1, ulong value2, bool expected)
    {
        // Arrange
        var bigSize1 = new BigSize(value1);
        var bigSize2 = new BigSize(value2);

        // Act
        var result = bigSize1 <= bigSize2;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, false)]
    public void Given_BigSize_When_CompareGreaterThan_Then_ResultIsKnown(ulong value1, ulong value2, bool expected)
    {
        // Arrange
        var bigSize1 = new BigSize(value1);
        var bigSize2 = new BigSize(value2);

        // Act
        var result = bigSize1 > bigSize2;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, true)]
    public void Given_BigSize_When_CompareGreaterThanOrEqual_Then_ResultIsKnown(
        ulong value1, ulong value2, bool expected)
    {
        // Arrange
        var bigSize1 = new BigSize(value1);
        var bigSize2 = new BigSize(value2);

        // Act
        var result = bigSize1 >= bigSize2;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Given_BigSize_When_CompareEqualWithObject_Then_ResultIsKnown()
    {
        // Arrange
        var bigSize = new BigSize(0);
        var obj = new object();

        // Act
        var result = bigSize.Equals(obj);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Given_BigSize_When_CompareEqualWithNull_Then_ResultIsKnown()
    {
        // Arrange
        var bigSize = new BigSize(0);

        // Act
        var result = bigSize.Equals(null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Given_BigSize_When_CompareEqualWithBigSize_Then_ResultIsKnown()
    {
        // Arrange
        var bigSize = new BigSize(0);
        var bigSize2 = new BigSize(0);

        // Act
        var result = bigSize.Equals(bigSize2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Given_BigSize_When_GetHashCode_Then_ResultIsKnown()
    {
        // Arrange
        var bigSize = new BigSize(0);

        // Act
        var result = bigSize.GetHashCode();

        // Assert
        Assert.Equal(0.GetHashCode(), result);
    }
}