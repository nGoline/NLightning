namespace NLightning.Domain.Crypto.ValueObjects;

using Constants;
using Utils.Extensions;

public readonly struct Hash : IEquatable<Hash>
{
    private readonly byte[] _value;
    public static Hash Empty => new(new byte[CryptoConstants.Sha256HashLen]);

    public Hash(byte[] value)
    {
        if (value.Length < CryptoConstants.Sha256HashLen)
            throw new ArgumentOutOfRangeException(nameof(value), value.Length,
                                                  $"Hash must have {CryptoConstants.Sha256HashLen} bytes.");

        _value = value;
    }

    public static implicit operator Hash(byte[] bytes) => new(bytes);
    public static implicit operator byte[](Hash hash) => hash._value;

    public static implicit operator ReadOnlyMemory<byte>(Hash hash) => hash._value;
    public static implicit operator ReadOnlySpan<byte>(Hash hash) => hash._value;

    public override string ToString() => Convert.ToHexString(_value).ToLowerInvariant();

    public bool Equals(Hash other)
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

        return obj is Hash other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _value.GetByteArrayHashCode();
    }

    public static bool operator ==(Hash left, Hash right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Hash left, Hash right)
    {
        return !(left == right);
    }
}