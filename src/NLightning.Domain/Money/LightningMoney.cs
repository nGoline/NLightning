using System.Globalization;

namespace NLightning.Domain.Money;

using Enums;

public class LightningMoney
{
    // For decimal.TryParse. None of the NumberStyles' composed values is useful for bitcoin style
    private const NumberStyles BitcoinStyle = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
                                                                             | NumberStyles.AllowDecimalPoint;

    private ulong _milliSatoshi;

    public const ulong Coin = 100 * 1000 * 1000 * 1000UL;
    public const ulong Cent = Coin / 100;
    public const ulong Nano = Cent / 100;

    public ulong MilliSatoshi
    {
        get => _milliSatoshi;
        set
        {
            _milliSatoshi = value;
        }
    }

    public long Satoshi
    {
        // Should round up to the nearest Satoshi
        get => checked((long)Math.Round(_milliSatoshi / 1_000D, MidpointRounding.ToNegativeInfinity));
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Satoshi value cannot be negative");

            checked
            {
                _milliSatoshi = (ulong)(value * 1000);
            }
        }
    }

    public static LightningMoney Zero => 0UL;
    public bool IsZero => _milliSatoshi == 0;

    #region Constructors

    public LightningMoney(ulong milliSatoshi)
    {
        MilliSatoshi = milliSatoshi;
    }

    public LightningMoney(decimal amount, LightningMoneyUnit unit)
    {
        // sanity check. Only valid units are allowed
        CheckLightningMoneyUnit(unit, nameof(unit));
        checked
        {
            var milliSats = amount * (long)unit;
            MilliSatoshi = (ulong)milliSats;
        }
    }

    public LightningMoney(long amount, LightningMoneyUnit unit)
    {
        // Sanity check. Only valid units are allowed
        CheckLightningMoneyUnit(unit, nameof(unit));
        checked
        {
            var milliSats = amount * (long)unit;
            MilliSatoshi = (ulong)milliSats;
        }
    }

    public LightningMoney(ulong amount, LightningMoneyUnit unit)
    {
        // Sanity check. Only valid units are allowed
        CheckLightningMoneyUnit(unit, nameof(unit));
        checked
        {
            var milliSats = amount * (ulong)unit;
            MilliSatoshi = milliSats;
        }
    }

    #endregion

    #region Parsers

    /// <summary>
    /// Parse a bitcoin amount (Culture Invariant)
    /// </summary>
    /// <param name="bitcoin"></param>
    /// <param name="nRet"></param>
    /// <returns></returns>
    public static bool TryParse(string bitcoin, out LightningMoney? nRet)
    {
        nRet = null;

        if (!decimal.TryParse(bitcoin, BitcoinStyle, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        try
        {
            nRet = new LightningMoney(value, LightningMoneyUnit.Btc);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    /// <summary>
    /// Parse a bitcoin amount (Culture Invariant)
    /// </summary>
    /// <param name="bitcoin"></param>
    /// <returns></returns>
    public static LightningMoney? Parse(string bitcoin)
    {
        if (TryParse(bitcoin, out var result))
        {
            return result;
        }

        throw new FormatException("Impossible to parse the string in a bitcoin amount");
    }

    #endregion

    #region Conversions

    /// <summary>
    /// Convert Money to decimal (same as ToDecimal)
    /// </summary>
    /// <param name="unit"></param>
    /// <returns></returns>
    public decimal ToUnit(LightningMoneyUnit unit)
    {
        CheckLightningMoneyUnit(unit, nameof(unit));
        // overflow safe because (long / int) always fit in decimal
        // decimal operations are checked by default
        return (decimal)MilliSatoshi / (ulong)unit;
    }

    /// <summary>
    /// Convert Money to decimal (same as ToUnit)
    /// </summary>
    /// <param name="unit"></param>
    /// <returns></returns>
    public decimal ToDecimal(LightningMoneyUnit unit)
    {
        return ToUnit(unit);
    }

    #endregion

    /// <summary>
    /// Split the Money in parts without loss
    /// </summary>
    /// <param name="parts">The number of parts (must be more than 0)</param>
    /// <returns>The split money</returns>
    public IEnumerable<LightningMoney> Split(int parts)
    {
        if (parts <= 0)
            throw new ArgumentOutOfRangeException(nameof(parts), "Parts should be more than 0");

        var result = DivRem(_milliSatoshi, (ulong)parts, out var remain);

        for (var i = 0; i < parts; i++)
        {
            yield return MilliSatoshis(result + (remain > 0 ? 1UL : 0UL));
            if (remain > 0)
            {
                remain--;
            }
        }
    }

    #region Static Converters

    public static LightningMoney FromUnit(decimal amount, LightningMoneyUnit unit)
    {
        return new LightningMoney(amount, unit);
    }

    public static LightningMoney Coins(decimal coins)
    {
        // overflow safe.
        // decimal operations are checked by default
        return new LightningMoney(coins * Coin, LightningMoneyUnit.MilliSatoshi);
    }

    public static LightningMoney Bits(decimal bits)
    {
        // overflow safe.
        // decimal operations are checked by default
        return new LightningMoney(bits * Cent, LightningMoneyUnit.MilliSatoshi);
    }

    public static LightningMoney Cents(decimal cents)
    {
        // overflow safe.
        // decimal operations are checked by default
        return new LightningMoney(cents * Cent, LightningMoneyUnit.MilliSatoshi);
    }

    public static LightningMoney Satoshis(decimal sats)
    {
        return new LightningMoney(sats, LightningMoneyUnit.Satoshi);
    }

    public static LightningMoney Satoshis(ulong sats)
    {
        return new LightningMoney(sats, LightningMoneyUnit.Satoshi);
    }

    public static LightningMoney Satoshis(long sats)
    {
        return new LightningMoney((ulong)sats, LightningMoneyUnit.Satoshi);
    }

    public static LightningMoney MilliSatoshis(ulong milliSats)
    {
        return new LightningMoney(milliSats);
    }

    public static LightningMoney MilliSatoshis(long sats)
    {
        return new LightningMoney((ulong)sats);
    }

    #endregion

    #region IEquatable<Money> Members

    public bool Equals(LightningMoney? other)
    {
        return other is not null && _milliSatoshi.Equals(other._milliSatoshi);
    }

    public int CompareTo(LightningMoney? other)
    {
        return other is null ? 1 : _milliSatoshi.CompareTo(other._milliSatoshi);
    }

    #endregion

    #region IComparable Members

    public int CompareTo(object? obj)
    {
        return obj switch
        {
            null => 1,
            LightningMoney m => _milliSatoshi.CompareTo(m._milliSatoshi),
            _ => _milliSatoshi.CompareTo((ulong)obj)
        };
    }

    #endregion

    #region Unary Operators

    public static LightningMoney operator -(LightningMoney left, LightningMoney right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left._milliSatoshi < right._milliSatoshi)
            throw new ArithmeticException("LightningMoney does not support negative values");

        return new LightningMoney(checked(left._milliSatoshi - right._milliSatoshi));
    }

    public static LightningMoney operator -(LightningMoney _)
    {
        throw new ArithmeticException("LightningMoney does not support unary negation");
    }

    public static LightningMoney operator +(LightningMoney left, LightningMoney right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return new LightningMoney(checked(left._milliSatoshi + right._milliSatoshi));
    }

    public static LightningMoney operator *(ulong left, LightningMoney right)
    {
        ArgumentNullException.ThrowIfNull(right);

        return MilliSatoshis(checked(left * right._milliSatoshi));
    }

    public static LightningMoney operator *(LightningMoney left, ulong right)
    {
        ArgumentNullException.ThrowIfNull(left);

        return MilliSatoshis(checked(left._milliSatoshi * right));
    }

    public static LightningMoney operator *(long left, LightningMoney right)
    {
        ArgumentNullException.ThrowIfNull(right);

        return MilliSatoshis(checked((ulong)left * right._milliSatoshi));
    }

    public static LightningMoney operator *(LightningMoney left, long right)
    {
        ArgumentNullException.ThrowIfNull(left);

        return MilliSatoshis(checked((ulong)right * left._milliSatoshi));
    }

    public static LightningMoney operator *(decimal left, LightningMoney right)
    {
        ArgumentNullException.ThrowIfNull(right);

        return MilliSatoshis((ulong)(left * right._milliSatoshi));
    }

    public static LightningMoney operator *(LightningMoney left, decimal right)
    {
        ArgumentNullException.ThrowIfNull(left);

        return MilliSatoshis((ulong)(right * left._milliSatoshi));
    }

    public static LightningMoney operator /(LightningMoney left, ulong right)
    {
        ArgumentNullException.ThrowIfNull(left);

        return new LightningMoney(left._milliSatoshi / right);
    }

    public static bool operator <(LightningMoney left, LightningMoney right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return left._milliSatoshi < right._milliSatoshi;
    }

    public static bool operator >(LightningMoney left, LightningMoney right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return left._milliSatoshi > right._milliSatoshi;
    }

    public static bool operator <=(LightningMoney left, LightningMoney right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return left._milliSatoshi <= right._milliSatoshi;
    }

    public static bool operator >=(LightningMoney left, LightningMoney right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return left._milliSatoshi >= right._milliSatoshi;
    }

    #endregion

    #region Implicit Operators

    public static implicit operator LightningMoney(long value)
    {
        return new LightningMoney((ulong)value);
    }

    public static implicit operator LightningMoney(ulong value)
    {
        return new LightningMoney(value);
    }

    public static implicit operator LightningMoney(string value)
    {
        return Parse(value) ??
               throw new ArgumentException("Cannot parse value into a valid LightningMoney", nameof(value));
    }

    public static implicit operator long(LightningMoney value)
    {
        return checked((long)value.MilliSatoshi);
    }

    public static implicit operator ulong(LightningMoney value)
    {
        return value.MilliSatoshi;
    }

    #endregion

    #region Equality Operators

    public override bool Equals(object? obj)
    {
        return obj is LightningMoney item && _milliSatoshi.Equals(item._milliSatoshi);
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return _milliSatoshi.GetHashCode();
    }

    public static bool operator ==(LightningMoney? a, LightningMoney? b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a is null || b is null)
            return false;
        return a._milliSatoshi == b._milliSatoshi;
    }

    public static bool operator !=(LightningMoney a, LightningMoney b)
    {
        return !(a == b);
    }

    #endregion

    #region ToString

    /// <summary>
    /// Returns a culture invariant string representation of Bitcoin amount
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return ToString(true);
    }

    /// <summary>
    /// Returns a culture invariant string representation of Bitcoin amount
    /// </summary>
    /// <param name="trimExcessZero">True if trim excess zeros</param>
    /// <returns></returns>
    public string ToString(bool trimExcessZero = true)
    {
        var val = (decimal)_milliSatoshi / (ulong)LightningMoneyUnit.Btc;
        var decPos = trimExcessZero ? 2 : 11;
        var zeros = new string('0', decPos);
        var rest = new string('#', 11 - decPos);
        var fmt = "{0:0" + ("." + zeros + rest) + "}";

        return string.Format(CultureInfo.InvariantCulture, fmt, val);
    }

    #endregion

    /// <summary>
    /// Tell if the amount is almost equal to this instance
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="dust">more or less amount</param>
    /// <returns>true if equals, else false</returns>
    public bool Almost(LightningMoney amount, LightningMoney dust)
    {
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentNullException.ThrowIfNull(dust);

        return amount - this <= dust;
    }

    /// <summary>
    /// Tell if the amount is almost equal to this instance
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="margin">error margin (between 0 and 1)</param>
    /// <returns>true if equals, else false</returns>
    public bool Almost(LightningMoney amount, decimal margin)
    {
        ArgumentNullException.ThrowIfNull(amount);
        if (margin is < 0.0m or > 1.0m)
            throw new ArgumentOutOfRangeException(nameof(margin), "margin should be between 0 and 1");

        var dust = new LightningMoney(MilliSatoshi * margin, LightningMoneyUnit.MilliSatoshi);
        return Almost(amount, dust);
    }

    public static LightningMoney Min(LightningMoney a, LightningMoney b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        return a <= b ? a : b;
    }

    public static LightningMoney Max(LightningMoney a, LightningMoney b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        return a >= b ? a : b;
    }

    #region IMoney Members

    public LightningMoney Add(LightningMoney money)
    {
        return this + money;
    }

    public LightningMoney Sub(LightningMoney money)
    {
        return this - money;
    }

    public LightningMoney Negate()
    {
        throw new ArithmeticException("LightningMoney does not support unary negation");
    }

    #endregion

    private static void CheckLightningMoneyUnit(LightningMoneyUnit value, string paramName)
    {
        var typeOfMoneyUnit = typeof(LightningMoneyUnit);
        if (!Enum.IsDefined(typeOfMoneyUnit, value))
        {
            throw new ArgumentException("Invalid value for LightningMoneyUnit", paramName);
        }
    }

    private static ulong DivRem(ulong a, ulong b, out ulong result)
    {
        result = a % b;
        return a / b;
    }
}