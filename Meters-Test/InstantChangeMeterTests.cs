using CoreMetrics;
using Meters;
using Timeline;
namespace Meters_Test;

/// <summary>
/// Tests for InstantChangeMeter, the meter that only changes in jumps and that every other meter is built on.
/// Changing its minimum and maximum is tested in MeterLimitTests.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class InstantChangeMeterTests
{
    #region Making one
    [Fact(DisplayName = "A new InstantChangeMeter has the value and limits it was given and no change recorded")]
    public void StartsAsGiven()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 10, 100);

        Assert.Same(timeline, meter.OwningTimeline);
        Assert.Equal(50, meter.CurrentValue);
        Assert.Equal(10, meter.MinValue);
        Assert.Equal(100, meter.MaxValue);
        Assert.Equal(90, meter.Range);
        Assert.Equal(0, meter.InstantChange);
        Assert.Equal(0, meter.Derivative);
        Assert.Equal(1, meter.DerivativeDenominator);
    }
    [Fact(DisplayName = "An InstantChangeMeter can start on its minimum or its maximum")]
    public void CanStartOnALimit()
    {
        Timeline<int> timeline = new();

        Assert.Equal(10, new InstantChangeMeter<int, int>(timeline, 10, 10, 100).CurrentValue);
        Assert.Equal(100, new InstantChangeMeter<int, int>(timeline, 100, 10, 100).CurrentValue);
    }
    [Fact(DisplayName = "An InstantChangeMeter can't be made with a maximum that isn't above its minimum")]
    public void MaximumMustBeAboveMinimum()
    {
        Timeline<int> timeline = new();

        Assert.Throws<ArgumentException>(() => new InstantChangeMeter<int, int>(timeline, 10, 10, 10));
        Assert.Throws<ArgumentException>(() => new InstantChangeMeter<int, int>(timeline, 10, 20, 5));
    }
    [Fact(DisplayName = "An InstantChangeMeter can't be made with a starting value outside its limits")]
    public void StartingValueMustBeInsideTheLimits()
    {
        Timeline<int> timeline = new();

        Assert.Throws<ArgumentException>(() => new InstantChangeMeter<int, int>(timeline, 9, 10, 100));
        Assert.Throws<ArgumentException>(() => new InstantChangeMeter<int, int>(timeline, 101, 10, 100));
    }
    [Fact(DisplayName = "A new InstantChangeMeter is listening to its timeline's advances")]
    public void ListensToItsTimeline()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.Contains(meter, timeline.GetAdvanceSubscribers());
    }
    #endregion
    #region AddInstantChange
    [Fact(DisplayName = "AddInstantChange changes the value, records the change, and says the whole change fit")]
    public void AddInstantChangeChangesTheValue()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.True(meter.AddInstantChange(20));
        Assert.Equal(70, meter.CurrentValue);
        Assert.Equal(20, meter.InstantChange);

        Assert.True(meter.AddInstantChange(-45));
        Assert.Equal(25, meter.CurrentValue);
        Assert.Equal(-25, meter.InstantChange); //Both changes together
    }
    [Fact(DisplayName = "AddInstantChange stops at the maximum, records only what fit, and says the whole change didn't fit")]
    public void AddInstantChangeStopsAtTheMaximum()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 90, 0, 100);

        Assert.False(meter.AddInstantChange(25));

        Assert.Equal(100, meter.CurrentValue);
        Assert.Equal(10, meter.InstantChange);
    }
    [Fact(DisplayName = "AddInstantChange stops at the minimum, records only what fit, and says the whole change didn't fit")]
    public void AddInstantChangeStopsAtTheMinimum()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 10, 0, 100);

        Assert.False(meter.AddInstantChange(-25));

        Assert.Equal(0, meter.CurrentValue);
        Assert.Equal(-10, meter.InstantChange);
    }
    [Fact(DisplayName = "AddInstantChange that lands exactly on a limit counts as fitting")]
    public void AddInstantChangeOntoALimitFits()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 90, 0, 100);

        Assert.True(meter.AddInstantChange(10));

        Assert.Equal(100, meter.CurrentValue);
    }
    [Fact(DisplayName = "AddInstantChange announces the value before and after")]
    public void AddInstantChangeAnnouncesTheChange()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 90, 0, 100);
        List<(int before, int after)> announced = [];
        meter.InstantChangeEvt += (_, before, after) => announced.Add((before, after));

        meter.AddInstantChange(-30);
        meter.AddInstantChange(100); //Only 40 of it fits

        Assert.Equal([(90, 60), (60, 100)], announced);
    }
    [Fact(DisplayName = "AddInstantChange announces nothing when the value doesn't actually move")]
    public void AddInstantChangeIsQuietWhenNothingMoves()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 100, 0, 100);
        int timesAnnounced = 0;
        meter.InstantChangeEvt += (_, _, _) => timesAnnounced++;

        meter.AddInstantChange(0);
        meter.AddInstantChange(15); //Already at the maximum

        Assert.Equal(0, timesAnnounced);
        Assert.Equal(0, meter.InstantChange);
    }
    #endregion
    #region Advancing
    [Fact(DisplayName = "Advancing clears the recorded instant change and leaves the value where it is")]
    public void AdvancingClearsTheInstantChange()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        meter.AddInstantChange(20);

        meter.OnAdvance(timeline, 5);

        Assert.Equal(0, meter.InstantChange);
        Assert.Equal(70, meter.CurrentValue);
    }
    [Fact(DisplayName = "Advancing by no time at all still clears the recorded instant change")]
    public void AdvancingByNothingStillClears()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        meter.AddInstantChange(20);

        meter.OnAdvance(timeline, 0);

        Assert.Equal(0, meter.InstantChange);
    }
    [Fact(DisplayName = "An InstantChangeMeter refuses to be advanced by a timeline that isn't its own")]
    public void RefusesAnotherTimeline()
    {
        Timeline<int> timeline = new();
        Timeline<int> otherTimeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        meter.AddInstantChange(20);

        Assert.Throws<InvalidOperationException>(() => meter.OnAdvance(otherTimeline, 5));
        Assert.Throws<InvalidOperationException>(() => meter.OnAdvance(null, 5));
        Assert.Equal(20, meter.InstantChange);
    }
    #endregion
    #region Thresholds
    [Fact(DisplayName = "With no instant change, ThresholdReach is now for the current value and Never for anything else")]
    public void ReachWithNoChange()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.Equal(new TimeOrNever<int>(0), meter.ThresholdReach(50, false));
        Assert.True(meter.ThresholdReach(49, false).IsNever);
        Assert.True(meter.ThresholdReach(51, false).IsNever);
    }
    [Fact(DisplayName = "With no instant change, ThresholdPass is Never, even for the current value")]
    public void PassWithNoChange()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.True(meter.ThresholdPass(50, false).IsNever);
        Assert.True(meter.ThresholdPass(60, false).IsNever);
    }
    [Theory(DisplayName = "After a jump from 50 to 70, ThresholdReach is now for everything from 50 to 70 and Never outside that")]
    [InlineData(49, false)]
    [InlineData(50, true)]
    [InlineData(60, true)]
    [InlineData(70, true)]
    [InlineData(71, false)]
    public void ReachAfterAJump(int threshold, bool reached)
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        meter.AddInstantChange(20);

        Assert.Equal(!reached, meter.ThresholdReach(threshold, false).IsNever);
    }
    [Theory(DisplayName = "After a jump from 50 to 70, ThresholdPass is now only for what is strictly between them")]
    [InlineData(49, false)]
    [InlineData(50, false)]
    [InlineData(51, true)]
    [InlineData(69, true)]
    [InlineData(70, false)]
    [InlineData(71, false)]
    public void PassAfterAJump(int threshold, bool passed)
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        meter.AddInstantChange(20);

        Assert.Equal(!passed, meter.ThresholdPass(threshold, false).IsNever);
    }
    [Fact(DisplayName = "A jump stops counting as a crossing once time has advanced")]
    public void JumpIsForgottenAfterAdvancing()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        meter.AddInstantChange(20);

        meter.OnAdvance(timeline, 1);

        Assert.True(meter.ThresholdReach(60, false).IsNever);
        Assert.True(meter.ThresholdPass(60, false).IsNever);
        Assert.Equal(new TimeOrNever<int>(0), meter.ThresholdReach(70, false)); //It is still sitting on 70
    }
    [Theory(DisplayName = "DidInstantlyCross is true strictly between the before and after values, and at either end only if touching counts")]
    [InlineData(60, false, true)]
    [InlineData(60, true, true)]
    [InlineData(50, false, false)]
    [InlineData(50, true, true)]
    [InlineData(70, false, false)]
    [InlineData(70, true, true)]
    [InlineData(80, false, false)]
    [InlineData(80, true, false)]
    public void DidInstantlyCross(int threshold, bool includeTouch, bool expected)
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        meter.AddInstantChange(20);

        Assert.Equal(expected, meter.DidInstantlyCross(threshold, includeTouch));
    }
    [Fact(DisplayName = "DidInstantlyCross works the same for a jump downward")]
    public void DidInstantlyCrossDownward()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        meter.AddInstantChange(-20);

        Assert.True(meter.DidInstantlyCross(40, false));
        Assert.False(meter.DidInstantlyCross(30, false));
        Assert.True(meter.DidInstantlyCross(30, true));
        Assert.False(meter.DidInstantlyCross(60, true));
    }
    #endregion
    #region Equality and cloning
    [Fact(DisplayName = "Two meters are equal only if they are the same kind and have the same UUID, whatever their values")]
    public void EqualityIsByTypeAndUUID()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        InstantChangeMeter<int, int> sameUUID = new(timeline, 3, -5, 5) { UUID = meter.UUID };
        InstantChangeMeter<int, int> differentUUID = new(timeline, 50, 0, 100);
        LinearDiscreteMeter<int, int> differentKind = new(timeline, 50, 0, 100) { UUID = meter.UUID };

        Assert.Equal(meter, sameUUID);
        Assert.Equal(meter.GetHashCode(), sameUUID.GetHashCode());
        Assert.NotEqual(meter, differentUUID);
        Assert.False(meter.Equals(differentKind));
    }
    [Fact(DisplayName = "An InstantChangeMeter can't be cloned before its timeline")]
    public void CloneNeedsTheTimelineFirst()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.Throws<UnrecoverableCloneException>(() => meter.Clone(new CloneContext()));
    }
    [Fact(DisplayName = "A clone of an InstantChangeMeter has the same value, limits, recorded change and UUID, on the cloned timeline")]
    public void CloneMatches()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 10, 100);
        meter.AddInstantChange(20);
        meter.OnAdvance(timeline, 1);
        meter.AddInstantChange(-5); //So it has both a change that was cleared and one that is still recorded
        CloneContext context = new();
        Timeline<int> timelineClone = (Timeline<int>)timeline.Clone(context);

        InstantChangeMeter<int, int> clone = (InstantChangeMeter<int, int>)meter.Clone(context);

        Assert.NotSame(meter, clone);
        Assert.Same(timelineClone, clone.OwningTimeline);
        Assert.Equal(65, clone.CurrentValue);
        Assert.Equal(-5, clone.InstantChange);
        Assert.Equal(10, clone.MinValue);
        Assert.Equal(100, clone.MaxValue);
        Assert.Equal(meter.UUID, clone.UUID);
        Assert.Contains(clone, timelineClone.GetAdvanceSubscribers());
        //And the original is as it was
        Assert.Equal(65, meter.CurrentValue);
        Assert.Equal(-5, meter.InstantChange);
    }
    [Fact(DisplayName = "Cloning an InstantChangeMeter twice with the same context gives the same clone")]
    public void CloneTakesFromContext()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        CloneContext context = new();
        timeline.Clone(context);

        Assert.Same(meter.Clone(context), meter.Clone(context));
    }
    [Fact(DisplayName = "A clone of an InstantChangeMeter changes separately from its original")]
    public void CloneIsSeparate()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        CloneContext context = new();
        timeline.Clone(context);
        InstantChangeMeter<int, int> clone = (InstantChangeMeter<int, int>)meter.Clone(context);

        clone.AddInstantChange(20);
        meter.AddInstantChange(-10);

        Assert.Equal(70, clone.CurrentValue);
        Assert.Equal(40, meter.CurrentValue);
    }
    #endregion
}
