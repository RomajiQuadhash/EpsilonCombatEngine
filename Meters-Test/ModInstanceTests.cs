using Meters;
using Rationalz;
namespace Meters_Test;

/// <summary>
/// Tests for ModInstance, one source of change for a RateManagedMeter.
/// </summary>
/// <remarks>Claude written, Romaji approved (minus a test on ToString)</remarks>
public class ModInstanceTests
{
    [Fact(DisplayName = "A ModInstance keeps what it was made with and works out its magnitude as the change per unit of time")]
    public void KeepsItsValuesAndWorksOutMagnitude()
    {
        ModInstance<int, int> modifier = new(5, 2);

        Assert.Equal(5, modifier.PerTick);
        Assert.Equal(2, modifier.TickRate);
        Assert.Equal(new Rational<long>(5, 2), modifier.Magnitude);
    }
    [Fact(DisplayName = "A ModInstance that takes away has a negative magnitude, and one that does nothing has a magnitude of zero")]
    public void MagnitudeKeepsItsSign()
    {
        Assert.Equal(new Rational<long>(-5, 2), new ModInstance<int, int>(-5, 2).Magnitude);
        Assert.Equal(Rational<long>.Zero, new ModInstance<int, int>(0, 2).Magnitude);
    }
    [Fact(DisplayName = "A ModInstance's magnitude is reduced to its simplest form")]
    public void MagnitudeIsReduced()
    {
        Assert.Equal(new Rational<long>(5), new ModInstance<int, int>(10, 2).Magnitude);
        Assert.Equal(new Rational<long>(2, 3), new ModInstance<int, int>(4, 6).Magnitude);
    }
    [Fact(DisplayName = "A ModInstance on a floating point timeline reads its tick rate as the simple fraction it stands for")]
    public void MagnitudeFromAFractionalTickRate()
    {
        Assert.Equal(new Rational<long>(3, 10), new ModInstance<double, int>(1, 3.3333333333333335).Magnitude);
        Assert.Equal(new Rational<long>(2), new ModInstance<double, int>(1, 0.5).Magnitude);
    }
    [Fact(DisplayName = "A ModInstance can't be made with a tick rate of zero or less")]
    public void TickRateMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModInstance<int, int>(5, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModInstance<int, int>(5, -2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModInstance<double, int>(5, -0.5));
    }
    [Fact(DisplayName = "Two ModInstances made with the same values are equal, and ones made with different values aren't")]
    public void EqualByValue()
    {
        ModInstance<int, int> modifier = new(5, 2);

        Assert.Equal(new ModInstance<int, int>(5, 2), modifier);
        Assert.NotSame(new ModInstance<int, int>(5, 2), modifier);
        Assert.NotEqual(new ModInstance<int, int>(5, 3), modifier);
        Assert.NotEqual(new ModInstance<int, int>(4, 2), modifier);
        //The same rate from different values is still a different ModInstance, since the tick rate matters
        Assert.NotEqual(new ModInstance<int, int>(10, 4), modifier);
    }
}
