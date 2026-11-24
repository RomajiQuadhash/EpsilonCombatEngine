using CoreMetrics;
using Meters;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Meters_Test;

/// <summary>
/// Tests for what a meter's minimum and maximum mean for its threshold events, and for changing them with UpdateMin and UpdateMax.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class MeterLimitTests
{
    #region Thresholds outside the limits
    [Theory(DisplayName = "A threshold above a continuous meter's maximum never occurs, however fast the meter is rising")]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1000)]
    public void ContinuousThresholdAboveMaximumNeverOccurs(int derivative)
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, int> meter = new(timeline, 90, 0, 100);
        meter.SetDerivative(derivative);
        ReachThresholdEvent<int, int> threshold = new(meter, 150, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> thresholdOccurred = new();
        threshold.Occurring += thresholdOccurred.OnTrigger;
        Card<int, string> end = new(20, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> endOccurred = new();
        end.Occurring += endOccurred.OnTrigger;
        Assert.True(threshold.TimeRemaining.IsNever);

        while (endOccurred.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        Assert.Null(thresholdOccurred.MostRecentEvent);
        Assert.True(threshold.TimeRemaining.IsNever);
        Assert.Equal(100, meter.CurrentValue); //Stopped at its maximum
    }
    [Theory(DisplayName = "A threshold below a continuous meter's minimum never occurs, however fast the meter is falling")]
    [InlineData(-1)]
    [InlineData(-10)]
    [InlineData(-1000)]
    public void ContinuousThresholdBelowMinimumNeverOccurs(int derivative)
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, int> meter = new(timeline, 10, 0, 100);
        meter.SetDerivative(derivative);
        ReachThresholdEvent<int, int> threshold = new(meter, -50, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> thresholdOccurred = new();
        threshold.Occurring += thresholdOccurred.OnTrigger;
        Card<int, string> end = new(20, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> endOccurred = new();
        end.Occurring += endOccurred.OnTrigger;
        Assert.True(threshold.TimeRemaining.IsNever);

        while (endOccurred.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        Assert.Null(thresholdOccurred.MostRecentEvent);
        Assert.True(threshold.TimeRemaining.IsNever);
        Assert.Equal(0, meter.CurrentValue); //Stopped at its minimum
    }
    [Theory(DisplayName = "A threshold above a discrete meter's maximum never occurs, however much each tick adds")]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(1000)]
    public void DiscreteThresholdAboveMaximumNeverOccurs(int perTick)
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 97, 0, 100);
        meter.SetDerivative(perTick, 2, out _);
        DiscreteReachThresholdEvent<int, int> threshold = new(meter, 150, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> thresholdOccurred = new();
        threshold.Occurring += thresholdOccurred.OnTrigger;
        Card<int, string> end = new(20, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> endOccurred = new();
        end.Occurring += endOccurred.OnTrigger;
        Assert.True(threshold.TimeRemaining.IsNever);

        while (endOccurred.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        Assert.Null(thresholdOccurred.MostRecentEvent);
        Assert.True(threshold.TimeRemaining.IsNever);
        Assert.Equal(100, meter.CurrentValue); //Stopped at its maximum
    }
    [Fact(DisplayName = "Neither ThresholdReach nor ThresholdPass gives a time for a value outside the limits, looking forward or back")]
    public void ReachAndPassAreNeverOutsideTheLimits()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> instant = new(timeline, 50, 0, 100);
        LinearContinuousMeter<int, int> continuous = new(timeline, 50, 0, 100);
        continuous.SetDerivative(10);
        LinearDiscreteMeter<int, int> discrete = new(timeline, 50, 0, 100);
        discrete.SetDerivative(10, 2, out _);

        foreach (AbstractMeter<int, int> meter in new AbstractMeter<int, int>[] { instant, continuous, discrete })
        {
            foreach (int outside in new[] { 101, 150, -1, -50 })
            {
                foreach (bool allowNegative in new[] { false, true })
                {
                    Assert.True(meter.ThresholdReach(outside, allowNegative).IsNever, $"{meter.GetType().Name} reach {outside}");
                    Assert.True(meter.ThresholdPass(outside, allowNegative).IsNever, $"{meter.GetType().Name} pass {outside}");
                }
            }
        }
    }
    #endregion
    #region Thresholds on the limits
    [Fact(DisplayName = "A threshold exactly on a continuous meter's maximum occurs when the meter gets there")]
    public void ContinuousThresholdOnMaximumOccurs()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, int> meter = new(timeline, 90, 0, 100);
        meter.SetDerivative(10);
        ReachThresholdEvent<int, int> threshold = new(meter, 100, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> thresholdOccurred = new();
        threshold.Occurring += thresholdOccurred.OnTrigger;
        int totalAdvance = 0;

        foreach (StepReport<int> report in timeline.TakeSteps())
        {
            totalAdvance += report.AdvanceAmount;
        }

        Assert.Same(threshold, thresholdOccurred.MostRecentEvent);
        Assert.Equal(1, totalAdvance);
        Assert.Equal(100, meter.CurrentValue);
    }
    [Fact(DisplayName = "A threshold exactly on a continuous meter's minimum occurs when the meter gets there")]
    public void ContinuousThresholdOnMinimumOccurs()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, int> meter = new(timeline, 10, 0, 100);
        meter.SetDerivative(-5);
        ReachThresholdEvent<int, int> threshold = new(meter, 0, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> thresholdOccurred = new();
        threshold.Occurring += thresholdOccurred.OnTrigger;
        int totalAdvance = 0;

        foreach (StepReport<int> report in timeline.TakeSteps())
        {
            totalAdvance += report.AdvanceAmount;
        }

        Assert.Same(threshold, thresholdOccurred.MostRecentEvent);
        Assert.Equal(2, totalAdvance);
        Assert.Equal(0, meter.CurrentValue);
    }
    [Fact(DisplayName = "A threshold on a discrete meter's maximum occurs at the tick that would have gone past it")]
    public void DiscreteThresholdOnMaximumOccursWhenTheTickIsCutShort()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 97, 0, 100);
        meter.SetDerivative(5, 10, out _); //The first tick would take it to 102
        DiscreteReachThresholdEvent<int, int> threshold = new(meter, 100, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> thresholdOccurred = new();
        threshold.Occurring += thresholdOccurred.OnTrigger;
        int totalAdvance = 0;

        foreach (StepReport<int> report in timeline.TakeSteps())
        {
            totalAdvance += report.AdvanceAmount;
        }

        Assert.Same(threshold, thresholdOccurred.MostRecentEvent);
        Assert.Equal(10, totalAdvance);
        Assert.Equal(100, meter.CurrentValue);
    }
    [Fact(DisplayName = "A threshold on a discrete meter's minimum occurs at the tick that would have gone past it")]
    public void DiscreteThresholdOnMinimumOccursWhenTheTickIsCutShort()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 3, 0, 100);
        meter.SetDerivative(-5, 10, out _); //The first tick would take it to -2
        DiscreteReachThresholdEvent<int, int> threshold = new(meter, 0, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> thresholdOccurred = new();
        threshold.Occurring += thresholdOccurred.OnTrigger;
        int totalAdvance = 0;

        foreach (StepReport<int> report in timeline.TakeSteps())
        {
            totalAdvance += report.AdvanceAmount;
        }

        Assert.Same(threshold, thresholdOccurred.MostRecentEvent);
        Assert.Equal(10, totalAdvance);
        Assert.Equal(0, meter.CurrentValue);
    }
    #endregion
}
