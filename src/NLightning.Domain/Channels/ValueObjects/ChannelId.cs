namespace NLightning.Domain.Channels.ValueObjects;

using Constants;
using Domain.Interfaces;
using Utils.Extensions;

/// <summary>
/// Represents a channel id.
/// </summary>
/// <remarks>
/// The channel id is a unique identifier for a channel.
/// </remarks>
public readonly struct ChannelId : IEquatable<ChannelId>, IValueObject
{
    private readonly byte[] _value;

    public static ChannelId Zero => new byte[ChannelConstants.ChannelIdLength];

    public ChannelId(ReadOnlySpan<byte> value)
    {
        if (value.Length != ChannelConstants.ChannelIdLength)
            throw new ArgumentException($"ChannelId must be {ChannelConstants.ChannelIdLength} bytes", nameof(value));

        _value = value.ToArray();
    }

    #region Overrides

    public override string ToString()
    {
        return Convert.ToHexString(_value).ToLowerInvariant();
    }

    public bool Equals(ChannelId other)
    {
        // Handle null cases first
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (_value is null && other._value is null)
            return true;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (_value is null || other._value is null)
            return false;

        return _value.SequenceEqual(other._value);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        return obj is ChannelId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _value.GetByteArrayHashCode();
    }

    #endregion

    #region Implicit Conversions

    public static implicit operator byte[](ChannelId c) => c._value;
    public static implicit operator ReadOnlyMemory<byte>(ChannelId c) => c._value;
    public static implicit operator ReadOnlySpan<byte>(ChannelId c) => c._value;
    public static implicit operator ChannelId(byte[] value) => new(value);
    public static implicit operator ChannelId(Span<byte> value) => new(value);

    public static bool operator !=(ChannelId left, ChannelId right)
    {
        return !left.Equals(right);
    }

    public static bool operator ==(ChannelId left, ChannelId right)
    {
        return left.Equals(right);
    }

    #endregion
}