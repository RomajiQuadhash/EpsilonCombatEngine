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
        LinearContinuousMeter<int, double> meter = new(timeline, 90, 0, 100);
        meter.SetDerivative(derivative);
        ReachThresholdEvent<int, double> threshold = new(meter, 150, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
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
        LinearContinuousMeter<int, double> meter = new(timeline, 10, 0, 100);
        meter.SetDerivative(derivative);
        ReachThresholdEvent<int, double> threshold = new(meter, -50, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
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
        LinearContinuousMeter<int, double> continuous = new(timeline, 50, 0, 100);
        continuous.SetDerivative(10);
        LinearDiscreteMeter<int, int> discrete = new(timeline, 50, 0, 100);
        discrete.SetDerivative(10, 2, out _);

        foreach (int outside in new[] { 101, 150, -1, -50 })
        {
            foreach (bool allowNegative in new[] { false, true })
            {
                Assert.True(instant.ThresholdReach(outside, allowNegative).IsNever, $"Instant reach {outside}");
                Assert.True(instant.ThresholdPass(outside, allowNegative).IsNever, $"Instant pass {outside}");
                Assert.True(continuous.ThresholdReach(outside, allowNegative).IsNever, $"Continuous reach {outside}");
                Assert.True(continuous.ThresholdPass(outside, allowNegative).IsNever, $"Continuous pass {outside}");
                Assert.True(discrete.ThresholdReach(outside, allowNegative).IsNever, $"Discrete reach {outside}");
                Assert.True(discrete.ThresholdPass(outside, allowNegative).IsNever, $"Discrete pass {outside}");
            }
        }
    }
    #endregion
    #region Thresholds on the limits
    [Fact(DisplayName = "A threshold exactly on a continuous meter's maximum occurs when the meter gets there")]
    public void ContinuousThresholdOnMaximumOccurs()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 90, 0, 100);
        meter.SetDerivative(10);
        ReachThresholdEvent<int, double> threshold = new(meter, 100, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
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
        LinearContinuousMeter<int, double> meter = new(timeline, 10, 0, 100);
        meter.SetDerivative(-5);
        ReachThresholdEvent<int, double> threshold = new(meter, 0, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
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
    #region Thresholds when the limits move
    [Fact(DisplayName = "A threshold above the maximum can occur once the maximum is raised past it")]
    public void RaisingTheMaximumLetsTheThresholdOccur()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 90, 0, 100);
        meter.SetDerivative(10);
        ReachThresholdEvent<int, double> threshold = new(meter, 150, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> thresholdOccurred = new();
        threshold.Occurring += thresholdOccurred.OnTrigger;
        Assert.True(threshold.TimeRemaining.IsNever);

        Assert.True(meter.UpdateMax(200, false));
        Assert.Equal(new TimeOrNever<int>(6), threshold.TimeRemaining);
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }

        Assert.Same(threshold, thresholdOccurred.MostRecentEvent);
        Assert.Equal(150, meter.CurrentValue);
    }
    [Fact(DisplayName = "A threshold the meter was heading for stops being reachable when the maximum is lowered below it")]
    public void LoweringTheMaximumPutsTheThresholdOutOfReach()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(10);
        ReachThresholdEvent<int, double> threshold = new(meter, 80, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        Assert.Equal(new TimeOrNever<int>(3), threshold.TimeRemaining);

        Assert.True(meter.UpdateMax(70, false)); //50 still fits, so nothing is pushed

        Assert.True(threshold.TimeRemaining.IsNever);
    }
    [Fact(DisplayName = "Lowering the maximum so that the meter is pushed down past a threshold makes that threshold occur")]
    public void BeingPushedPastAThresholdMakesItOccur()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 30, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> thresholdOccurred = new();
        threshold.Occurring += thresholdOccurred.OnTrigger;
        Assert.True(threshold.TimeRemaining.IsNever);

        Assert.True(meter.UpdateMax(25, true)); //Pushed from 50 down to 25, over the 30
        Assert.Equal(new TimeOrNever<int>(0), threshold.TimeRemaining);
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }

        Assert.Same(threshold, thresholdOccurred.MostRecentEvent);
        Assert.Equal(25, meter.CurrentValue);
    }
    #endregion
    #region UpdateMax
    [Theory(DisplayName = "UpdateMax changes the maximum and leaves the value alone if the value still fits, whether or not clamping is allowed")]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdateMaxLeavesAValueThatFits(bool allowClamping)
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.True(meter.UpdateMax(60, allowClamping));

        Assert.Equal(60, meter.MaxValue);
        Assert.Equal(50, meter.CurrentValue);
        Assert.Equal(0, meter.InstantChange);
    }
    [Fact(DisplayName = "UpdateMax can be set exactly to the current value without moving it")]
    public void UpdateMaxOntoTheValue()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.True(meter.UpdateMax(50, false));

        Assert.Equal(50, meter.MaxValue);
        Assert.Equal(50, meter.CurrentValue);
        Assert.Equal(0, meter.InstantChange);
    }
    [Fact(DisplayName = "UpdateMax changes nothing and says so if the value wouldn't fit and clamping isn't allowed")]
    public void UpdateMaxRefusesWithoutClamping()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.False(meter.UpdateMax(40, false));

        Assert.Equal(100, meter.MaxValue);
        Assert.Equal(50, meter.CurrentValue);
        Assert.Equal(0, meter.InstantChange);
    }
    [Fact(DisplayName = "UpdateMax pushes the value down to the new maximum if it wouldn't fit and clamping is allowed, as an instant change")]
    public void UpdateMaxClampsAsAnInstantChange()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        (int before, int after)? announced = null;
        meter.InstantChangeEvt += (_, before, after) => announced = (before, after);

        Assert.True(meter.UpdateMax(40, true));

        Assert.Equal(40, meter.MaxValue);
        Assert.Equal(40, meter.CurrentValue);
        Assert.Equal(-10, meter.InstantChange);
        Assert.Equal((50, 40), announced);
    }
    [Fact(DisplayName = "UpdateMax throws if the new maximum isn't above the minimum")]
    public void UpdateMaxMustStayAboveMinimum()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.Throws<ArgumentException>(() => meter.UpdateMax(0, true));
        Assert.Throws<ArgumentException>(() => meter.UpdateMax(-5, true));
        Assert.Equal(100, meter.MaxValue);
        Assert.Equal(50, meter.CurrentValue);
    }
    #endregion
    #region UpdateMin
    [Theory(DisplayName = "UpdateMin changes the minimum and leaves the value alone if the value still fits, whether or not clamping is allowed")]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdateMinLeavesAValueThatFits(bool allowClamping)
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.True(meter.UpdateMin(40, allowClamping));

        Assert.Equal(40, meter.MinValue);
        Assert.Equal(50, meter.CurrentValue);
        Assert.Equal(0, meter.InstantChange);
    }
    [Fact(DisplayName = "UpdateMin can be set exactly to the current value without moving it")]
    public void UpdateMinOntoTheValue()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.True(meter.UpdateMin(50, false));

        Assert.Equal(50, meter.MinValue);
        Assert.Equal(50, meter.CurrentValue);
        Assert.Equal(0, meter.InstantChange);
    }
    [Fact(DisplayName = "UpdateMin changes nothing and says so if the value wouldn't fit and clamping isn't allowed")]
    public void UpdateMinRefusesWithoutClamping()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.False(meter.UpdateMin(60, false));

        Assert.Equal(0, meter.MinValue);
        Assert.Equal(50, meter.CurrentValue);
        Assert.Equal(0, meter.InstantChange);
    }
    [Fact(DisplayName = "UpdateMin pushes the value up to the new minimum if it wouldn't fit and clamping is allowed, as an instant change")]
    public void UpdateMinClampsAsAnInstantChange()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        (int before, int after)? announced = null;
        meter.InstantChangeEvt += (_, before, after) => announced = (before, after);

        Assert.True(meter.UpdateMin(60, true));

        Assert.Equal(60, meter.MinValue);
        Assert.Equal(60, meter.CurrentValue);
        Assert.Equal(10, meter.InstantChange);
        Assert.Equal((50, 60), announced);
    }
    [Fact(DisplayName = "UpdateMin throws if the new minimum isn't below the maximum")]
    public void UpdateMinMustStayBelowMaximum()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);

        Assert.Throws<ArgumentException>(() => meter.UpdateMin(100, true));
        Assert.Throws<ArgumentException>(() => meter.UpdateMin(105, true));
        Assert.Equal(0, meter.MinValue);
        Assert.Equal(50, meter.CurrentValue);
    }
    #endregion
    #region Clamping alongside other instant changes
    [Fact(DisplayName = "Being pushed by a range change adds to an instant change that is already recorded")]
    public void ClampAddsToAnExistingInstantChange()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        meter.AddInstantChange(10); //Now 60

        Assert.True(meter.UpdateMax(55, true));

        Assert.Equal(55, meter.CurrentValue);
        Assert.Equal(5, meter.InstantChange); //Up 10, then pushed down 5
    }
    [Fact(DisplayName = "The instant change from being pushed by a range change is cleared by the next advance like any other")]
    public void ClampInstantChangeClearsOnAdvance()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        meter.UpdateMax(40, true);
        Assert.Equal(-10, meter.InstantChange);

        meter.OnAdvance(timeline, 1);

        Assert.Equal(0, meter.InstantChange);
        Assert.Equal(40, meter.CurrentValue);
    }
    #endregion
}
