using CoreMetrics;
using Meters;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Meters_Test;

/// <summary>
/// Tests for TempContinuousChange.CreateEventFromLerp, which makes the right event for changing a meter by a set amount over a set length of time.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class LerpEventTests
{
    #region Which event is made
    [Fact(DisplayName = "A change over some time makes a TempContinuousChange at the rate that gives the whole change in that time")]
    public void GradualChangeMakesATempContinuousChange()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);

        IMeterChangeEvent<int, double> made = TempContinuousChange<int, double>.CreateEventFromLerp(meter, 20, 2, 4,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out double actualChange);

        TempContinuousChange<int, double> change = Assert.IsType<TempContinuousChange<int, double>>(made);
        Assert.Same(meter, change.MeterAppliedTo);
        Assert.Equal(5, change.DerivativeChange);
        Assert.Equal(2, change.WaitingTime);
        Assert.Equal(4, change.ApplyTime);
        Assert.Equal(PriorityRank.MeterChangePhaseStart, change.ApplyPriority);
        Assert.Equal(PriorityRank.MeterChangePhaseEnd, change.RemovePriority);
        Assert.Equal(20, actualChange);
    }
    [Fact(DisplayName = "A change over no time at all makes an InstantChangeEvent for the whole change")]
    public void InstantChangeMakesAnInstantChangeEvent()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);

        IMeterChangeEvent<int, double> made = TempContinuousChange<int, double>.CreateEventFromLerp(meter, 20, 2, 0,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out double actualChange);

        InstantChangeEvent<int, double> change = Assert.IsType<InstantChangeEvent<int, double>>(made);
        Assert.Same(meter, change.MeterAppliedTo);
        Assert.Equal(20, change.Change);
        Assert.Equal(new TimeOrNever<int>(2), change.TimeRemaining);
        Assert.Equal(PriorityRank.MeterChangePhaseStart, change.Priority); //The same priority a gradual one would start with
        Assert.Equal(20, actualChange);
    }
    [Fact(DisplayName = "The rate can be a fraction when the change doesn't divide evenly by the time")]
    public void RateCanBeAFraction()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);

        IMeterChangeEvent<int, double> made = TempContinuousChange<int, double>.CreateEventFromLerp(meter, 10, 0, 4,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _);

        Assert.Equal(2.5, Assert.IsType<TempContinuousChange<int, double>>(made).DerivativeChange);
    }
    [Fact(DisplayName = "A change downward makes a negative rate")]
    public void ChangeDownwardMakesANegativeRate()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);

        IMeterChangeEvent<int, double> made = TempContinuousChange<int, double>.CreateEventFromLerp(meter, -20, 0, 4,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out double actualChange);

        Assert.Equal(-5, Assert.IsType<TempContinuousChange<int, double>>(made).DerivativeChange);
        Assert.Equal(-20, actualChange);
    }
    [Fact(DisplayName = "A negative time for the change to take is refused")]
    public void NegativeTimeIsRefused()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);

        Assert.Throws<ArgumentOutOfRangeException>(() => TempContinuousChange<int, double>.CreateEventFromLerp(meter, 20, 2, -4,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _));
    }
    [Fact(DisplayName = "Making the event doesn't change the meter or put anything on the timeline")]
    public void MakingItChangesNothing()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);

        TempContinuousChange<int, double>.CreateEventFromLerp(meter, 20, 2, 4,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _);
        TempContinuousChange<int, double>.CreateEventFromLerp(meter, 20, 2, 0,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _);

        Assert.Equal(50, meter.CurrentValue);
        Assert.Equal(0, meter.Derivative);
        Assert.Equal(0, meter.InstantChange);
        Assert.Empty(timeline.Events);
        Assert.Empty(timeline.PossibleEvents);
    }
    #endregion
    #region How much would fit
    [Theory(DisplayName = "It reports how much of the change fits under the maximum, whether the change is gradual or all at once")]
    [InlineData(4)]
    [InlineData(0)]
    public void ReportsWhatFitsUnderTheMaximum(int lerpTime)
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 90, 0, 100);

        TempContinuousChange<int, double>.CreateEventFromLerp(meter, 20, 2, lerpTime,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out double actualChange);

        Assert.Equal(10, actualChange);
    }
    [Theory(DisplayName = "It reports how much of the change fits above the minimum, whether the change is gradual or all at once")]
    [InlineData(4)]
    [InlineData(0)]
    public void ReportsWhatFitsAboveTheMinimum(int lerpTime)
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 5, 0, 100);

        TempContinuousChange<int, double>.CreateEventFromLerp(meter, -20, 2, lerpTime,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out double actualChange);

        Assert.Equal(-5, actualChange);
    }
    [Fact(DisplayName = "A gradual change that doesn't all fit still gets the full rate, and the meter stops at its limit")]
    public void GradualChangeThatDoesNotFitStillHasTheFullRate()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 90, 0, 100);
        IMeterChangeEvent<int, double> made = TempContinuousChange<int, double>.CreateEventFromLerp(meter, 20, 2, 4,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _);
        TempContinuousChange<int, double> change = Assert.IsType<TempContinuousChange<int, double>>(made);
        timeline.AddEvent(change);

        while (!change.IsFinished)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        Assert.Equal(5, change.DerivativeChange);
        Assert.Equal(100, meter.CurrentValue);
        Assert.Equal(0, meter.Derivative);
    }
    #endregion
    #region Through the timeline
    [Fact(DisplayName = "A gradual change waits, then moves the meter by the whole amount over its time, then stops")]
    public void GradualChangeThroughTheTimeline()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        IMeterChangeEvent<int, double> made = TempContinuousChange<int, double>.CreateEventFromLerp(meter, 20, 2, 4,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _);
        timeline.AddEvent(made.AsOkazo);
        int totalAdvance = 0;

        foreach (StepReport<int> report in timeline.TakeSteps())
        {
            totalAdvance += report.AdvanceAmount;
        }
        Assert.Equal(2, totalAdvance);
        Assert.Equal(50, meter.CurrentValue); //It has only just started
        Assert.Equal(5, meter.Derivative);

        timeline.SetOpen();
        foreach (StepReport<int> report in timeline.TakeSteps())
        {
            totalAdvance += report.AdvanceAmount;
        }
        Assert.Equal(6, totalAdvance);
        Assert.Equal(70, meter.CurrentValue);
        Assert.Equal(0, meter.Derivative);
    }
    [Fact(DisplayName = "A change over no time waits, then moves the meter by the whole amount at once, and it stays moved")]
    public void InstantChangeThroughTheTimeline()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        IMeterChangeEvent<int, double> made = TempContinuousChange<int, double>.CreateEventFromLerp(meter, 20, 2, 0,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _);
        timeline.AddEvent(made.AsOkazo);
        int totalAdvance = 0;

        foreach (StepReport<int> report in timeline.TakeSteps())
        {
            totalAdvance += report.AdvanceAmount;
        }

        Assert.Equal(2, totalAdvance);
        Assert.Equal(70, meter.CurrentValue);
        Assert.Equal(0, meter.Derivative);
        Assert.DoesNotContain(made.AsOkazo, timeline.Events);         //Nothing is left to undo it
        Assert.DoesNotContain(made.AsOkazo, timeline.PossibleEvents);
    }
    [Fact(DisplayName = "Two gradual changes on the same meter at once each add their own rate and take it back off")]
    public void TwoGradualChangesAtOnce()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        IMeterChangeEvent<int, double> rise = TempContinuousChange<int, double>.CreateEventFromLerp(meter, 20, 0, 4,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _);
        IMeterChangeEvent<int, double> fall = TempContinuousChange<int, double>.CreateEventFromLerp(meter, -6, 1, 2,
            PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline, out _);
        timeline.AddEvent(rise.AsOkazo);
        timeline.AddEvent(fall.AsOkazo);
        TempContinuousChange<int, double> riseChange = Assert.IsType<TempContinuousChange<int, double>>(rise);

        while (!riseChange.IsFinished)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        Assert.Equal(64, meter.CurrentValue); //Up 20 and down 6
        Assert.Equal(0, meter.Derivative);
    }
    #endregion
}
