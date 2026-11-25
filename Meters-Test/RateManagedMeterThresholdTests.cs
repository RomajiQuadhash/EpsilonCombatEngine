using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Meters_Test;

/// <summary>
/// Tests for threshold events on a RateManagedMeter's own value while leap years come and go,
/// and for what the leap years themselves are allowed to do.
/// </summary>
/// <remarks>Claude written, Romaji Approved</remarks>
public class RateManagedMeterThresholdTests
{
    #region Thresholds while the rate changes
    [Fact(DisplayName = "A threshold the leap year's rate would reach, but which the leap year ends before, doesn't occur at the time it showed during the leap year")]
    public void ThresholdPredictedDuringALeapYearDoesNotOccurEarly()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        //7/25 ticking every 5: ticks of 1, with a leap year of ticks of 2 from the tick at 15 to the tick at 25.
        //So the value is 5 at 20 and 7 at 25, and then goes back to 1 a tick: 8 at 30, 9 at 35.
        meter.Add(new ModInstance<int, int>(1, 5));
        meter.Add(new ModInstance<int, int>(2, 25));
        DiscreteReachThresholdEvent<int, int> threshold = new(meter, 9, PriorityRank.MeterTriggerProgressive, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> thresholdOccurred = new();
        threshold.Occurring += thresholdOccurred.OnTrigger;
        Card<int, string> duringLeapYear = new(22, timeline, PriorityRank.None);
        timeline.AddEvent(duringLeapYear);
        Card<int, string> afterItShowed = new(31, timeline, PriorityRank.None);
        timeline.AddEvent(afterItShowed);
        MostRecentEventHolder<int> reached = new();
        duringLeapYear.Occurring += reached.OnTrigger;
        afterItShowed.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, duringLeapYear))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //At 22, with ticks of 2, it looks like the tick at 30 will take it from 7 to 9
        Assert.Equal(LeapYearState.Positive, meter.CurrentLeapYear);
        Assert.Equal(5, meter.CurrentValue);
        Assert.Equal(new CoreMetrics.TimeOrNever<int>(8), threshold.TimeRemaining);

        while (!ReferenceEquals(reached.MostRecentEvent, afterItShowed))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //But the leap year ended at 25, so the tick at 30 only took it to 8
        Assert.Null(thresholdOccurred.MostRecentEvent);
        Assert.Equal(8, meter.CurrentValue);
        Assert.Equal(new CoreMetrics.TimeOrNever<int>(4), threshold.TimeRemaining);

        while (thresholdOccurred.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //It occurs at the tick at 35, when the value really gets there
        Assert.Equal(9, meter.CurrentValue);
        Assert.Equal(0, meter.TimeSinceLastIncrement);
        Assert.Equal(0, meter.MidAdvanceRateChanges);
    }
    [Fact(DisplayName = "A threshold that a leap year brings forward occurs at the leap tick that reaches it")]
    public void ThresholdBroughtForwardByALeapYearOccursThen()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        //Same meter as above: 3 at 15, then leap ticks to 5 at 20 and 7 at 25
        meter.Add(new ModInstance<int, int>(1, 5));
        meter.Add(new ModInstance<int, int>(2, 25));
        DiscreteReachThresholdEvent<int, int> threshold = new(meter, 5, PriorityRank.MeterTriggerProgressive, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> thresholdOccurred = new();
        threshold.Occurring += thresholdOccurred.OnTrigger;
        //Before any leap year, ticks of 1 would get to 5 at 25
        Assert.Equal(new CoreMetrics.TimeOrNever<int>(25), threshold.TimeRemaining);
        int totalAdvance = 0;

        while (thresholdOccurred.MostRecentEvent is null)
        {
            foreach (StepReport<int> report in timeline.TakeSteps())
            {
                totalAdvance += report.AdvanceAmount;
            }
            timeline.SetOpen();
        }

        Assert.Equal(20, totalAdvance);
        Assert.Equal(5, meter.CurrentValue);
        Assert.Equal(LeapYearState.Positive, meter.CurrentLeapYear);
    }
    [Fact(DisplayName = "A threshold on a RateManagedMeter's maximum occurs when a tick is cut short by it")]
    public void ThresholdOnTheMaximumOccurs()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, 0, 8);
        meter.Add(new ModInstance<int, int>(5, 2));
        DiscreteReachThresholdEvent<int, int> threshold = new(meter, 8, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> thresholdOccurred = new();
        threshold.Occurring += thresholdOccurred.OnTrigger;
        int totalAdvance = 0;

        while (thresholdOccurred.MostRecentEvent is null)
        {
            foreach (StepReport<int> report in timeline.TakeSteps())
            {
                totalAdvance += report.AdvanceAmount;
            }
            timeline.SetOpen();
        }

        Assert.Equal(4, totalAdvance); //5 at 2, then the tick at 4 would be 10 and stops at 8
        Assert.Equal(8, meter.CurrentValue);
    }
    #endregion
    #region What leap years are allowed to do
    [Theory(DisplayName = "While the ModInstances keep adding, a positive leap year is never followed by a negative one, and the spillover never gets to -1")]
    [InlineData(1, 5, 2, 25)]  //7/25
    [InlineData(7, 3, 1, 4)]   //31/12
    [InlineData(3, 2, 1, 7)]   //23/14
    [InlineData(1, 1, 1, 3)]   //4/3
    public void PositiveLeapYearNeverLeadsToANegativeOne(int firstPerTick, int firstTickRate, int secondPerTick, int secondTickRate)
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -100000, 100000);
        meter.Add(new ModInstance<int, int>(firstPerTick, firstTickRate));
        meter.Add(new ModInstance<int, int>(secondPerTick, secondTickRate));
        Card<int, string> end = new(500, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> endOccurred = new();
        end.Occurring += endOccurred.OnTrigger;
        int positiveLeapYearsSeen = 0;
        LeapYearState before = LeapYearState.None;

        while (endOccurred.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
            //Checked every time the timeline stops, which includes every time a leap year starts or ends
            Assert.NotEqual(LeapYearState.Negative, meter.CurrentLeapYear);
            Assert.NotEqual(LeapYearState.Negative, meter.UpcomingLeapYear);
            Assert.True(meter.CurrentSpilloverFraction > new Rational<long>(-1), $"Spillover was {meter.CurrentSpilloverFraction}");
            if (meter.CurrentLeapYear == LeapYearState.Positive && before != LeapYearState.Positive)
            {
                positiveLeapYearsSeen++;
            }
            before = meter.CurrentLeapYear;
        }

        Assert.True(positiveLeapYearsSeen > 1, "Expected several positive leap years to come and go");
        Assert.Equal(0, meter.MidAdvanceRateChanges);
    }
    [Theory(DisplayName = "While the ModInstances keep taking away, a negative leap year is never followed by a positive one, and the spillover never gets to 1")]
    [InlineData(-1, 5, -2, 25)]
    [InlineData(-7, 3, -1, 4)]
    [InlineData(-3, 2, -1, 7)]
    [InlineData(-1, 1, -1, 3)]
    public void NegativeLeapYearNeverLeadsToAPositiveOne(int firstPerTick, int firstTickRate, int secondPerTick, int secondTickRate)
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -100000, 100000);
        meter.Add(new ModInstance<int, int>(firstPerTick, firstTickRate));
        meter.Add(new ModInstance<int, int>(secondPerTick, secondTickRate));
        Card<int, string> end = new(500, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> endOccurred = new();
        end.Occurring += endOccurred.OnTrigger;
        int negativeLeapYearsSeen = 0;
        LeapYearState before = LeapYearState.None;

        while (endOccurred.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
            Assert.NotEqual(LeapYearState.Positive, meter.CurrentLeapYear);
            Assert.NotEqual(LeapYearState.Positive, meter.UpcomingLeapYear);
            Assert.True(meter.CurrentSpilloverFraction < new Rational<long>(1), $"Spillover was {meter.CurrentSpilloverFraction}");
            if (meter.CurrentLeapYear == LeapYearState.Negative && before != LeapYearState.Negative)
            {
                negativeLeapYearsSeen++;
            }
            before = meter.CurrentLeapYear;
        }

        Assert.True(negativeLeapYearsSeen > 1, "Expected several negative leap years to come and go");
        Assert.Equal(0, meter.MidAdvanceRateChanges);
    }
    [Fact(DisplayName = "A leap year adds exactly 1 to each tick while it is on")]
    public void LeapYearAddsOneToEachTick()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        meter.Add(new ModInstance<int, int>(7, 3));
        meter.Add(new ModInstance<int, int>(1, 4)); //Together 31/12, which is 7 and 3/4 every 3
        Assert.Equal(7, meter.Derivative);
        Card<int, string> end = new(200, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> endOccurred = new();
        end.Occurring += endOccurred.OnTrigger;

        while (endOccurred.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
            Assert.Equal(meter.CurrentLeapYear == LeapYearState.Positive ? 8 : 7, meter.Derivative);
            Assert.Equal(3, meter.DerivativeDenominator);
        }
    }
    #endregion
}
