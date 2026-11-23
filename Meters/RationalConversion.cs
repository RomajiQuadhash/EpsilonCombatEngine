using Rationalz;
using System.Numerics;

namespace Meters
{
    /// <summary>
    /// Which representable value to pick when a fraction can't be stored exactly in the target type.
    /// </summary>
    public enum FractionRounding
    {
        /// <summary>
        /// Floating point types get the nearest representable value.
        /// Integer types truncate toward zero, the same as CreateTruncating.
        /// </summary>
        Closest,
        /// <summary>
        /// The largest representable value that is not above the fraction.
        /// </summary>
        AtMost,
        /// <summary>
        /// The smallest representable value that is not below the fraction.
        /// </summary>
        AtLeast
    }
    /// <summary>
    /// The one place that converts between timeline/meter number types and Rational.
    /// Use this instead of Rational.CreateChecked and T.CreateTruncating:
    /// Rationalz reads a double through its decimal digits (1/3 becomes 3333333333333333/10000000000000000)
    /// and drops the whole fractional part when converting back, even into a double.
    /// </summary>
    /// <remarks>Written by Claude at my request</remarks>
    public static class RationalConversion
    {
        /// <summary>
        /// Denominators above this are rounded away. Keeps products of a few fractions inside a long.
        /// </summary>
        public const long DefaultMaxDenominator = 10_000_000;

        /// <summary>
        /// Converts a number into the simplest fraction that converts back into exactly that number.
        /// 3.3333333333333335 becomes 10/3, 0.1 becomes 1/10.
        /// If no fraction with a small enough denominator does that, returns the closest fraction that fits (lossy).
        /// </summary>
        /// <typeparam name="TNum">Any number type. Whole numbers and Rational pass straight through</typeparam>
        /// <param name="value">What to convert. Must be finite</param>
        /// <param name="maxDenominator">Largest denominator the result may have</param>
        /// <returns>The fraction, in lowest terms</returns>
        /// <exception cref="ArgumentException">If value is NaN or infinite</exception>
        /// <exception cref="OverflowException">If the value is too large for a long numerator at this denominator</exception>
        public static Rational<long> ToRational<TNum>(TNum value, long maxDenominator = DefaultMaxDenominator) where TNum : INumber<TNum>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxDenominator, 1);
            if (value is Rational<long> alreadyRational)
            {
                return LimitDenominator(alreadyRational, maxDenominator);
            }
            if (!TNum.IsFinite(value))
            {
                throw new ArgumentException($"{value} can't be written as a fraction");
            }
            if (TNum.IsInteger(value))
            {
                return new Rational<long>(long.CreateChecked(value));
            }
            //Every float and double is exactly some integer over a power of two, so start from that exact value.
            SplitDouble(double.CreateChecked(value), out BigInteger numerator, out BigInteger denominator);
            TNum magnitude = TNum.Abs(value);
            return BestFraction(numerator, denominator, maxDenominator,
                (p, q) => p <= long.MaxValue && TNum.CreateChecked((long)p) / TNum.CreateChecked((long)q) == magnitude);
        }
        /// <summary>
        /// Converts a fraction into another number type.
        /// </summary>
        /// <typeparam name="TNum">The type to convert into</typeparam>
        /// <param name="value">The fraction to convert</param>
        /// <param name="rounding">Which side to land on when the fraction can't be stored exactly</param>
        /// <returns>The converted value</returns>
        /// <remarks>AtMost and AtLeast compare using ToRational, so a double that ToRational reads as exactly the fraction counts as equal to it.</remarks>
        /// <exception cref="NotSupportedException">AtMost or AtLeast on a type that is neither an integer nor double, float or Half</exception>
        public static TNum FromRational<TNum>(Rational<long> value, FractionRounding rounding = FractionRounding.Closest) where TNum : INumber<TNum>
        {
            if (typeof(TNum) == typeof(Rational<long>))
            {
                return (TNum)(object)value;
            }
            TNum candidate = TNum.CreateChecked(value.Numerator) / TNum.CreateChecked(value.Denominator);
            if (rounding == FractionRounding.Closest)
            {
                return candidate;
            }
            int comparison = Compare(ToRational(candidate), value);
            if (rounding == FractionRounding.AtLeast && comparison < 0)
            {
                return Step(candidate, true);
            }
            if (rounding == FractionRounding.AtMost && comparison > 0)
            {
                return Step(candidate, false);
            }
            return candidate;
        }
        /// <summary>
        /// Replacement for TTo.CreateChecked(value) that goes through ToRational/FromRational when either side is a Rational.
        /// </summary>
        public static TTo ConvertChecked<TFrom, TTo>(TFrom value) where TFrom : INumber<TFrom> where TTo : INumber<TTo>
        {
            if (TryConvertRational(value, out TTo converted))
            {
                return converted;
            }
            return TTo.CreateChecked(value);
        }
        /// <summary>
        /// Replacement for TTo.CreateTruncating(value) that goes through ToRational/FromRational when either side is a Rational.
        /// </summary>
        public static TTo ConvertTruncating<TFrom, TTo>(TFrom value) where TFrom : INumber<TFrom> where TTo : INumber<TTo>
        {
            if (TryConvertRational(value, out TTo converted))
            {
                return converted;
            }
            return TTo.CreateTruncating(value);
        }
        /// <summary>
        /// Rounds a fraction to the closest one whose denominator is small enough. Does nothing if it already is.
        /// </summary>
        public static Rational<long> LimitDenominator(Rational<long> value, long maxDenominator = DefaultMaxDenominator)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxDenominator, 1);
            if (value.Denominator <= maxDenominator)
            {
                return value;
            }
            return BestFraction(value.Numerator, value.Denominator, maxDenominator, null);
        }
        /// <summary>
        /// Rounds the result of a calculation done in a wider type back into a long fraction with a small enough denominator.
        /// Do the multiplication or addition in Rational&lt;Int128&gt; first so it can't overflow, then pass it here.
        /// </summary>
        /// <exception cref="OverflowException">If the value is too large for a long numerator at this denominator</exception>
        public static Rational<long> LimitDenominator(Rational<Int128> value, long maxDenominator = DefaultMaxDenominator)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxDenominator, 1);
            return BestFraction((BigInteger)value.Numerator, (BigInteger)value.Denominator, maxDenominator, null);
        }
        /// <summary>
        /// Brings the result of a calculation done in a wider type back into a long fraction without any rounding.
        /// Use this instead of LimitDenominator for totals that must come back to exactly 0 when everything added is taken away again.
        /// </summary>
        /// <exception cref="OverflowException">If the numerator or denominator is too big for a long</exception>
        public static Rational<long> NarrowExact(Rational<Int128> value)
        {
            if (value.Numerator > long.MaxValue || value.Numerator < long.MinValue || value.Denominator > long.MaxValue)
            {
                throw new OverflowException($"{value} can't be stored exactly as a long fraction.");
            }
            return new Rational<long>((long)value.Numerator, (long)value.Denominator);
        }
        /// <summary>
        /// Widens a fraction so that calculations on it have room before being passed to LimitDenominator or NarrowExact.
        /// </summary>
        public static Rational<Int128> Widen(Rational<long> value)
        {
            return new Rational<Int128>(value.Numerator, value.Denominator);
        }
        /// <summary>
        /// Compares two fractions without the overflow that the built in comparison can hit on large denominators.
        /// </summary>
        /// <returns>Negative if left is smaller, zero if equal, positive if left is larger</returns>
        public static int Compare(Rational<long> left, Rational<long> right)
        {
            //Denominators are always positive, so cross multiplying keeps the direction.
            Int128 leftScaled = (Int128)left.Numerator * right.Denominator;
            Int128 rightScaled = (Int128)right.Numerator * left.Denominator;
            return leftScaled.CompareTo(rightScaled);
        }

        #region Internals
        private static bool TryConvertRational<TFrom, TTo>(TFrom value, out TTo converted) where TFrom : INumber<TFrom> where TTo : INumber<TTo>
        {
            if (typeof(TTo) == typeof(Rational<long>))
            {
                converted = (TTo)(object)ToRational(value);
                return true;
            }
            if (value is Rational<long> rational)
            {
                converted = FromRational<TTo>(rational);
                return true;
            }
            converted = TTo.Zero;
            return false;
        }
        /// <summary>
        /// Walks the continued fraction of numerator/denominator, which lists its best approximations from simplest to most exact.
        /// Stops at the first one that is exact or that goodEnough accepts, or at the best one that fits the denominator limit.
        /// </summary>
        private static Rational<long> BestFraction(BigInteger numerator, BigInteger denominator, long maxDenominator, Func<BigInteger, BigInteger, bool>? goodEnough)
        {
            bool negative = numerator.Sign < 0;
            BigInteger target = BigInteger.Abs(numerator);
            BigInteger previousP = 0, previousQ = 1, currentP = 1, currentQ = 0;
            BigInteger remainingNumerator = target, remainingDenominator = denominator;
            while (true)
            {
                BigInteger wholePart = BigInteger.DivRem(remainingNumerator, remainingDenominator, out BigInteger remainder);
                BigInteger nextQ = previousQ + wholePart * currentQ;
                if (nextQ > maxDenominator)
                {
                    break;
                }
                BigInteger nextP = previousP + wholePart * currentP;
                previousP = currentP; previousQ = currentQ;
                currentP = nextP; currentQ = nextQ;
                if (remainder.IsZero || (goodEnough is not null && goodEnough(currentP, currentQ)))
                {
                    return Build(currentP, currentQ, negative);
                }
                remainingNumerator = remainingDenominator;
                remainingDenominator = remainder;
            }
            //The exact value doesn't fit. The closest fraction that does is either the last one found,
            //or the one that goes as far toward the next one as the denominator limit allows.
            BigInteger steps = (maxDenominator - previousQ) / currentQ;
            BigInteger partialP = previousP + steps * currentP;
            BigInteger partialQ = previousQ + steps * currentQ;
            BigInteger partialError = BigInteger.Abs(partialP * denominator - target * partialQ) * currentQ;
            BigInteger currentError = BigInteger.Abs(currentP * denominator - target * currentQ) * partialQ;
            if (steps > 0 && partialError < currentError)
            {
                return Build(partialP, partialQ, negative);
            }
            return Build(currentP, currentQ, negative);
        }
        private static Rational<long> Build(BigInteger numerator, BigInteger denominator, bool negative)
        {
            if (numerator > long.MaxValue)
            {
                throw new OverflowException($"{numerator}/{denominator} doesn't fit in a long fraction. Use a smaller value or a smaller maximum denominator.");
            }
            long longNumerator = (long)numerator;
            return new Rational<long>(negative ? -longNumerator : longNumerator, (long)denominator);
        }
        /// <summary>
        /// Gives the exact value of a double as an integer over a power of two.
        /// </summary>
        private static void SplitDouble(double value, out BigInteger numerator, out BigInteger denominator)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);
            int exponent = (int)((bits >> 52) & 0x7FF);
            long mantissa = bits & 0xFFFFFFFFFFFFFL;
            if (exponent == 0)
            {
                exponent = 1; //Subnormal: no implied leading bit
            }
            else
            {
                mantissa |= 1L << 52;
            }
            exponent -= 1075;
            numerator = mantissa;
            denominator = BigInteger.One;
            if (exponent > 0)
            {
                numerator <<= exponent;
            }
            else
            {
                denominator <<= -exponent;
            }
            if (value < 0)
            {
                numerator = -numerator;
            }
        }
        /// <summary>
        /// The next representable value above or below.
        /// </summary>
        private static TNum Step<TNum>(TNum value, bool up) where TNum : INumber<TNum>
        {
            if (typeof(TNum) == typeof(double))
            {
                double asDouble = (double)(object)value;
                return (TNum)(object)(up ? double.BitIncrement(asDouble) : double.BitDecrement(asDouble));
            }
            if (typeof(TNum) == typeof(float))
            {
                float asFloat = (float)(object)value;
                return (TNum)(object)(up ? float.BitIncrement(asFloat) : float.BitDecrement(asFloat));
            }
            if (typeof(TNum) == typeof(Half))
            {
                Half asHalf = (Half)(object)value;
                return (TNum)(object)(up ? Half.BitIncrement(asHalf) : Half.BitDecrement(asHalf));
            }
            if (TNum.One / (TNum.One + TNum.One) == TNum.Zero)
            {
                //Integer type, so neighbors are one apart
                return up ? value + TNum.One : value - TNum.One;
            }
            throw new NotSupportedException($"Don't know the next representable value for {typeof(TNum).FullName}");
        }
        #endregion
    }
}
