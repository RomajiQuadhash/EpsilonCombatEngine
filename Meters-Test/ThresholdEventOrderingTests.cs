using Meters;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Meters_Test;

/// <summary>
/// Tests for the order real threshold events come in when they are due at the same moment.
/// The timeline's own tests cover time, priority and UUID. These cover the step in between that only threshold events have: their transition data.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class ThresholdEventOrderingTests
{
    #region Continuous meters
    [Fact(DisplayName = "Of two thresholds reached steadily at the same moment, the one whose meter is changing faster for its range comes first")]
    public void FasterSteadyChangeComesFirst()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> fastMeter = new(timeline, 0, 0, 100);
        fastMeter.SetDerivative(10);
        LinearContinuousMeter<int, double> slowMeter = new(timeline, 0, 0, 100);
        slowMeter.SetDerivative(2);
        ReachThresholdEvent<int, double> fast = new(fastMeter, 20, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        ReachThresholdEvent<int, double> slow = new(slowMeter, 4, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        Assert.Equal(fast.TimeRemaining, slow.TimeRemaining); //Both in 2

        Assert.True(fast.CompareTo(slow) < 0);
        Assert.True(slow.CompareTo(fast) > 0);
    }
    [Fact(DisplayName = "The timeline makes the faster-changing one occur first, whichever was added first")]
    public void TimelineUsesThatOrder()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> fastMeter = new(timeline, 0, 0, 100);
        fastMeter.SetDerivative(10);
        LinearContinuousMeter<int, double> slowMeter = new(timeline, 0, 0, 100);
        slowMeter.SetDerivative(2);
        ReachThresholdEvent<int, double> fast = new(fastMeter, 20, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        ReachThresholdEvent<int, double> slow = new(slowMeter, 4, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(slow);
        timeline.AddEvent(fast);
        MostRecentEventHolder<int> occurred = new();
        fast.Occurring += occurred.OnTrigger;
        slow.Occurring += occurred.OnTrigger;

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        Assert.Same(fast, occurred.MostRecentEvent);

        timeline.SetOpen();
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        Assert.Same(slow, occurred.MostRecentEvent);
    }
    [Fact(DisplayName = "How fast a meter is changing is judged against its range, so a small meter changing slowly can come before a big one changing fast")]
    public void SteadyChangeIsJudgedAgainstTheRange()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> bigMeter = new(timeline, 0, 0, 1000);
        bigMeter.SetDerivative(10); //1% of its range a time unit
        LinearContinuousMeter<int, double> smallMeter = new(timeline, 0, 0, 100);
        smallMeter.SetDerivative(2); //2% of its range a time unit
        ReachThresholdEvent<int, double> onBig = new(bigMeter, 20, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        ReachThresholdEvent<int, double> onSmall = new(smallMeter, 4, PriorityRank.MeterLimit, timeline) { CouldOccur = true };

        Assert.True(onSmall.CompareTo(onBig) < 0);
        Assert.True(onBig.CompareTo(onSmall) > 0);
    }
    [Fact(DisplayName = "Of two thresholds jumped over at the same moment, the one the jump went further past for its size comes first")]
    public void FurtherPastComesFirst()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> firstMeter = new(timeline, 50, 0, 100);
        InstantChangeMeter<int, int> secondMeter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> wellPast = new(firstMeter, 60, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        ReachThresholdEvent<int, int> barelyPast = new(secondMeter, 75, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        //Both jump 30, to 80. That is 20 past the first threshold and only 5 past the second.
        firstMeter.AddInstantChange(30);
        secondMeter.AddInstantChange(30);
        Assert.Equal(wellPast.TimeRemaining, barelyPast.TimeRemaining); //Both now

        Assert.True(wellPast.CompareTo(barelyPast) < 0);
        Assert.True(barelyPast.CompareTo(wellPast) > 0);
    }
    [Fact(DisplayName = "A threshold that was jumped over comes before one that was only just landed on")]
    public void JumpedOverBeforeLandedOn()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> firstMeter = new(timeline, 50, 0, 100);
        InstantChangeMeter<int, int> secondMeter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> jumpedOver = new(firstMeter, 60, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        ReachThresholdEvent<int, int> landedOn = new(secondMeter, 80, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        firstMeter.AddInstantChange(30);
        secondMeter.AddInstantChange(30);

        Assert.True(jumpedOver.CompareTo(landedOn) < 0);
        Assert.True(landedOn.CompareTo(jumpedOver) > 0);
    }
    [Fact(DisplayName = "Priority is looked at before transition data")]
    public void PriorityComesBeforeTransitionData()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> firstMeter = new(timeline, 50, 0, 100);
        InstantChangeMeter<int, int> secondMeter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> wellPast = new(firstMeter, 60, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        ReachThresholdEvent<int, int> barelyPastButUrgent = new(secondMeter, 75, PriorityRank.PlayerDeath, timeline) { CouldOccur = true };
        firstMeter.AddInstantChange(30);
        secondMeter.AddInstantChange(30);

        Assert.True(barelyPastButUrgent.CompareTo(wellPast) < 0);
        Assert.True(wellPast.CompareTo(barelyPastButUrgent) > 0);
    }
    #endregion
    #region Discrete meters
    [Fact(DisplayName = "Of two discrete thresholds reached by ticks at the same moment, the one on the faster tick rate comes first")]
    public void FasterTickRateComesFirst()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> fastMeter = new(timeline, 0, 0, 100);
        fastMeter.SetDerivative(5, 2, out _);
        LinearDiscreteMeter<int, int> slowMeter = new(timeline, 0, 0, 100);
        slowMeter.SetDerivative(10, 4, out _);
        DiscreteReachThresholdEvent<int, int> onFast = new(fastMeter, 10, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        DiscreteReachThresholdEvent<int, int> onSlow = new(slowMeter, 10, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        Assert.Equal(onFast.TimeRemaining, onSlow.TimeRemaining); //Both in 4

        Assert.True(onFast.CompareTo(onSlow) < 0);
        Assert.True(onSlow.CompareTo(onFast) > 0);
    }
    [Fact(DisplayName = "Of two discrete thresholds jumped over by instant changes at the same moment, the bigger instant change comes first")]
    public void BiggerInstantChangeComesFirst()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> firstMeter = new(timeline, 50, 0, 100);
        firstMeter.SetDerivative(1, 10, out _);
        LinearDiscreteMeter<int, int> secondMeter = new(timeline, 50, 0, 100);
        secondMeter.SetDerivative(1, 10, out _);
        //Partway through a tick, so the tick isn't part of what happens
        firstMeter.OnAdvance(timeline, 3);
        secondMeter.OnAdvance(timeline, 3);
        DiscreteReachThresholdEvent<int, int> bigJump = new(firstMeter, 55, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        DiscreteReachThresholdEvent<int, int> smallJump = new(secondMeter, 52, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        firstMeter.AddInstantChange(10);
        secondMeter.AddInstantChange(4);
        Assert.Equal(bigJump.TimeRemaining, smallJump.TimeRemaining); //Both now

        Assert.True(bigJump.CompareTo(smallJump) < 0);
        Assert.True(smallJump.CompareTo(bigJump) > 0);
    }
    [Fact(DisplayName = "A discrete threshold jumped over by an instant change comes before one reached by a tick at the same moment")]
    public void InstantChangeBeforeATick()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> jumpedMeter = new(timeline, 50, 0, 100);
        jumpedMeter.SetDerivative(1, 10, out _);
        LinearDiscreteMeter<int, int> tickedMeter = new(timeline, 50, 0, 100);
        tickedMeter.SetDerivative(5, 10, out _);
        DiscreteReachThresholdEvent<int, int> byJump = new(jumpedMeter, 55, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        DiscreteReachThresholdEvent<int, int> byTick = new(tickedMeter, 55, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        //The tick lands on both, and then one of them is also jumped
        jumpedMeter.OnAdvance(timeline, 10);
        tickedMeter.OnAdvance(timeline, 10);
        jumpedMeter.AddInstantChange(10);
        Assert.Equal(byJump.TimeRemaining, byTick.TimeRemaining); //Both now

        Assert.True(byJump.CompareTo(byTick) < 0);
        Assert.True(byTick.CompareTo(byJump) > 0);
    }
    #endregion
    #region Mixing kinds of meter
    [Fact(DisplayName = "A discrete meter's threshold and a jump-only meter's threshold, both jumped over at the same moment, are put in order by how far past each went for its size")]
    public void DiscreteAndJumpOnlyCanBeOrdered()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> discreteMeter = new(timeline, 97, 0, 200);
        discreteMeter.SetDerivative(1, 10, out _);
        discreteMeter.OnAdvance(timeline, 3); //Partway through a tick, so the tick isn't part of what happens
        InstantChangeMeter<int, int> jumpOnlyMeter = new(timeline, 50, 0, 100);
        DiscreteReachThresholdEvent<int, int> onDiscrete = new(discreteMeter, 100, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        ReachThresholdEvent<int, int> onJumpOnly = new(jumpOnlyMeter, 60, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        discreteMeter.AddInstantChange(10); //7 past, of a jump of 10
        jumpOnlyMeter.AddInstantChange(30); //20 past, of a jump of 30
        Assert.Equal(onDiscrete.TimeRemaining, onJumpOnly.TimeRemaining); //Both now

        Assert.True(onDiscrete.CompareTo(onJumpOnly) < 0);
        Assert.True(onJumpOnly.CompareTo(onDiscrete) > 0);
    }
    [Fact(DisplayName = "The timeline can run a discrete meter's threshold and a jump-only meter's threshold that are due at the same moment")]
    public void TimelineRunsDiscreteAndJumpOnlyTogether()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> discreteMeter = new(timeline, 97, 0, 200);
        discreteMeter.SetDerivative(1, 10, out _);
        discreteMeter.OnAdvance(timeline, 3);
        InstantChangeMeter<int, int> jumpOnlyMeter = new(timeline, 50, 0, 100);
        DiscreteReachThresholdEvent<int, int> onDiscrete = new(discreteMeter, 100, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        ReachThresholdEvent<int, int> onJumpOnly = new(jumpOnlyMeter, 60, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(onJumpOnly);
        timeline.AddEvent(onDiscrete);
        MostRecentEventHolder<int> occurred = new();
        onDiscrete.Occurring += occurred.OnTrigger;
        onJumpOnly.Occurring += occurred.OnTrigger;
        discreteMeter.AddInstantChange(10);
        jumpOnlyMeter.AddInstantChange(30);

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        Assert.Same(onDiscrete, occurred.MostRecentEvent);

        timeline.SetOpen();
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        Assert.Same(onJumpOnly, occurred.MostRecentEvent);
    }
    [Fact(DisplayName = "A continuous meter's threshold and a discrete meter's threshold due at the same moment with the same priority are still put in order, without their transition data")]
    public void ContinuousAndDiscreteFallBackToUUID()
    {
        //A continuous meter holds fractions and a discrete one holds whole numbers, so their transition data is never the same type and isn't compared.
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> continuousMeter = new(timeline, 0, 0, 100);
        continuousMeter.SetDerivative(5);
        LinearDiscreteMeter<int, int> discreteMeter = new(timeline, 0, 0, 100);
        discreteMeter.SetDerivative(5, 2, out _);
        ReachThresholdEvent<int, double> onContinuous = new(continuousMeter, 20, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        DiscreteReachThresholdEvent<int, int> onDiscrete = new(discreteMeter, 10, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        Assert.Equal(onContinuous.TimeRemaining, onDiscrete.TimeRemaining); //Both in 4

        int order = onContinuous.CompareTo(onDiscrete);

        Assert.NotEqual(0, order);
        Assert.Equal(-order, onDiscrete.CompareTo(onContinuous));
        Assert.Equal(onContinuous.UUID.CompareTo(onDiscrete.UUID), order); //With nothing else to go on, it is the UUIDs
    }
    [Fact(DisplayName = "Giving them different priorities puts a continuous meter's threshold and a discrete meter's threshold in order")]
    public void DifferentPrioritiesOrderThem()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> continuousMeter = new(timeline, 0, 0, 100);
        continuousMeter.SetDerivative(5);
        LinearDiscreteMeter<int, int> discreteMeter = new(timeline, 0, 0, 100);
        discreteMeter.SetDerivative(5, 2, out _);
        ReachThresholdEvent<int, double> onContinuous = new(continuousMeter, 20, PriorityRank.MeterTriggerProgressive, timeline) { CouldOccur = true };
        DiscreteReachThresholdEvent<int, int> onDiscrete = new(discreteMeter, 10, PriorityRank.MeterLimit, timeline) { CouldOccur = true };

        Assert.True(onDiscrete.CompareTo(onContinuous) < 0);
        Assert.True(onContinuous.CompareTo(onDiscrete) > 0);
    }
    #endregion
}
