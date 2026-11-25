using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
namespace Meters_Test;

/// <summary>
/// Tests for LinearContinuousMeter, the meter that changes smoothly at a set rate.
/// </summary>
/// <remarks>Claude written, Romaji Approved (after minor changes)</remarks>
public class LinearContinuousMeterTests
{
    #region Derivative
    [Fact(DisplayName = "A new LinearContinuousMeter isn't changing")]
    public void StartsStill()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);

        Assert.Equal(0, meter.Derivative);
        Assert.Equal(1, meter.DerivativeDenominator);

        meter.OnAdvance(timeline, 10);
        Assert.Equal(50, meter.CurrentValue);
    }
    [Fact(DisplayName = "SetDerivative replaces the derivative, and AddToDerivative adds to it")]
    public void SettingAndAddingToTheDerivative()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);

        Assert.True(meter.SetDerivative(3));
        Assert.Equal(3, meter.Derivative);

        Assert.True(meter.AddToDerivative(4));
        Assert.Equal(7, meter.Derivative);

        Assert.True(meter.AddToDerivative(-10));
        Assert.Equal(-3, meter.Derivative);

        Assert.True(meter.SetDerivative(1));
        Assert.Equal(1, meter.Derivative);
    }
    #endregion
    #region Advancing
    [Fact(DisplayName = "Advancing changes the value by the derivative for each unit of time")]
    public void AdvancingAppliesTheDerivative()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(3);

        meter.OnAdvance(timeline, 4);
        Assert.Equal(62, meter.CurrentValue);

        meter.SetDerivative(-5);
        meter.OnAdvance(timeline, 2);
        Assert.Equal(52, meter.CurrentValue);
    }
    [Fact(DisplayName = "Advancing by a negative time takes the change back off")]
    public void AdvancingBackwards()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(3);
        meter.OnAdvance(timeline, 4);

        meter.OnAdvance(timeline, -3);

        Assert.Equal(53, meter.CurrentValue);
    }
    [Fact(DisplayName = "Advancing stops the value at the maximum or minimum, and leaves the derivative as it was")]
    public void AdvancingStopsAtTheLimits()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> rising = new(timeline, 90, 0, 100);
        rising.SetDerivative(3);
        LinearContinuousMeter<int, double> falling = new(timeline, 10, 0, 100);
        falling.SetDerivative(-3);

        rising.OnAdvance(timeline, 10);
        falling.OnAdvance(timeline, 10);

        Assert.Equal(100, rising.CurrentValue);
        Assert.Equal(3, rising.Derivative);
        Assert.Equal(0, falling.CurrentValue);
        Assert.Equal(-3, falling.Derivative);
    }
    [Fact(DisplayName = "Advancing clears the recorded instant change")]
    public void AdvancingClearsTheInstantChange()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(3);
        meter.AddInstantChange(10);

        meter.OnAdvance(timeline, 2);

        Assert.Equal(0, meter.InstantChange);
        Assert.Equal(66, meter.CurrentValue);
    }
    [Fact(DisplayName = "Advancing announces how much time passed and the value before and after")]
    public void AdvancingAnnouncesTheChange()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(3);
        List<(int time, double before, double after)> announced = [];
        meter.ContinuousChangeEvt += (_, time, before, after) => announced.Add((time, before, after));

        meter.OnAdvance(timeline, 4);
        meter.OnAdvance(timeline, 2);

        Assert.Equal([(4, 50.0, 62.0), (2, 62.0, 68.0)], announced);
    }
    [Fact(DisplayName = "Advancing announces nothing when the value doesn't move")]
    public void AdvancingIsQuietWhenNothingMoves()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> still = new(timeline, 50, 0, 100);
        LinearContinuousMeter<int, double> atMaximum = new(timeline, 100, 0, 100);
        atMaximum.SetDerivative(3);
        int timesAnnounced = 0;
        still.ContinuousChangeEvt += (_, _, _, _) => timesAnnounced++;
        atMaximum.ContinuousChangeEvt += (_, _, _, _) => timesAnnounced++;

        still.OnAdvance(timeline, 4);
        atMaximum.OnAdvance(timeline, 4);

        Assert.Equal(0, timesAnnounced);
    }
    [Fact(DisplayName = "A LinearContinuousMeter refuses to be advanced by a timeline that isn't its own")]
    public void RefusesAnotherTimeline()
    {
        Timeline<int> timeline = new();
        Timeline<int> otherTimeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(3);

        Assert.Throws<InvalidOperationException>(() => meter.OnAdvance(otherTimeline, 5));
        Assert.Equal(50, meter.CurrentValue);
    }
    [Fact(DisplayName = "A meter of fractions on a whole-number timeline changes by exact fractions")]
    public void FractionsOnAWholeNumberTimeline()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, Rational<long>> meter = new(timeline, Rational<long>.Zero, new Rational<long>(-100), new Rational<long>(100));
        meter.SetDerivative(new Rational<long>(3, 10));

        meter.OnAdvance(timeline, 5);
        Assert.Equal(new Rational<long>(3, 2), meter.CurrentValue);

        meter.OnAdvance(timeline, 5);
        Assert.Equal(new Rational<long>(3), meter.CurrentValue);
    }
    [Fact(DisplayName = "A meter of fractions on a floating point timeline reads the time as the simple fraction it stands for")]
    public void FractionsOnAFloatingPointTimeline()
    {
        Timeline<double> timeline = new();
        LinearContinuousMeter<double, Rational<long>> meter = new(timeline, Rational<long>.Zero, new Rational<long>(-100), new Rational<long>(100));
        meter.SetDerivative(new Rational<long>(3, 10));

        meter.OnAdvance(timeline, 10.0 / 3.0);

        Assert.Equal(Rational<long>.One, meter.CurrentValue);
    }
    #endregion
    #region AddLerp and CancelLerp
    [Fact(DisplayName = "AddLerp adds the derivative that makes the total change over the time given, and says the whole change fits")]
    public void AddLerpSetsTheRate()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);

        Assert.True(meter.AddLerp(20, 4, out double actualChange));

        Assert.Equal(20, actualChange);
        Assert.Equal(5, meter.Derivative);
        Assert.Equal(50, meter.CurrentValue); //Nothing has happened yet

        meter.OnAdvance(timeline, 4);
        Assert.Equal(70, meter.CurrentValue);
    }
    [Fact(DisplayName = "AddLerp adds to a derivative that is already there")]
    public void AddLerpAddsToTheDerivative()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(2);

        meter.AddLerp(20, 4, out _);

        Assert.Equal(7, meter.Derivative);
    }
    [Fact(DisplayName = "AddLerp that would run into a limit still adds the full rate, but says how much will actually fit")]
    public void AddLerpIntoALimit()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 90, 0, 100);

        Assert.False(meter.AddLerp(20, 4, out double actualChange));

        Assert.Equal(10, actualChange);
        Assert.Equal(5, meter.Derivative);
    }
    [Fact(DisplayName = "AddLerp over no time at all is an instant change")]
    public void AddLerpOverNoTime()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);

        Assert.True(meter.AddLerp(20, 0, out double actualChange));

        Assert.Equal(20, actualChange);
        Assert.Equal(70, meter.CurrentValue);
        Assert.Equal(20, meter.InstantChange);
        Assert.Equal(0, meter.Derivative);
    }
    [Fact(DisplayName = "AddLerp throws for a negative time and changes nothing")]
    public void AddLerpOverNegativeTime()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);

        Assert.Throws<ArgumentException>(() => meter.AddLerp(20, -4, out _));

        Assert.Equal(0, meter.Derivative);
        Assert.Equal(50, meter.CurrentValue);
    }
    [Fact(DisplayName = "CancelLerp takes off the derivative that the matching AddLerp added")]
    public void CancelLerpUndoesAddLerp()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(2);
        meter.AddLerp(20, 4, out _);
        meter.OnAdvance(timeline, 4);

        meter.CancelLerp(20, 4);

        Assert.Equal(2, meter.Derivative);
        Assert.Equal(78, meter.CurrentValue); //The 20, and 4 time units of the 2 that was already there
    }
    #endregion
    #region Thresholds
    [Theory(DisplayName = "ThresholdReach is the distance to the threshold divided by the derivative")]
    [InlineData(80, 3)]
    [InlineData(60, 1)]
    [InlineData(51, 0)]  //A tenth of a time unit, which a whole-number timeline drops
    [InlineData(85, 3)]  //3 and a half, which a whole-number timeline drops to 3
    [InlineData(100, 5)]
    public void ReachAhead(int threshold, int expectedTime)
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(10);

        Assert.Equal(new TimeOrNever<int>(expectedTime), meter.ThresholdReach(threshold, false));
        Assert.Equal(new TimeOrNever<int>(expectedTime), meter.ThresholdPass(threshold, false));
    }
    [Fact(DisplayName = "ThresholdReach is now for the value the meter is at")]
    public void ReachWhereItIs()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(10);

        Assert.Equal(new TimeOrNever<int>(0), meter.ThresholdReach(50, false));
        Assert.Equal(new TimeOrNever<int>(0), meter.ThresholdReach(50, true));
    }
    [Fact(DisplayName = "ThresholdReach is Never for a threshold the meter is moving away from, unless looking back is allowed")]
    public void ReachBehind()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(10);

        Assert.True(meter.ThresholdReach(20, false).IsNever);
        Assert.Equal(new TimeOrNever<int>(-3), meter.ThresholdReach(20, true));
    }
    [Fact(DisplayName = "ThresholdReach works the same for a falling meter")]
    public void ReachWhenFalling()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(-10);

        Assert.Equal(new TimeOrNever<int>(3), meter.ThresholdReach(20, false));
        Assert.True(meter.ThresholdReach(80, false).IsNever);
        Assert.Equal(new TimeOrNever<int>(-3), meter.ThresholdReach(80, true));
    }
    [Fact(DisplayName = "A meter that isn't changing reaches only the value it is at, and passes nothing")]
    public void ThresholdsWhenStill()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);

        Assert.Equal(new TimeOrNever<int>(0), meter.ThresholdReach(50, false));
        Assert.True(meter.ThresholdReach(60, false).IsNever);
        Assert.True(meter.ThresholdPass(50, false).IsNever);
        Assert.True(meter.ThresholdPass(60, false).IsNever);
    }
    [Fact(DisplayName = "A meter of fractions gives a whole-number timeline the time with the fraction dropped, and a floating point one the closest time")]
    public void ReachForAMeterOfFractions()
    {
        Timeline<int> wholeTimeline = new();
        LinearContinuousMeter<int, Rational<long>> onWhole = new(wholeTimeline, Rational<long>.Zero, new Rational<long>(-100), new Rational<long>(100));
        onWhole.SetDerivative(new Rational<long>(3, 10));
        Timeline<double> floatTimeline = new();
        LinearContinuousMeter<double, Rational<long>> onFloat = new(floatTimeline, Rational<long>.Zero, new Rational<long>(-100), new Rational<long>(100));
        onFloat.SetDerivative(new Rational<long>(3, 10));

        Assert.Equal(new TimeOrNever<int>(3), onWhole.ThresholdReach(Rational<long>.One, false));
        Assert.Equal(new TimeOrNever<double>(10.0 / 3.0), onFloat.ThresholdReach(Rational<long>.One, false));
    }
    #endregion
    #region Cloning
    [Fact(DisplayName = "A clone of a LinearContinuousMeter has the same value, derivative, limits, recorded change and UUID")]
    public void CloneMatches()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 10, 100);
        meter.SetDerivative(3);
        meter.OnAdvance(timeline, 4);
        meter.AddInstantChange(-5);
        CloneContext context = new();
        Timeline<int> timelineClone = (Timeline<int>)timeline.Clone(context);

        LinearContinuousMeter<int, double> clone = (LinearContinuousMeter<int, double>)meter.Clone(context);

        Assert.NotSame(meter, clone);
        Assert.Same(timelineClone, clone.OwningTimeline);
        Assert.Equal(57, clone.CurrentValue);
        Assert.Equal(3, clone.Derivative);
        Assert.Equal(-5, clone.InstantChange);
        Assert.Equal(10, clone.MinValue);
        Assert.Equal(100, clone.MaxValue);
        Assert.Equal(meter.UUID, clone.UUID);
    }
    [Fact(DisplayName = "A clone of a LinearContinuousMeter advances separately from its original")]
    public void CloneIsSeparate()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(3);
        CloneContext context = new();
        Timeline<int> timelineClone = (Timeline<int>)timeline.Clone(context);
        LinearContinuousMeter<int, double> clone = (LinearContinuousMeter<int, double>)meter.Clone(context);

        clone.OnAdvance(timelineClone, 4);
        clone.SetDerivative(-1);

        Assert.Equal(62, clone.CurrentValue);
        Assert.Equal(50, meter.CurrentValue);
        Assert.Equal(3, meter.Derivative);
    }
    [Fact(DisplayName = "A LinearContinuousMeter can't be cloned before its timeline")]
    public void CloneNeedsTheTimelineFirst()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);

        Assert.Throws<UnrecoverableCloneException>(() => meter.Clone(new CloneContext()));
    }
    #endregion
}
