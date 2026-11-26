using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
namespace Meters_Test;

/// <summary>
/// Tests for SpeedMeter and the MaxStatAggregator it shares with the other speeds in a combat.
/// </summary>
/// <remarks>Claude written, Romaji approved after removing an issue with being MaxDelayMagnitude dependent.</remarks>
public class SpeedMeterTests
{
    #region MaxStatAggregator
    [Fact(DisplayName = "An aggregator with no meters has a maximum of zero")]
    public void EmptyAggregatorHasNoMaximum()
    {
        MaxStatAggregator<int, SpeedMeter<int>> aggregator = new();

        Assert.Empty(aggregator.Meters);
        Assert.Equal(Rational<long>.Zero, aggregator.GetMaximum());
    }
    [Fact(DisplayName = "A SpeedMeter adds itself to its aggregator when it is made")]
    public void SpeedMeterRegistersItself()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> aggregator = new();

        SpeedMeter<int> speed = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2), aggregator);

        Assert.Same(aggregator, speed.SpeedAggregator);
        Assert.Contains(speed, aggregator.Meters);
        Assert.Single(aggregator.Meters);
    }
    [Fact(DisplayName = "The aggregator's maximum is the highest modified stat among its meters")]
    public void MaximumIsTheHighestModifiedStat()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> aggregator = new();
        SpeedMeter<int> slow = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2), aggregator);
        SpeedMeter<int> fast = new(timeline, 30, new Rational<long>(-2), new Rational<long>(2), aggregator);
        SpeedMeter<int> middle = new(timeline, 20, new Rational<long>(-2), new Rational<long>(2), aggregator);

        Assert.Equal(new Rational<long>(30), aggregator.GetMaximum());

        //Buffs and debuffs count: the slow one tripled and buffed again is now ahead of the fast one halved
        slow.AddInstantChange(new Rational<long>(2));
        fast.AddInstantChange(new Rational<long>(-1));
        Assert.Equal(new Rational<long>(30), slow.GetModifiedStat());
        Assert.Equal(new Rational<long>(15), fast.GetModifiedStat());
        Assert.Equal(new Rational<long>(30), aggregator.GetMaximum());

        middle.AddInstantChange(new Rational<long>(1));
        Assert.Equal(new Rational<long>(40), aggregator.GetMaximum());
    }
    #endregion
    #region SpeedFraction and DelayMultiplier
    [Fact(DisplayName = "The fastest meter has a speed fraction of 1 and a delay multiplier of 1")]
    public void FastestIsOne()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> aggregator = new();
        SpeedMeter<int> fast = new(timeline, 30, new Rational<long>(-2), new Rational<long>(2), aggregator);
        _ = new SpeedMeter<int>(timeline, 10, new Rational<long>(-2), new Rational<long>(2), aggregator);

        Assert.Equal(Rational<long>.One, fast.SpeedFraction);
        Assert.Equal(Rational<long>.One, fast.DelayMultiplier);
    }
    [Fact(DisplayName = "A slower meter's speed fraction is its speed over the fastest, and its delay multiplier is the other way up")]
    public void SlowerIsAFraction()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> aggregator = new();
        _ = new SpeedMeter<int>(timeline, 30, new Rational<long>(-2), new Rational<long>(2), aggregator);
        SpeedMeter<int> slower = new(timeline, 20, new Rational<long>(-2), new Rational<long>(2), aggregator);

        Assert.Equal(new Rational<long>(2, 3), slower.SpeedFraction);
        Assert.Equal(new Rational<long>(3, 2), slower.DelayMultiplier);
    }
    [Fact(DisplayName = "The delay multiplier stops at MaxDelayMagnitude however slow the meter is")]
    public void DelayMultiplierIsCapped()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> aggregator = new();
        _ = new SpeedMeter<int>(timeline, 100, new Rational<long>(-2), new Rational<long>(2), aggregator);
        SpeedMeter<int> exactlyAtTheCap = new(timeline, 100, new Rational<long>(-2), new Rational<long>(2), aggregator);
        exactlyAtTheCap.AddInstantChange(-BattleTuning.MaxDelayMagnitude + 1); //A MaxDelayMagnitude of 100
        SpeedMeter<int> verySlow = new(timeline, 1, new Rational<long>(-2), new Rational<long>(2), aggregator);

        Assert.Equal(BattleTuning.MaxDelayMagnitude, exactlyAtTheCap.DelayMultiplier);
        Assert.Equal(BattleTuning.MaxDelayMagnitude, verySlow.DelayMultiplier);
        Assert.Equal(new Rational<long>(1, 100), verySlow.SpeedFraction); //The fraction itself isn't capped
    }
    [Fact(DisplayName = "Buffing the fastest meter slows everyone else's speed fraction")]
    public void BuffingTheFastestSlowsTheRest()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> aggregator = new();
        SpeedMeter<int> fast = new(timeline, 20, new Rational<long>(-2), new Rational<long>(2), aggregator);
        SpeedMeter<int> slower = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2), aggregator);
        Assert.Equal(new Rational<long>(1, 2), slower.SpeedFraction);

        fast.AddInstantChange(Rational<long>.One); //Now 40

        Assert.Equal(new Rational<long>(1, 4), slower.SpeedFraction);
        Assert.Equal(BattleTuning.MaxDelayMagnitude, slower.DelayMultiplier); //Would be 4, but it is capped
    }
    [Fact(DisplayName = "A meter that overtakes the fastest becomes the one the others are measured against")]
    public void OvertakingBecomesTheMeasure()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> aggregator = new();
        SpeedMeter<int> wasFastest = new(timeline, 20, new Rational<long>(-2), new Rational<long>(2), aggregator);
        SpeedMeter<int> overtakes = new(timeline, 10, new Rational<long>(-2), new Rational<long>(4), aggregator);

        overtakes.AddInstantChange(new Rational<long>(3)); //Now 40

        Assert.Equal(Rational<long>.One, overtakes.SpeedFraction);
        Assert.Equal(new Rational<long>(1, 2), wasFastest.SpeedFraction);
    }
    #endregion
    #region Cloning
    [Fact(DisplayName = "Cloning one SpeedMeter clones its aggregator and every other speed in it")]
    public void CloningOneClonesThemAll()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> aggregator = new();
        SpeedMeter<int> first = new(timeline, 20, new Rational<long>(-2), new Rational<long>(2), aggregator);
        SpeedMeter<int> second = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2), aggregator);
        CloneContext context = new();
        timeline.Clone(context);

        SpeedMeter<int> firstClone = (SpeedMeter<int>)first.Clone(context);

        Assert.True(context.TryGet(aggregator, out MaxStatAggregator<int, SpeedMeter<int>> aggregatorClone));
        Assert.True(context.TryGet(second, out SpeedMeter<int> secondClone));
        Assert.NotSame(aggregator, aggregatorClone);
        Assert.Same(aggregatorClone, firstClone.SpeedAggregator);
        Assert.Same(aggregatorClone, secondClone.SpeedAggregator);
        Assert.Equal(2, aggregatorClone.Meters.Count);
        Assert.Contains(aggregatorClone.Meters, meter => ReferenceEquals(meter, firstClone));
        Assert.Contains(aggregatorClone.Meters, meter => ReferenceEquals(meter, secondClone));
        //And the original aggregator still has only the originals
        Assert.Equal(2, aggregator.Meters.Count);
        Assert.Contains(aggregator.Meters, meter => ReferenceEquals(meter, first));
        Assert.Contains(aggregator.Meters, meter => ReferenceEquals(meter, second));
    }
    [Fact(DisplayName = "Cloning the aggregator first gives the same clones as cloning a SpeedMeter first")]
    public void CloningTheAggregatorFirst()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> aggregator = new();
        SpeedMeter<int> first = new(timeline, 20, new Rational<long>(-2), new Rational<long>(2), aggregator);
        SpeedMeter<int> second = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2), aggregator);
        CloneContext context = new();
        timeline.Clone(context);

        MaxStatAggregator<int, SpeedMeter<int>> aggregatorClone = (MaxStatAggregator<int, SpeedMeter<int>>)aggregator.Clone(context);

        Assert.Equal(2, aggregatorClone.Meters.Count);
        Assert.Contains(aggregatorClone.Meters, meter => ReferenceEquals(meter, first.Clone(context)));
        Assert.Contains(aggregatorClone.Meters, meter => ReferenceEquals(meter, second.Clone(context)));
        Assert.Same(aggregatorClone, aggregator.Clone(context));
    }
    [Fact(DisplayName = "Cloned speeds keep their buffs, so the clones compare to each other the way the originals do")]
    public void ClonesKeepTheirBuffs()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> aggregator = new();
        SpeedMeter<int> fast = new(timeline, 20, new Rational<long>(-2), new Rational<long>(2), aggregator);
        SpeedMeter<int> slower = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2), aggregator);
        fast.AddInstantChange(Rational<long>.One);         //40
        fast.OnAdvance(timeline, 1);
        slower.AddInstantChange(new Rational<long>(-1));   //5
        CloneContext context = new();
        timeline.Clone(context);

        SpeedMeter<int> slowerClone = (SpeedMeter<int>)slower.Clone(context);
        SpeedMeter<int> fastClone = (SpeedMeter<int>)fast.Clone(context);

        Assert.Equal(new Rational<long>(40), fastClone.GetModifiedStat());
        Assert.Equal(new Rational<long>(5), slowerClone.GetModifiedStat());
        Assert.Equal(new Rational<long>(1, 8), slowerClone.SpeedFraction);
        Assert.Equal(slower.SpeedFraction, slowerClone.SpeedFraction);
        //And the originals are as they were
        Assert.Equal(new Rational<long>(40), fast.GetModifiedStat());
        Assert.Equal(new Rational<long>(5), slower.GetModifiedStat());
    }
    [Fact(DisplayName = "Changing a cloned speed doesn't change what the original speeds are measured against")]
    public void ClonedSpeedsAreSeparate()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> aggregator = new();
        SpeedMeter<int> fast = new(timeline, 20, new Rational<long>(-2), new Rational<long>(2), aggregator);
        SpeedMeter<int> slower = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2), aggregator);
        CloneContext context = new();
        timeline.Clone(context);
        SpeedMeter<int> fastClone = (SpeedMeter<int>)fast.Clone(context);

        fastClone.AddInstantChange(Rational<long>.One); //The clone is now 40

        Assert.Equal(new Rational<long>(1, 2), slower.SpeedFraction);
        Assert.True(context.TryGet(slower, out SpeedMeter<int> slowerClone));
        Assert.Equal(new Rational<long>(1, 4), slowerClone.SpeedFraction);
    }
    [Fact(DisplayName = "A SpeedMeter can't be cloned before its timeline")]
    public void CloneNeedsTheTimelineFirst()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> aggregator = new();
        SpeedMeter<int> speed = new(timeline, 20, new Rational<long>(-2), new Rational<long>(2), aggregator);

        Assert.Throws<UnrecoverableCloneException>(() => speed.Clone(new CloneContext()));
    }
    #endregion
}
