using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
namespace Meters_Test;

/// <summary>
/// Tests for LinearDiscreteMeter, the meter that changes by a whole amount every so often.
/// A threshold being jumped over by a tick is tested in DiscreteThresholdSkipOverTests.
/// </summary>
/// <remarks>Claude written, Romaji approved after comment change</remarks>
public class LinearDiscreteMeterTests
{
    #region SetDerivative
    [Fact(DisplayName = "A new LinearDiscreteMeter isn't changing, and has a tick rate of 1 so that nothing divides by zero")]
    public void StartsStill()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.Equal(0, meter.Derivative);
        Assert.Equal(1, meter.DerivativeDenominator);
        Assert.Equal(0, meter.TimeSinceLastIncrement);
    }
    [Fact(DisplayName = "SetDerivative sets how much each tick adds and how long is between ticks")]
    public void SetDerivativeSetsBoth()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.True(meter.SetDerivative(5, 10, out Rational<long> spillover));

        Assert.Equal(5, meter.Derivative);
        Assert.Equal(10, meter.DerivativeDenominator);
        Assert.Equal(Rational<long>.Zero, spillover);
    }
    [Fact(DisplayName = "SetDerivative throws for a tick rate of zero or less and changes nothing")]
    public void SetDerivativeNeedsAPositiveTickRate()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(5, 10, out _);

        Assert.Throws<ArgumentException>(() => meter.SetDerivative(3, 0, out _));
        Assert.Throws<ArgumentException>(() => meter.SetDerivative(3, -2, out _));

        Assert.Equal(5, meter.Derivative);
        Assert.Equal(10, meter.DerivativeDenominator);
    }
    [Fact(DisplayName = "SetDerivative partway through a tick starts a new tick and hands back what the old one had built up")]
    public void SetDerivativeMidTickHandsBackWhatWasBuiltUp()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(5, 10, out _);
        meter.OnAdvance(timeline, 4);

        meter.SetDerivative(1, 3, out Rational<long> spillover);

        Assert.Equal(new Rational<long>(2), spillover); //4 tenths of a tick of 5
        Assert.Equal(0, meter.TimeSinceLastIncrement);
        Assert.Equal(50, meter.CurrentValue); //It isn't added to the meter
    }
    [Fact(DisplayName = "What SetDerivative hands back can be a fraction, and is negative for a falling meter")]
    public void SetDerivativeHandsBackFractions()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(-5, 3, out _);
        meter.OnAdvance(timeline, 1);

        meter.SetDerivative(1, 3, out Rational<long> spillover);

        Assert.Equal(new Rational<long>(-5, 3), spillover);
    }
    #endregion
    #region Advancing
    [Fact(DisplayName = "The value only changes when a whole tick has passed")]
    public void ValueChangesOnlyOnATick()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 0, -100, 100);
        meter.SetDerivative(5, 10, out _);

        meter.OnAdvance(timeline, 9);
        Assert.Equal(0, meter.CurrentValue);
        Assert.Equal(9, meter.TimeSinceLastIncrement);

        meter.OnAdvance(timeline, 1);
        Assert.Equal(5, meter.CurrentValue);
        Assert.Equal(0, meter.TimeSinceLastIncrement);
    }
    [Fact(DisplayName = "One advance can land several ticks and leave part of the next one built up")]
    public void SeveralTicksInOneAdvance()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 0, -100, 100);
        meter.SetDerivative(5, 10, out _);

        meter.OnAdvance(timeline, 37);

        Assert.Equal(15, meter.CurrentValue);
        Assert.Equal(7, meter.TimeSinceLastIncrement);
    }
    [Fact(DisplayName = "Advancing by a negative time takes ticks back off")]
    public void AdvancingBackwards()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 0, -100, 100);
        meter.SetDerivative(5, 10, out _);
        meter.OnAdvance(timeline, 37);

        meter.OnAdvance(timeline, -8); //Back to 29, which is before the third tick
        Assert.Equal(10, meter.CurrentValue);
        Assert.Equal(9, meter.TimeSinceLastIncrement);

        meter.OnAdvance(timeline, -29); //All the way back
        Assert.Equal(0, meter.CurrentValue);
        Assert.Equal(0, meter.TimeSinceLastIncrement);
    }
    [Fact(DisplayName = "A tick that would go past a limit stops at it")]
    public void TicksStopAtTheLimits()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> rising = new(timeline, 97, 0, 100);
        rising.SetDerivative(5, 10, out _);
        LinearDiscreteMeter<int, int> falling = new(timeline, 3, 0, 100);
        falling.SetDerivative(-5, 10, out _);

        rising.OnAdvance(timeline, 10);
        falling.OnAdvance(timeline, 10);

        Assert.Equal(100, rising.CurrentValue);
        Assert.Equal(0, falling.CurrentValue);
    }
    [Fact(DisplayName = "A meter that isn't changing doesn't build up time toward a tick")]
    public void StillMeterBuildsNothingUp()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 50, 0, 100);

        meter.OnAdvance(timeline, 7);

        Assert.Equal(0, meter.TimeSinceLastIncrement);
        Assert.Equal(50, meter.CurrentValue);
    }
    [Fact(DisplayName = "Advancing clears the recorded instant change")]
    public void AdvancingClearsTheInstantChange()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(5, 10, out _);
        meter.AddInstantChange(10);

        meter.OnAdvance(timeline, 1);

        Assert.Equal(0, meter.InstantChange);
        Assert.Equal(60, meter.CurrentValue);
    }
    [Fact(DisplayName = "An advance announces the whole change once, however many ticks landed, and nothing if none did")]
    public void AnnouncesOncePerAdvance()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 0, -100, 100);
        meter.SetDerivative(5, 10, out _);
        List<(int time, int before, int after)> announced = [];
        meter.ContinuousChangeEvt += (_, time, before, after) => announced.Add((time, before, after));

        meter.OnAdvance(timeline, 4);  //No tick
        meter.OnAdvance(timeline, 30); //Three ticks
        meter.OnAdvance(timeline, 5);  //No tick

        Assert.Equal([(30, 0, 15)], announced);
    }
    [Fact(DisplayName = "On a floating point timeline, advancing by the time a tick was predicted at lands exactly on that tick")]
    public void LandsExactlyOnPredictedTicks()
    {
        Random random = new(1);
        int misses = 0;
        for (int trial = 0; trial < 5000; trial++)
        {
            Timeline<double> timeline = new();
            LinearDiscreteMeter<double, int> meter = new(timeline, 0, -1000000, 1000000);
            double tickRate = random.NextDouble() * 10 + 0.01;
            meter.SetDerivative(1, tickRate, out _);
            meter.OnAdvance(timeline, random.NextDouble() * tickRate * 0.999); //Partway into a tick, by an awkward amount
            int ticks = random.Next(1, 6);
            int before = meter.CurrentValue;

            meter.OnAdvance(timeline, meter.ThresholdReach(before + ticks, false).Time);

            if (meter.TimeSinceLastIncrement != 0 || meter.CurrentValue != before + ticks)
            {
                misses++;
            }
        }

        Assert.Equal(0, misses);
    }
    #endregion
    #region ThresholdReach
    [Theory(DisplayName = "ThresholdReach is the tick that first gets to or past the threshold")]
    [InlineData(5, 10)]   //Landed on by the first tick
    [InlineData(1, 10)]   //Passed by the first tick
    [InlineData(6, 20)]   //Passed by the second tick
    [InlineData(10, 20)]  //Landed on by the second tick
    [InlineData(21, 50)]  //Passed by the fifth tick
    public void ReachAhead(int threshold, int expectedTime)
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 0, -100, 100);
        meter.SetDerivative(5, 10, out _);

        Assert.Equal(new TimeOrNever<int>(expectedTime), meter.ThresholdReach(threshold, false));
    }
    [Fact(DisplayName = "ThresholdReach counts from where the tick in progress has got to")]
    public void ReachPartwayThroughATick()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 0, -100, 100);
        meter.SetDerivative(5, 10, out _);
        meter.OnAdvance(timeline, 4);

        Assert.Equal(new TimeOrNever<int>(6), meter.ThresholdReach(5, false));
        Assert.Equal(new TimeOrNever<int>(16), meter.ThresholdReach(6, false));
    }
    [Fact(DisplayName = "ThresholdReach works the same for a falling meter")]
    public void ReachWhenFalling()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 0, -100, 100);
        meter.SetDerivative(-5, 10, out _);

        Assert.Equal(new TimeOrNever<int>(10), meter.ThresholdReach(-5, false));
        Assert.Equal(new TimeOrNever<int>(20), meter.ThresholdReach(-6, false));
        Assert.True(meter.ThresholdReach(5, false).IsNever);
    }
    [Fact(DisplayName = "ThresholdReach is now for the value the meter is at, or when it got there if looking back is allowed")]
    public void ReachWhereItIs()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 0, -100, 100);
        meter.SetDerivative(5, 10, out _);
        meter.OnAdvance(timeline, 13); //At 5 since the tick at 10

        Assert.Equal(new TimeOrNever<int>(0), meter.ThresholdReach(5, false));
        Assert.Equal(new TimeOrNever<int>(-3), meter.ThresholdReach(5, true));
    }
    [Fact(DisplayName = "ThresholdReach is Never for a threshold behind the meter, unless looking back is allowed")]
    public void ReachBehind()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 0, -100, 100);
        meter.SetDerivative(5, 10, out _);
        meter.OnAdvance(timeline, 33); //At 15 since the tick at 30

        Assert.True(meter.ThresholdReach(10, false).IsNever);
        Assert.Equal(new TimeOrNever<int>(-13), meter.ThresholdReach(10, true)); //Landed on by the tick at 20
        Assert.Equal(new TimeOrNever<int>(-3), meter.ThresholdReach(11, true));  //Passed by the tick at 30
        Assert.Equal(new TimeOrNever<int>(-23), meter.ThresholdReach(1, true));  //Passed by the tick at 10
    }
    [Fact(DisplayName = "A meter that isn't changing reaches only the value it is at")]
    public void ReachWhenStill()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.Equal(new TimeOrNever<int>(0), meter.ThresholdReach(50, false));
        Assert.True(meter.ThresholdReach(55, false).IsNever);
    }
    #endregion
    #region ThresholdPass
    [Fact(DisplayName = "ThresholdPass is the tick after the one that lands exactly on the threshold")]
    public void PassAfterLandingOnIt()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 0, -100, 100);
        meter.SetDerivative(5, 10, out _);

        Assert.Equal(new TimeOrNever<int>(20), meter.ThresholdPass(5, false));
        Assert.Equal(new TimeOrNever<int>(30), meter.ThresholdPass(10, false));
    }
    [Fact(DisplayName = "ThresholdPass is the same tick as ThresholdReach when that tick jumps over the threshold")]
    public void PassWhenJumpedOver()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 0, -100, 100);
        meter.SetDerivative(5, 10, out _);

        Assert.Equal(new TimeOrNever<int>(10), meter.ThresholdPass(3, false));
        Assert.Equal(new TimeOrNever<int>(20), meter.ThresholdPass(7, false));
    }
    [Fact(DisplayName = "ThresholdPass for the value the meter is at is the next tick, when it leaves")]
    public void PassWhereItIs()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 0, -100, 100);
        meter.SetDerivative(5, 10, out _);
        meter.OnAdvance(timeline, 13);

        Assert.Equal(new TimeOrNever<int>(7), meter.ThresholdPass(5, false));
    }
    [Fact(DisplayName = "A meter that isn't changing passes nothing")]
    public void PassWhenStill()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.True(meter.ThresholdPass(50, false).IsNever);
        Assert.True(meter.ThresholdPass(55, false).IsNever);
    }
    #endregion
    #region Cloning
    [Fact(DisplayName = "A clone of a LinearDiscreteMeter has the same value, schedule, place in its tick, limits and UUID")]
    public void CloneMatches()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 50, 10, 100);
        meter.SetDerivative(5, 10, out _);
        meter.OnAdvance(timeline, 14);
        meter.AddInstantChange(-3);
        CloneContext context = new();
        Timeline<int> timelineClone = (Timeline<int>)timeline.Clone(context);

        LinearDiscreteMeter<int, int> clone = (LinearDiscreteMeter<int, int>)meter.Clone(context);

        Assert.NotSame(meter, clone);
        Assert.Same(timelineClone, clone.OwningTimeline);
        Assert.Equal(52, clone.CurrentValue);
        Assert.Equal(-3, clone.InstantChange);
        Assert.Equal(5, clone.Derivative);
        Assert.Equal(10, clone.DerivativeDenominator);
        Assert.Equal(4, clone.TimeSinceLastIncrement);
        Assert.Equal(10, clone.MinValue);
        Assert.Equal(100, clone.MaxValue);
        Assert.Equal(meter.UUID, clone.UUID);
    }
    [Fact(DisplayName = "A clone of a LinearDiscreteMeter ticks separately from its original")]
    public void CloneIsSeparate()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 0, -100, 100);
        meter.SetDerivative(5, 10, out _);
        meter.OnAdvance(timeline, 4);
        CloneContext context = new();
        Timeline<int> timelineClone = (Timeline<int>)timeline.Clone(context);
        LinearDiscreteMeter<int, int> clone = (LinearDiscreteMeter<int, int>)meter.Clone(context);

        clone.OnAdvance(timelineClone, 6);

        Assert.Equal(5, clone.CurrentValue);
        Assert.Equal(0, clone.TimeSinceLastIncrement);
        Assert.Equal(0, meter.CurrentValue);
        Assert.Equal(4, meter.TimeSinceLastIncrement);
    }
    [Fact(DisplayName = "A LinearDiscreteMeter can't be cloned before its timeline")]
    public void CloneNeedsTheTimelineFirst()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.Throws<UnrecoverableCloneException>(() => meter.Clone(new CloneContext()));
    }
    #endregion
}
