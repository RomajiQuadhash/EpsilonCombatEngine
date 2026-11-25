using Meters;
using Rationalz;
namespace Meters_Test;

/// <summary>
/// Tests for RationalConversion, which every conversion between a timeline or meter number and a fraction goes through.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class RationalConversionTests
{
    #region ToRational
    [Theory(DisplayName = "ToRational gives a double the simplest fraction that is that double")]
    [InlineData(0.5, 1, 2)]
    [InlineData(0.1, 1, 10)]
    [InlineData(0.3, 3, 10)]
    [InlineData(-2.5, -5, 2)]
    [InlineData(3.3333333333333335, 10, 3)]
    [InlineData(0.3333333333333333, 1, 3)]
    [InlineData(0.14285714285714285, 1, 7)]
    [InlineData(123456.789, 123456789, 1000)]
    public void ToRationalFindsTheSimplestFractionForADouble(double value, long numerator, long denominator)
    {
        Assert.Equal(new Rational<long>(numerator, denominator), RationalConversion.ToRational(value));
    }
    [Theory(DisplayName = "ToRational gives a float the simplest fraction that is that float")]
    [InlineData(0.5f, 1, 2)]
    [InlineData(0.1f, 1, 10)]
    [InlineData(-0.75f, -3, 4)]
    public void ToRationalFindsTheSimplestFractionForAFloat(float value, long numerator, long denominator)
    {
        Assert.Equal(new Rational<long>(numerator, denominator), RationalConversion.ToRational(value));
    }
    [Fact(DisplayName = "ToRational finds a third from a float, which holds far fewer digits than a double")]
    public void ToRationalFindsAThirdFromAFloat()
    {
        Assert.Equal(new Rational<long>(1, 3), RationalConversion.ToRational(1f / 3f));
    }
    [Fact(DisplayName = "ToRational passes whole numbers straight through, whatever type they are")]
    public void ToRationalPassesWholeNumbersThrough()
    {
        Assert.Equal(new Rational<long>(7), RationalConversion.ToRational(7));
        Assert.Equal(new Rational<long>(-7), RationalConversion.ToRational(-7L));
        Assert.Equal(new Rational<long>(7), RationalConversion.ToRational(7.0));
        Assert.Equal(new Rational<long>(0), RationalConversion.ToRational(0.0));
        Assert.Equal(new Rational<long>(3), RationalConversion.ToRational((byte)3));
    }
    [Fact(DisplayName = "ToRational leaves a fraction that already has a small enough denominator alone")]
    public void ToRationalLeavesASmallFractionAlone()
    {
        Rational<long> fraction = new(22, 7);

        Assert.Equal(fraction, RationalConversion.ToRational(fraction));
    }
    [Fact(DisplayName = "ToRational gives the closest fraction that fits when no small fraction is exactly the number")]
    public void ToRationalRoundsWhenNothingSmallIsExact()
    {
        Rational<long> pi = RationalConversion.ToRational(Math.PI);

        Assert.True(pi.Denominator <= RationalConversion.DefaultMaxDenominator);
        Assert.True(Math.Abs((double)pi.Numerator / pi.Denominator - Math.PI) < 1e-13);
    }
    [Fact(DisplayName = "ToRational keeps to a smaller maximum denominator when given one")]
    public void ToRationalKeepsToTheMaximumDenominator()
    {
        Assert.Equal(new Rational<long>(22, 7), RationalConversion.ToRational(Math.PI, 10));
        Assert.Equal(new Rational<long>(355, 113), RationalConversion.ToRational(Math.PI, 1000));
        Assert.Equal(new Rational<long>(3), RationalConversion.ToRational(Math.PI, 1));
    }
    [Fact(DisplayName = "ToRational throws for numbers that aren't finite")]
    public void ToRationalThrowsForNonFinite()
    {
        Assert.Throws<ArgumentException>(() => RationalConversion.ToRational(double.NaN));
        Assert.Throws<ArgumentException>(() => RationalConversion.ToRational(double.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => RationalConversion.ToRational(float.NegativeInfinity));
    }
    [Fact(DisplayName = "ToRational throws if the maximum denominator is less than 1")]
    public void ToRationalThrowsForABadMaximumDenominator()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RationalConversion.ToRational(0.5, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RationalConversion.ToRational(0.5, -3));
    }
    #endregion
    #region FromRational
    [Fact(DisplayName = "FromRational gives a floating point type the closest value it can hold")]
    public void FromRationalClosestForFloatingPoint()
    {
        Assert.Equal(1.0 / 3.0, RationalConversion.FromRational<double>(new Rational<long>(1, 3)));
        Assert.Equal(1f / 3f, RationalConversion.FromRational<float>(new Rational<long>(1, 3)));
        Assert.Equal(3.5, RationalConversion.FromRational<double>(new Rational<long>(7, 2)));
        Assert.Equal(-0.1, RationalConversion.FromRational<double>(new Rational<long>(-1, 10)));
    }
    [Theory(DisplayName = "FromRational gives an integer type the fraction with its fractional part dropped, toward zero")]
    [InlineData(7, 2, 3)]
    [InlineData(-7, 2, -3)]
    [InlineData(6, 2, 3)]
    [InlineData(1, 3, 0)]
    [InlineData(-1, 3, 0)]
    public void FromRationalClosestForIntegers(long numerator, long denominator, int expected)
    {
        Assert.Equal(expected, RationalConversion.FromRational<int>(new Rational<long>(numerator, denominator)));
    }
    [Theory(DisplayName = "FromRational with AtLeast gives an integer type the fraction rounded up")]
    [InlineData(7, 2, 4)]
    [InlineData(-7, 2, -3)]
    [InlineData(6, 2, 3)]
    [InlineData(1, 3, 1)]
    [InlineData(-1, 3, 0)]
    [InlineData(10, 3, 4)]
    public void FromRationalAtLeastForIntegers(long numerator, long denominator, int expected)
    {
        Assert.Equal(expected, RationalConversion.FromRational<int>(new Rational<long>(numerator, denominator), FractionRounding.AtLeast));
    }
    [Theory(DisplayName = "FromRational with AtMost gives an integer type the fraction rounded down")]
    [InlineData(7, 2, 3)]
    [InlineData(-7, 2, -4)]
    [InlineData(6, 2, 3)]
    [InlineData(1, 3, 0)]
    [InlineData(-1, 3, -1)]
    public void FromRationalAtMostForIntegers(long numerator, long denominator, int expected)
    {
        Assert.Equal(expected, RationalConversion.FromRational<int>(new Rational<long>(numerator, denominator), FractionRounding.AtMost));
    }
    [Fact(DisplayName = "FromRational with AtLeast or AtMost leaves a double alone when it already reads back as the fraction")]
    public void FromRationalDoesNotNudgeADoubleThatReadsBackRight()
    {
        //0.3 as a double is a hair under 3/10, but ToRational reads it as exactly 3/10, and that is what everything else uses
        Assert.Equal(0.3, RationalConversion.FromRational<double>(new Rational<long>(3, 10), FractionRounding.AtLeast));
        Assert.Equal(0.3, RationalConversion.FromRational<double>(new Rational<long>(3, 10), FractionRounding.AtMost));
        Assert.Equal(3.3333333333333335, RationalConversion.FromRational<double>(new Rational<long>(10, 3), FractionRounding.AtLeast));
    }
    [Fact(DisplayName = "FromRational works for an integer type when the numerator alone is too big for it but the answer isn't")]
    public void FromRationalDividesBeforeConverting()
    {
        Rational<long> fraction = new(99999810000090, 19999981); //About 5 million, with a numerator far beyond an int

        Assert.Equal(4999995, RationalConversion.FromRational<int>(fraction));
        Assert.Equal(4999996, RationalConversion.FromRational<int>(fraction, FractionRounding.AtLeast));
    }
    [Fact(DisplayName = "FromRational into a fraction gives the same fraction back")]
    public void FromRationalIntoAFraction()
    {
        Rational<long> fraction = new(22, 7);

        Assert.Equal(fraction, RationalConversion.FromRational<Rational<long>>(fraction));
    }
    [Theory(DisplayName = "A simple fraction survives being turned into a double and back")]
    [InlineData(1, 3)]
    [InlineData(10, 3)]
    [InlineData(-22, 7)]
    [InlineData(355, 113)]
    [InlineData(1, 1000)]
    [InlineData(99999, 4999)]
    public void FractionsRoundTripThroughDouble(long numerator, long denominator)
    {
        Rational<long> fraction = new(numerator, denominator);

        Assert.Equal(fraction, RationalConversion.ToRational(RationalConversion.FromRational<double>(fraction)));
    }
    #endregion
    #region LimitDenominator, NarrowExact, Widen and Compare
    [Fact(DisplayName = "LimitDenominator leaves a fraction with a small enough denominator alone")]
    public void LimitDenominatorLeavesSmallFractionsAlone()
    {
        Rational<long> fraction = new(22, 7);

        Assert.Equal(fraction, RationalConversion.LimitDenominator(fraction));
        Assert.Equal(fraction, RationalConversion.LimitDenominator(fraction, 7));
    }
    [Fact(DisplayName = "LimitDenominator gives the closest fraction that fits when the denominator is too big")]
    public void LimitDenominatorRoundsBigDenominators()
    {
        Assert.Equal(new Rational<long>(1, 3), RationalConversion.LimitDenominator(new Rational<long>(3333333333333333, 10000000000000000)));
        Assert.Equal(new Rational<long>(22, 7), RationalConversion.LimitDenominator(new Rational<long>(355, 113), 10));
        Assert.Equal(new Rational<long>(-22, 7), RationalConversion.LimitDenominator(new Rational<long>(-355, 113), 10));
        //With more room there is a closer one than 22/7, even though it isn't a simpler one
        Assert.Equal(new Rational<long>(311, 99), RationalConversion.LimitDenominator(new Rational<long>(355, 113), 100));
    }
    [Fact(DisplayName = "LimitDenominator brings back a result that was worked out in a wider type")]
    public void LimitDenominatorNarrowsAWideResult()
    {
        Rational<long> big = new(long.MaxValue / 3);
        Rational<long> small = new(3, long.MaxValue / 3);

        //Multiplied as longs this would overflow on the way, even though the answer is just 3
        Assert.Equal(new Rational<long>(3), RationalConversion.LimitDenominator(RationalConversion.Widen(big) * RationalConversion.Widen(small)));
    }
    [Fact(DisplayName = "NarrowExact brings a wide fraction back unchanged if it fits")]
    public void NarrowExactKeepsWhatFits()
    {
        Rational<Int128> wide = new(22, 7);

        Assert.Equal(new Rational<long>(22, 7), RationalConversion.NarrowExact(wide));
    }
    [Fact(DisplayName = "NarrowExact throws if the numerator or denominator is too big, and never rounds")]
    public void NarrowExactThrowsWhenItDoesNotFit()
    {
        Rational<Int128> bigNumerator = new((Int128)long.MaxValue * 4 + 1, 2);
        Rational<Int128> bigDenominator = new(1, (Int128)long.MaxValue * 4);

        Assert.Throws<OverflowException>(() => RationalConversion.NarrowExact(bigNumerator));
        Assert.Throws<OverflowException>(() => RationalConversion.NarrowExact(bigDenominator));
    }
    [Fact(DisplayName = "Widen gives the same fraction in a wider type")]
    public void WidenKeepsTheFraction()
    {
        Rational<Int128> wide = RationalConversion.Widen(new Rational<long>(-22, 7));

        Assert.Equal((Int128)(-22), wide.Numerator);
        Assert.Equal((Int128)7, wide.Denominator);
    }
    [Fact(DisplayName = "Compare orders fractions, including negative ones and equal ones written differently")]
    public void CompareOrdersFractions()
    {
        Assert.True(RationalConversion.Compare(new Rational<long>(1, 3), new Rational<long>(1, 2)) < 0);
        Assert.True(RationalConversion.Compare(new Rational<long>(1, 2), new Rational<long>(1, 3)) > 0);
        Assert.True(RationalConversion.Compare(new Rational<long>(-1, 2), new Rational<long>(1, 3)) < 0);
        Assert.True(RationalConversion.Compare(new Rational<long>(-1, 3), new Rational<long>(-1, 2)) > 0);
        Assert.Equal(0, RationalConversion.Compare(new Rational<long>(2, 4), new Rational<long>(1, 2)));
    }
    [Fact(DisplayName = "Compare gets fractions with huge parts right, where multiplying them as longs would overflow")]
    public void CompareHandlesHugeFractions()
    {
        Rational<long> slightlyBigger = new(long.MaxValue - 1, long.MaxValue);
        Rational<long> slightlySmaller = new(long.MaxValue - 2, long.MaxValue - 1);

        Assert.True(RationalConversion.Compare(slightlyBigger, slightlySmaller) > 0);
        Assert.True(RationalConversion.Compare(slightlySmaller, slightlyBigger) < 0);
    }
    #endregion
    #region ConvertChecked and ConvertTruncating
    [Fact(DisplayName = "ConvertChecked and ConvertTruncating turn a number into a fraction the same way ToRational does")]
    public void ConvertIntoAFraction()
    {
        Assert.Equal(new Rational<long>(1, 10), RationalConversion.ConvertChecked<double, Rational<long>>(0.1));
        Assert.Equal(new Rational<long>(1, 10), RationalConversion.ConvertTruncating<double, Rational<long>>(0.1));
        Assert.Equal(new Rational<long>(4), RationalConversion.ConvertChecked<int, Rational<long>>(4));
    }
    [Fact(DisplayName = "ConvertChecked and ConvertTruncating turn a fraction into a number the same way FromRational does")]
    public void ConvertOutOfAFraction()
    {
        Assert.Equal(3, RationalConversion.ConvertChecked<Rational<long>, int>(new Rational<long>(7, 2)));
        Assert.Equal(3, RationalConversion.ConvertTruncating<Rational<long>, int>(new Rational<long>(7, 2)));
        Assert.Equal(3.5, RationalConversion.ConvertTruncating<Rational<long>, double>(new Rational<long>(7, 2)));
    }
    [Fact(DisplayName = "ConvertChecked and ConvertTruncating act like the ordinary conversions when no fraction is involved")]
    public void ConvertWithoutFractions()
    {
        Assert.Equal(3.0, RationalConversion.ConvertChecked<int, double>(3));
        Assert.Equal(3, RationalConversion.ConvertTruncating<double, int>(3.9));
        Assert.Equal(255, RationalConversion.ConvertTruncating<int, byte>(511)); //Truncating keeps the low bits
        Assert.Throws<OverflowException>(() => RationalConversion.ConvertChecked<int, byte>(511));
    }
    #endregion
}
