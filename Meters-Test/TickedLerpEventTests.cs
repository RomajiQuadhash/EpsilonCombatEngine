using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Meters_Test;

/// <summary>
/// Tests for TempRMMChange.CreateEventFromTickedLerp, which makes the right event for changing a RateManagedMeter by a set amount
/// over a set length of time in a set number of ticks.
/// </summary>
/// <remarks>Claude written, Romaji approved after documentation changes.</remarks>
public class TickedLerpEventTests
{
    #region Which event is made
    [Fact(DisplayName = "A change over some time makes a TempRMMChange whose ModInstance is one tick's share of the change and one tick's share of the time")]
    public void ChangeOverTimeMakesATempRMMChange()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        IMeterChangeEvent<int, int> made = TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, 20, 3, 10, 5, TickRounding.TowardZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out int actualChange);

        TempRMMChange<int, int> change = Assert.IsType<TempRMMChange<int, int>>(made);
        Assert.Same(meter, change.MeterAppliedTo);
        Assert.Equal(new ModInstance<int, int>(4, 2), change.Modifier);
        Assert.Equal(3, change.WaitingTime);
        Assert.Equal(10, change.ApplyTime);
        Assert.Equal(PriorityRank.MeterChangePhaseStart, change.ApplyPriority);
        Assert.Equal(PriorityRank.MeterChangePhaseEnd, change.RemovePriority);
        Assert.Equal(20, actualChange);
    }
    [Fact(DisplayName = "A change over no time at all makes an InstantChangeEvent for the whole change, and the ticks and rounding aren't used")]
    public void ChangeOverNoTimeMakesAnInstantChangeEvent()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        IMeterChangeEvent<int, int> made = TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, 10, 3, 0, 4, TickRounding.TowardZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out int actualChange);

        InstantChangeEvent<int, int> change = Assert.IsType<InstantChangeEvent<int, int>>(made);
        Assert.Same(meter, change.MeterAppliedTo);
        Assert.Equal(10, change.Change); //All 10, where four ticks rounded toward zero would only have made 8
        Assert.Equal(new TimeOrNever<int>(3), change.TimeRemaining);
        Assert.Equal(PriorityRank.MeterChangePhaseStart, change.Priority);
        Assert.Equal(10, actualChange);
    }
    [Fact(DisplayName = "A change over no time doesn't care if the number of ticks makes no sense")]
    public void ChangeOverNoTimeIgnoresTheTicks()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        IMeterChangeEvent<int, int> made = TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, 10, 3, 0, 0, TickRounding.Nearest,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _);

        Assert.IsType<InstantChangeEvent<int, int>>(made);
    }
    [Fact(DisplayName = "One tick puts the whole change at the end of the time")]
    public void OneTick()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        IMeterChangeEvent<int, int> made = TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, 20, 0, 10, 1, TickRounding.TowardZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _);

        Assert.Equal(new ModInstance<int, int>(20, 10), Assert.IsType<TempRMMChange<int, int>>(made).Modifier);
    }
    [Fact(DisplayName = "Making the event doesn't change the meter or put anything on the timeline")]
    public void MakingItChangesNothing()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        int eventsBefore = timeline.Events.Count + timeline.PossibleEvents.Count; //The meter keeps a couple of its own

        TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, 20, 3, 10, 5, TickRounding.TowardZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _);
        TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, 20, 3, 0, 5, TickRounding.TowardZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _);

        Assert.Empty(meter.Modifiers);
        Assert.Equal(0, meter.Derivative);
        Assert.Equal(0, meter.CurrentValue);
        Assert.Equal(eventsBefore, timeline.Events.Count + timeline.PossibleEvents.Count);
    }
    #endregion
    #region Rounding
    [Theory(DisplayName = "When the change doesn't divide evenly into the ticks, the rounding decides the size of each tick, and what is reported is what the ticks add up to")]
    [InlineData(10, 4, TickRounding.TowardZero, 2, 8)]
    [InlineData(10, 4, TickRounding.AwayFromZero, 3, 12)]
    [InlineData(10, 4, TickRounding.Nearest, 3, 12)]   //2 and a half, and a half rounds up in size
    [InlineData(9, 4, TickRounding.TowardZero, 2, 8)]
    [InlineData(9, 4, TickRounding.AwayFromZero, 3, 12)]
    [InlineData(9, 4, TickRounding.Nearest, 2, 8)]     //2 and a quarter
    [InlineData(11, 4, TickRounding.Nearest, 3, 12)]   //2 and three quarters
    [InlineData(-10, 4, TickRounding.TowardZero, -2, -8)]
    [InlineData(-10, 4, TickRounding.AwayFromZero, -3, -12)]
    [InlineData(-10, 4, TickRounding.Nearest, -3, -12)]
    [InlineData(-9, 4, TickRounding.Nearest, -2, -8)]
    public void RoundingDecidesTheTick(int totalChange, int ticks, TickRounding rounding, int expectedPerTick, int expectedActualChange)
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        IMeterChangeEvent<int, int> made = TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, totalChange, 0, 8, ticks, rounding,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out int actualChange);

        Assert.Equal(expectedPerTick, Assert.IsType<TempRMMChange<int, int>>(made).Modifier.PerTick);
        Assert.Equal(expectedActualChange, actualChange);
    }
    [Theory(DisplayName = "When the change divides evenly into the ticks, every kind of rounding gives the same thing")]
    [InlineData(TickRounding.TowardZero)]
    [InlineData(TickRounding.AwayFromZero)]
    [InlineData(TickRounding.Nearest)]
    public void RoundingDoesNothingToAnEvenSplit(TickRounding rounding)
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        IMeterChangeEvent<int, int> made = TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, -12, 0, 8, 4, rounding,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out int actualChange);

        Assert.Equal(-3, Assert.IsType<TempRMMChange<int, int>>(made).Modifier.PerTick);
        Assert.Equal(-12, actualChange);
    }
    [Fact(DisplayName = "A change smaller than the number of ticks, rounded toward zero, makes ticks of 0 and reports that nothing will change")]
    public void SmallerThanTheTicksRoundedTowardZero()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        IMeterChangeEvent<int, int> made = TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, 3, 0, 8, 4, TickRounding.TowardZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out int actualChange);

        Assert.Equal(0, Assert.IsType<TempRMMChange<int, int>>(made).Modifier.PerTick);
        Assert.Equal(0, actualChange);
    }
    [Fact(DisplayName = "A change smaller than the number of ticks, rounded away from zero, makes ticks of 1")]
    public void SmallerThanTheTicksRoundedAway()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        IMeterChangeEvent<int, int> made = TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, 3, 0, 8, 4, TickRounding.AwayFromZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out int actualChange);

        Assert.Equal(1, Assert.IsType<TempRMMChange<int, int>>(made).Modifier.PerTick);
        Assert.Equal(4, actualChange);
    }
    #endregion
    #region What is reported and what is refused
    [Fact(DisplayName = "What is reported is cut down to what fits between the meter's limits")]
    public void ReportsWhatFits()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> nearMaximum = new(timeline, 95, 0, 100);
        RateManagedMeter<int, int> nearMinimum = new(timeline, 5, 0, 100);

        TempRMMChange<int, int>.CreateEventFromTickedLerp(nearMaximum, 20, 0, 10, 5, TickRounding.TowardZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out int upward);
        TempRMMChange<int, int>.CreateEventFromTickedLerp(nearMinimum, -20, 0, 0, 5, TickRounding.TowardZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out int downwardAtOnce);

        Assert.Equal(5, upward);
        Assert.Equal(-5, downwardAtOnce);
    }
    [Fact(DisplayName = "A negative time is refused, and so is a change that takes time with fewer than 1 tick")]
    public void BadTimeOrTicksAreRefused()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        Assert.Throws<ArgumentOutOfRangeException>(() => TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, 20, 0, -10, 5, TickRounding.TowardZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, 20, 0, 10, 0, TickRounding.TowardZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, 20, 0, 10, -2, TickRounding.TowardZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _));
    }
    [Fact(DisplayName = "On a whole-number timeline, a time that doesn't divide into the ticks is refused")]
    public void TimeMustDivideIntoTheTicksOnAWholeNumberTimeline()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

        Assert.Throws<ArgumentException>(() => TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, 30, 0, 10, 3, TickRounding.TowardZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _));
    }
    [Fact(DisplayName = "On a floating point timeline, the same time divides into the ticks as a fraction")]
    public void TimeDividesIntoTheTicksOnAFloatingPointTimeline()
    {
        Timeline<double> timeline = new();
        RateManagedMeter<double, int> meter = new(timeline, 0, -1000, 1000);

        IMeterChangeEvent<double, int> made = TempRMMChange<double, int>.CreateEventFromTickedLerp(meter, 30, 0, 10, 3, TickRounding.TowardZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _);

        ModInstance<double, int> modifier = Assert.IsType<TempRMMChange<double, int>>(made).Modifier;
        Assert.Equal(10, modifier.PerTick);
        Assert.Equal(10.0 / 3.0, modifier.TickRate);
        Assert.Equal(new Rational<long>(3), modifier.Magnitude);
    }
    #endregion
    #region Through the timeline
    [Fact(DisplayName = "A change in ticks waits, then moves the meter by the whole amount one tick at a time, then stops")]
    public void ChangeInTicksThroughTheTimeline()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        IMeterChangeEvent<int, int> made = TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, 20, 3, 10, 5, TickRounding.TowardZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _);
        timeline.AddEvent(made.AsOkazo);
        int totalAdvance = 0;

        foreach (StepReport<int> report in timeline.TakeSteps())
        {
            totalAdvance += report.AdvanceAmount;
        }
        Assert.Equal(3, totalAdvance);
        Assert.Equal(0, meter.CurrentValue); //It has only just started
        Assert.Equal((4, 2), (meter.Derivative, meter.DerivativeDenominator));
        Assert.False(made.IsFinished);

        while (!made.IsFinished)
        {
            timeline.SetOpen();
            foreach (StepReport<int> report in timeline.TakeSteps())
            {
                totalAdvance += report.AdvanceAmount;
            }
        }
        Assert.Equal(13, totalAdvance);
        Assert.Equal(20, meter.CurrentValue);
        Assert.Empty(meter.Modifiers);
        Assert.Equal(0, meter.Derivative);
    }
    [Fact(DisplayName = "A change in ticks that was rounded moves the meter by what was reported, not by what was asked for")]
    public void RoundedChangeThroughTheTimeline()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        IMeterChangeEvent<int, int> made = TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, 10, 0, 8, 4, TickRounding.AwayFromZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out int actualChange);
        timeline.AddEvent(made.AsOkazo);

        while (!made.IsFinished)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        Assert.Equal(12, actualChange);
        Assert.Equal(12, meter.CurrentValue);
    }
    [Fact(DisplayName = "A change over no time waits, then moves the meter by the whole amount at once, and it stays moved")]
    public void ChangeAtOnceThroughTheTimeline()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        IMeterChangeEvent<int, int> made = TempRMMChange<int, int>.CreateEventFromTickedLerp(meter, 10, 3, 0, 4, TickRounding.TowardZero,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _);
        timeline.AddEvent(made.AsOkazo);
        int totalAdvance = 0;

        foreach (StepReport<int> report in timeline.TakeSteps())
        {
            totalAdvance += report.AdvanceAmount;
        }

        Assert.Equal(3, totalAdvance);
        Assert.Equal(10, meter.CurrentValue);
        Assert.True(made.IsFinished);
        Assert.Empty(meter.Modifiers);
    }
    #endregion
}
