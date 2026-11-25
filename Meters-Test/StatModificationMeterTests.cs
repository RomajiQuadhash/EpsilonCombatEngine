using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
namespace Meters_Test;

/// <summary>
/// Tests for StatModificationMeter, which holds how much a stat is buffed or debuffed as a ratio and works out the stat from it.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class StatModificationMeterTests
{
    #region Making one
    [Fact(DisplayName = "A new StatModificationMeter has no buff or debuff, so the stat is its base value")]
    public void StartsUnmodified()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> meter = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2));

        Assert.Equal(10, meter.BaseStat);
        Assert.Equal(Rational<long>.Zero, meter.CurrentValue);
        Assert.Equal(Rational<long>.One, meter.GetMultiplier());
        Assert.Equal(new Rational<long>(10), meter.GetModifiedStat());
        Assert.Equal(StatModificationType.None, meter.ModificationType());
    }
    [Fact(DisplayName = "A StatModificationMeter can't be made with a base stat of zero or less")]
    public void BaseStatMustBePositive()
    {
        Timeline<int> timeline = new();

        Assert.Throws<ArgumentException>(() => new StatModificationMeter<int>(timeline, 0, new Rational<long>(-2), new Rational<long>(2)));
        Assert.Throws<ArgumentException>(() => new StatModificationMeter<int>(timeline, -5, new Rational<long>(-2), new Rational<long>(2)));
    }
    [Fact(DisplayName = "A StatModificationMeter can't be made with limits that don't have zero between them, since it starts at zero")]
    public void LimitsMustIncludeZero()
    {
        Timeline<int> timeline = new();

        Assert.Throws<ArgumentException>(() => new StatModificationMeter<int>(timeline, 10, new Rational<long>(1), new Rational<long>(2)));
        Assert.Throws<ArgumentException>(() => new StatModificationMeter<int>(timeline, 10, new Rational<long>(-2), new Rational<long>(-1)));
    }
    #endregion
    #region Working out the stat
    [Theory(DisplayName = "A buff multiplies the stat by 1 plus the ratio")]
    [InlineData(1, 2, 15, 1)]  //+1/2 makes 10 into 15
    [InlineData(1, 1, 20, 1)]  //+1 doubles it
    [InlineData(1, 4, 25, 2)]  //+1/4 makes 10 into 12 and a half
    public void BuffMultiplies(long ratioNumerator, long ratioDenominator, long statNumerator, long statDenominator)
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> meter = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2));

        meter.AddInstantChange(new Rational<long>(ratioNumerator, ratioDenominator));

        Assert.Equal(new Rational<long>(statNumerator, statDenominator), meter.GetModifiedStat());
        Assert.Equal(StatModificationType.Buff, meter.ModificationType());
    }
    [Theory(DisplayName = "A debuff divides the stat by 1 plus the size of the ratio")]
    [InlineData(-1, 2, 20, 3)]  //-1/2 divides 10 by 1 and a half
    [InlineData(-1, 1, 5, 1)]   //-1 halves it
    [InlineData(-2, 1, 10, 3)]  //-2 leaves a third
    public void DebuffDivides(long ratioNumerator, long ratioDenominator, long statNumerator, long statDenominator)
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> meter = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2));

        meter.AddInstantChange(new Rational<long>(ratioNumerator, ratioDenominator));

        Assert.Equal(new Rational<long>(statNumerator, statDenominator), meter.GetModifiedStat());
        Assert.Equal(StatModificationType.Debuff, meter.ModificationType());
    }
    [Fact(DisplayName = "A buff and a debuff of the same size are opposites: one multiplies by what the other divides by")]
    public void BuffAndDebuffAreOpposites()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> buffed = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2));
        StatModificationMeter<int> debuffed = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2));

        buffed.AddInstantChange(new Rational<long>(3, 4));
        debuffed.AddInstantChange(new Rational<long>(-3, 4));

        Assert.Equal(Rational<long>.One, buffed.GetMultiplier() * debuffed.GetMultiplier());
    }
    [Fact(DisplayName = "A buff and a debuff that cancel out leave the stat at its base value")]
    public void CancellingOutLeavesTheBase()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> meter = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2));

        meter.AddInstantChange(new Rational<long>(3, 4));
        meter.AddInstantChange(new Rational<long>(-3, 4));

        Assert.Equal(new Rational<long>(10), meter.GetModifiedStat());
        Assert.Equal(StatModificationType.None, meter.ModificationType());
    }
    [Fact(DisplayName = "The ratio stops at the meter's limits, so the stat can't be buffed or debuffed past them")]
    public void RatioStopsAtTheLimits()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> meter = new(timeline, 10, new Rational<long>(-1), new Rational<long>(1));

        Assert.False(meter.AddInstantChange(new Rational<long>(5)));
        Assert.Equal(new Rational<long>(20), meter.GetModifiedStat());

        Assert.False(meter.AddInstantChange(new Rational<long>(-50)));
        Assert.Equal(new Rational<long>(5), meter.GetModifiedStat());
    }
    #endregion
    #region Changing the limits
    [Fact(DisplayName = "The minimum can be moved anywhere at or below zero, but not above it")]
    public void MinimumStaysAtOrBelowZero()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> meter = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2));

        Assert.True(meter.UpdateMin(new Rational<long>(-5), false));
        Assert.True(meter.UpdateMin(Rational<long>.Zero, false));
        Assert.Throws<ArgumentException>(() => meter.UpdateMin(new Rational<long>(1, 2), true));

        Assert.Equal(Rational<long>.Zero, meter.MinValue);
    }
    [Fact(DisplayName = "The maximum can be moved anywhere at or above zero, but not below it")]
    public void MaximumStaysAtOrAboveZero()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> meter = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2));

        Assert.True(meter.UpdateMax(new Rational<long>(5), false));
        Assert.True(meter.UpdateMax(Rational<long>.Zero, false));
        Assert.Throws<ArgumentException>(() => meter.UpdateMax(new Rational<long>(-1, 2), true));

        Assert.Equal(Rational<long>.Zero, meter.MaxValue);
    }
    [Fact(DisplayName = "Lowering the maximum below a buff pushes the buff down to it when clamping is allowed")]
    public void LoweringTheMaximumCutsABuff()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> meter = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2));
        meter.AddInstantChange(new Rational<long>(3, 2));

        Assert.False(meter.UpdateMax(Rational<long>.One, false));
        Assert.Equal(new Rational<long>(25), meter.GetModifiedStat());

        Assert.True(meter.UpdateMax(Rational<long>.One, true));
        Assert.Equal(new Rational<long>(20), meter.GetModifiedStat());
    }
    #endregion
    #region Cloning
    [Fact(DisplayName = "A clone keeps a buff that was applied before the last advance, and the original keeps it too")]
    public void CloneKeepsASettledBuff()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> meter = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2));
        meter.AddInstantChange(new Rational<long>(1, 2));
        meter.OnAdvance(timeline, 1); //The buff is no longer a recorded instant change
        CloneContext context = new();
        timeline.Clone(context);

        StatModificationMeter<int> clone = (StatModificationMeter<int>)meter.Clone(context);

        Assert.Equal(new Rational<long>(1, 2), clone.CurrentValue);
        Assert.Equal(Rational<long>.Zero, clone.InstantChange);
        Assert.Equal(new Rational<long>(15), clone.GetModifiedStat());
        Assert.Equal(new Rational<long>(1, 2), meter.CurrentValue);
        Assert.Equal(Rational<long>.Zero, meter.InstantChange);
    }
    [Fact(DisplayName = "A clone keeps a buff that has just been applied, along with it being a recorded instant change, and the original keeps it too")]
    public void CloneKeepsAFreshBuff()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> meter = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2));
        meter.AddInstantChange(new Rational<long>(1, 2));
        CloneContext context = new();
        timeline.Clone(context);

        StatModificationMeter<int> clone = (StatModificationMeter<int>)meter.Clone(context);

        Assert.Equal(new Rational<long>(1, 2), clone.CurrentValue);
        Assert.Equal(new Rational<long>(1, 2), clone.InstantChange);
        Assert.Equal(new Rational<long>(1, 2), meter.CurrentValue);
        Assert.Equal(new Rational<long>(1, 2), meter.InstantChange);
    }
    [Fact(DisplayName = "A clone keeps both an older buff and a fresh one, and the original keeps both too")]
    public void CloneKeepsSettledAndFreshBuffsTogether()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> meter = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2));
        meter.AddInstantChange(new Rational<long>(1, 2));
        meter.OnAdvance(timeline, 1);
        meter.AddInstantChange(new Rational<long>(1, 4));
        CloneContext context = new();
        timeline.Clone(context);

        StatModificationMeter<int> clone = (StatModificationMeter<int>)meter.Clone(context);

        Assert.Equal(new Rational<long>(3, 4), clone.CurrentValue);
        Assert.Equal(new Rational<long>(1, 4), clone.InstantChange);
        Assert.Equal(new Rational<long>(3, 4), meter.CurrentValue);
        Assert.Equal(new Rational<long>(1, 4), meter.InstantChange);
    }
    [Fact(DisplayName = "A clone of a StatModificationMeter has the same base stat, limits and UUID, on the cloned timeline")]
    public void CloneMatches()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> meter = new(timeline, 10, new Rational<long>(-2), new Rational<long>(3));
        CloneContext context = new();
        Timeline<int> timelineClone = (Timeline<int>)timeline.Clone(context);

        StatModificationMeter<int> clone = (StatModificationMeter<int>)meter.Clone(context);

        Assert.NotSame(meter, clone);
        Assert.Same(timelineClone, clone.OwningTimeline);
        Assert.Equal(10, clone.BaseStat);
        Assert.Equal(new Rational<long>(-2), clone.MinValue);
        Assert.Equal(new Rational<long>(3), clone.MaxValue);
        Assert.Equal(meter.UUID, clone.UUID);
        Assert.Same(clone, meter.Clone(context));
    }
    [Fact(DisplayName = "A StatModificationMeter can't be cloned before its timeline")]
    public void CloneNeedsTheTimelineFirst()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> meter = new(timeline, 10, new Rational<long>(-2), new Rational<long>(2));

        Assert.Throws<UnrecoverableCloneException>(() => meter.Clone(new CloneContext()));
    }
    #endregion
}
