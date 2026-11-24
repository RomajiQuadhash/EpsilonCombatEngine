using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Meters_Test;

/// <summary>
/// Tests for how a RateManagedMeter turns its ModInstances into one tick rate and one amount per tick,
/// and for adding, replacing and removing them.
/// </summary>
/// <remarks>Claude written, Romaji approved with one code change (an extra check while removing)</remarks>
public class RateManagedMeterModifierTests
{
    #region ModInstances with the same tick rate
    [Fact(DisplayName = "Two ModInstances with the same tick rate give ticks of their total at that tick rate")]
    public void SameTickRateAddsUp()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        meter.Add(new ModInstance<int, int>(3, 5));
        meter.Add(new ModInstance<int, int>(4, 5));

        Assert.Equal(7, meter.Derivative);
        Assert.Equal(5, meter.DerivativeDenominator);
        Assert.Equal(new Rational<long>(7, 5), meter.TrueRate);
    }
    [Fact(DisplayName = "Several ModInstances with the same tick rate give ticks of their total, including ones that take away")]
    public void SameTickRateAddsUpWithNegatives()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        meter.Add(new ModInstance<int, int>(1, 2));
        meter.Add(new ModInstance<int, int>(6, 2));
        meter.Add(new ModInstance<int, int>(-2, 2));

        Assert.Equal(5, meter.Derivative);
        Assert.Equal(2, meter.DerivativeDenominator);
    }
    [Fact(DisplayName = "ModInstances with the same tick rate tick together on the timeline, with nothing left over")]
    public void SameTickRateTicksTogether()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        meter.Add(new ModInstance<int, int>(3, 5));
        meter.Add(new ModInstance<int, int>(4, 5));
        Card<int, string> justBeforeSecondTick = new(9, timeline, PriorityRank.None);
        timeline.AddEvent(justBeforeSecondTick);
        Card<int, string> end = new(10, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> reached = new();
        justBeforeSecondTick.Occurring += reached.OnTrigger;
        end.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, justBeforeSecondTick))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(7, meter.CurrentValue);

        while (!ReferenceEquals(reached.MostRecentEvent, end))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(14, meter.CurrentValue);
        Assert.Equal(Rational<long>.Zero, meter.CurrentSpilloverFraction);
        Assert.Equal(LeapYearState.None, meter.CurrentLeapYear);
    }
    #endregion
    #region ModInstances that nearly cancel
    [Fact(DisplayName = "ModInstances that almost cancel out tick slower than either of them, by the size of what is left")]
    public void NearlyCancellingTicksSlower()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        meter.Add(new ModInstance<int, int>(1, 3));
        meter.Add(new ModInstance<int, int>(-1, 4));

        //1/3 - 1/4 leaves 1/12, so 1 every 12
        Assert.Equal(new Rational<long>(1, 12), meter.TrueRate);
        Assert.Equal(1, meter.Derivative);
        Assert.Equal(12, meter.DerivativeDenominator);
    }
    [Fact(DisplayName = "ModInstances that almost cancel out the other way tick slower and downward")]
    public void NearlyCancellingTicksSlowerDownward()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        meter.Add(new ModInstance<int, int>(-1, 3));
        meter.Add(new ModInstance<int, int>(1, 4));

        Assert.Equal(new Rational<long>(-1, 12), meter.TrueRate);
        Assert.Equal(-1, meter.Derivative);
        Assert.Equal(12, meter.DerivativeDenominator);
    }
    [Fact(DisplayName = "ModInstances that almost cancel out move the meter by 1 at the slow tick and not before")]
    public void NearlyCancellingMovesAtTheSlowTick()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        meter.Add(new ModInstance<int, int>(1, 3));
        meter.Add(new ModInstance<int, int>(-1, 4));
        Card<int, string> justBeforeTick = new(11, timeline, PriorityRank.None);
        timeline.AddEvent(justBeforeTick);
        Card<int, string> end = new(12, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> reached = new();
        justBeforeTick.Occurring += reached.OnTrigger;
        end.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, justBeforeTick))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(0, meter.CurrentValue);

        while (!ReferenceEquals(reached.MostRecentEvent, end))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(1, meter.CurrentValue);
        Assert.Equal(Rational<long>.Zero, meter.CurrentSpilloverFraction);
    }
    [Fact(DisplayName = "ModInstances that cancel out exactly leave the meter not ticking at all")]
    public void ExactlyCancellingDoesNotTick()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        meter.Add(new ModInstance<int, int>(3, 6));
        meter.Add(new ModInstance<int, int>(-1, 2));

        Assert.Equal(2, meter.Modifiers.Count);
        Assert.Equal(Rational<long>.Zero, meter.TrueRate);
        Assert.Equal(0, meter.Derivative);
    }
    #endregion
    #region Which ModInstance sets the tick rate
    [Fact(DisplayName = "The ModInstance that changes the meter most sets the tick rate, and the others are merged into its ticks")]
    public void StrongestSetsTheTickRate()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        meter.Add(new ModInstance<int, int>(1, 7));  //1/7 a time unit
        meter.Add(new ModInstance<int, int>(10, 2)); //5 a time unit

        Assert.Equal(2, meter.DerivativeDenominator);
        Assert.Equal(10, meter.Derivative); //5 and 1/7 a time unit is 10 and 2/7 every 2, and the fraction is dropped
    }
    [Fact(DisplayName = "The strongest ModInstance is judged by size, so one that takes away can set the tick rate")]
    public void StrongestIsBySize()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        meter.Add(new ModInstance<int, int>(1, 7));
        meter.Add(new ModInstance<int, int>(-10, 2));

        Assert.Equal(2, meter.DerivativeDenominator);
        Assert.Equal(-9, meter.Derivative); //-5 + 1/7 a time unit is -9 and 5/7 every 2, and the fraction is dropped toward zero
    }
    [Fact(DisplayName = "When two ModInstances are equally strong, the one that ticks more often sets the tick rate")]
    public void TiesGoToTheFastest()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> slowFirst = new(timeline, 0, -1000, 1000);
        RateManagedMeter<int, int> fastFirst = new(timeline, 0, -1000, 1000);

        slowFirst.Add(new ModInstance<int, int>(20, 2));
        slowFirst.Add(new ModInstance<int, int>(10, 1));
        fastFirst.Add(new ModInstance<int, int>(10, 1));
        fastFirst.Add(new ModInstance<int, int>(20, 2));

        //The order they were added in doesn't matter
        Assert.Equal((20, 1), (slowFirst.Derivative, slowFirst.DerivativeDenominator));
        Assert.Equal((20, 1), (fastFirst.Derivative, fastFirst.DerivativeDenominator));
    }
    [Fact(DisplayName = "Removing the strongest ModInstance hands the tick rate to the next strongest")]
    public void RemovingTheStrongestPicksTheNext()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        meter.Add(new ModInstance<int, int>(1, 7));
        ModifierKey strongest = meter.Add(new ModInstance<int, int>(10, 2));
        meter.Add(new ModInstance<int, int>(2, 5));

        Assert.True(meter.Remove(strongest));

        //2/5 is stronger than 1/7. Together they are 19/35, which is 2 and 5/7 every 5
        Assert.Equal(5, meter.DerivativeDenominator);
        Assert.Equal(2, meter.Derivative);
    }
    [Fact(DisplayName = "Removing a weaker ModInstance leaves the tick rate with the strongest")]
    public void RemovingAWeakerKeepsTheTickRate()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        ModifierKey weaker = meter.Add(new ModInstance<int, int>(1, 7));
        meter.Add(new ModInstance<int, int>(10, 2));

        Assert.True(meter.Remove(weaker));

        Assert.Equal((10, 2), (meter.Derivative, meter.DerivativeDenominator));
    }
    #endregion
    #region Add, Replace and Remove
    [Fact(DisplayName = "A new RateManagedMeter has no ModInstances and isn't changing")]
    public void StartsEmpty()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 5, -1000, 1000);

        Assert.Empty(meter.Modifiers);
        Assert.Equal(Rational<long>.Zero, meter.TrueRate);
        Assert.Equal(0, meter.Derivative);
        Assert.Equal(5, meter.CurrentValue);
        Assert.Equal(LeapYearState.None, meter.CurrentLeapYear);
        Assert.Equal(Rational<long>.Zero, meter.CurrentSpilloverFraction);
    }
    [Fact(DisplayName = "Add gives back a different key each time, and each key finds its own ModInstance")]
    public void AddGivesDistinctKeys()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        ModInstance<int, int> first = new(3, 5);
        ModInstance<int, int> second = new(4, 5);

        ModifierKey firstKey = meter.Add(first);
        ModifierKey secondKey = meter.Add(second);

        Assert.NotEqual(firstKey, secondKey);
        Assert.Same(first, meter.Modifiers[firstKey]);
        Assert.Same(second, meter.Modifiers[secondKey]);
    }
    [Fact(DisplayName = "Two ModInstances that are equal are still kept separately, and removing one leaves the other")]
    public void EqualModInstancesAreKeptSeparately()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        ModifierKey firstKey = meter.Add(new ModInstance<int, int>(3, 5));
        ModifierKey secondKey = meter.Add(new ModInstance<int, int>(3, 5));
        Assert.Equal(6, meter.Derivative);

        Assert.True(meter.Remove(firstKey));

        Assert.Single(meter.Modifiers);
        Assert.True(meter.Modifiers.ContainsKey(secondKey));
        Assert.Equal(3, meter.Derivative);
    }
    [Fact(DisplayName = "A key isn't given out again after its ModInstance is removed")]
    public void KeysAreNotReused()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        ModifierKey removed = meter.Add(new ModInstance<int, int>(3, 5));
        meter.Remove(removed);

        ModifierKey next = meter.Add(new ModInstance<int, int>(3, 5));

        Assert.NotEqual(removed, next);
        Assert.False(meter.Remove(removed)); //The old key doesn't remove the new one
        Assert.Single(meter.Modifiers);
    }
    [Fact(DisplayName = "Remove with a key the meter doesn't have changes nothing and says so")]
    public void RemoveUnknownKey()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        meter.Add(new ModInstance<int, int>(3, 5));

        Assert.False(meter.Remove(new ModifierKey(999)));

        Assert.Single(meter.Modifiers);
        Assert.Equal((3, 5), (meter.Derivative, meter.DerivativeDenominator));
    }
    [Fact(DisplayName = "Removing the last ModInstance stops the meter and leaves the true rate at exactly zero")]
    public void RemovingTheLastStopsTheMeter()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        ModifierKey first = meter.Add(new ModInstance<int, int>(1, 3));
        ModifierKey second = meter.Add(new ModInstance<int, int>(1, 7));

        meter.Remove(first);
        Assert.Equal(new Rational<long>(1, 7), meter.TrueRate);
        meter.Remove(second);

        Assert.Empty(meter.Modifiers);
        Assert.Equal(Rational<long>.Zero, meter.TrueRate);
        Assert.Equal(0, meter.Derivative);
    }
    [Fact(DisplayName = "Replace swaps the ModInstance under a key and the rate follows")]
    public void ReplaceSwapsTheModInstance()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        ModifierKey key = meter.Add(new ModInstance<int, int>(3, 5));
        ModInstance<int, int> replacement = new(8, 2);

        Assert.True(meter.Replace(key, replacement));

        Assert.Single(meter.Modifiers);
        Assert.Same(replacement, meter.Modifiers[key]);
        Assert.Equal((8, 2), (meter.Derivative, meter.DerivativeDenominator));
        Assert.Equal(new Rational<long>(4), meter.TrueRate);
    }
    [Fact(DisplayName = "Replace with a key the meter doesn't have changes nothing and says so")]
    public void ReplaceUnknownKey()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        meter.Add(new ModInstance<int, int>(3, 5));

        Assert.False(meter.Replace(new ModifierKey(999), new ModInstance<int, int>(8, 2)));

        Assert.Single(meter.Modifiers);
        Assert.Equal((3, 5), (meter.Derivative, meter.DerivativeDenominator));
    }
    [Fact(DisplayName = "Add refuses a ModInstance whose total can't be stored exactly, and changes nothing")]
    public void AddRefusesATotalThatDoesNotFit()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        //Three tick rates with nothing in common, so the total needs a denominator of all three multiplied together
        meter.Add(new ModInstance<int, int>(1, 9999991));
        meter.Add(new ModInstance<int, int>(1, 9999990));
        Rational<long> rateBefore = meter.TrueRate;
        (int perTick, int tickRate) scheduleBefore = (meter.Derivative, meter.DerivativeDenominator);

        Assert.Throws<OverflowException>(() => meter.Add(new ModInstance<int, int>(1, 9999989)));

        Assert.Equal(2, meter.Modifiers.Count);
        Assert.Equal(rateBefore, meter.TrueRate);
        Assert.Equal(scheduleBefore, (meter.Derivative, meter.DerivativeDenominator));
    }
    [Fact(DisplayName = "SetDerivative always throws, since the rate comes from the ModInstances")]
    public void SetDerivativeThrows()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        meter.Add(new ModInstance<int, int>(3, 5));

        Assert.Throws<NotSupportedException>(() => meter.SetDerivative(99, 1, out _));

        Assert.Equal((3, 5), (meter.Derivative, meter.DerivativeDenominator));
    }
    #endregion
}
