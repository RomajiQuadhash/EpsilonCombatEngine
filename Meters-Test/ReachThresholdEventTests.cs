using CoreMetrics;
using Meters;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Meters_Test;

/// <summary>
/// Tests for ReachThresholdEvent, and for what DiscreteReachThresholdEvent adds for discrete meters.
/// Thresholds at and beyond a meter's limits are tested in MeterLimitTests, and ticks that jump over one in DiscreteThresholdSkipOverTests.
/// </summary>
/// <remarks>Claude written, Romaji approved after a formatting change</remarks>
public class ReachThresholdEventTests
{
    #region Time remaining
    [Fact(DisplayName = "A ReachThresholdEvent's time is when its meter will get to the threshold")]
    public void TimeComesFromTheMeter()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(10);
        ReachThresholdEvent<int, double> threshold = new(meter, 80, PriorityRank.MeterLimit, timeline);

        Assert.Equal(new TimeOrNever<int>(3), threshold.TimeRemaining);
        Assert.Same(meter, threshold.OwningMeter);
        Assert.Equal(80, threshold.ThresholdPoint);
        Assert.Equal(PriorityRank.MeterLimit, threshold.Priority);
    }
    [Fact(DisplayName = "A ReachThresholdEvent's time follows the meter as its rate changes")]
    public void TimeFollowsTheMeter()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, double> threshold = new(meter, 80, PriorityRank.MeterLimit, timeline);
        Assert.True(threshold.TimeRemaining.IsNever); //The meter isn't moving

        meter.SetDerivative(10);
        Assert.Equal(new TimeOrNever<int>(3), threshold.TimeRemaining);

        meter.SetDerivative(15);
        Assert.Equal(new TimeOrNever<int>(2), threshold.TimeRemaining);

        meter.SetDerivative(-10);
        Assert.True(threshold.TimeRemaining.IsNever); //Now moving away
    }
    [Fact(DisplayName = "Moving the threshold point changes the time")]
    public void MovingTheThresholdPoint()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(10);
        ReachThresholdEvent<int, double> threshold = new(meter, 80, PriorityRank.MeterLimit, timeline)
        {
            ThresholdPoint = 60
        };

        Assert.Equal(new TimeOrNever<int>(1), threshold.TimeRemaining);
    }
    [Fact(DisplayName = "A ReachThresholdEvent's time can't be set, since it comes from the meter")]
    public void TimeCannotBeSet()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, double> threshold = new(meter, 80, PriorityRank.MeterLimit, timeline);

        Assert.Throws<InvalidOperationException>(() => threshold.TimeRemaining = new TimeOrNever<int>(5));
    }
    [Fact(DisplayName = "A ReachThresholdEvent whose meter is already on the threshold has a time of zero")]
    public void AlreadyOnTheThreshold()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 50, PriorityRank.MeterLimit, timeline);

        Assert.Equal(new TimeOrNever<int>(0), threshold.TimeRemaining);
    }
    #endregion
    #region Direction
    [Fact(DisplayName = "A threshold that only counts going up ignores a meter that is falling toward it")]
    public void UpOnlyIgnoresFalling()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(-10);
        ReachThresholdEvent<int, double> threshold = new(meter, 20, PriorityRank.MeterLimit, timeline);
        Assert.Equal(new TimeOrNever<int>(3), threshold.TimeRemaining);

        threshold.Direction = ThresholdDirection.Up;
        Assert.True(threshold.TimeRemaining.IsNever);

        threshold.Direction = ThresholdDirection.Down;
        Assert.Equal(new TimeOrNever<int>(3), threshold.TimeRemaining);
    }
    [Fact(DisplayName = "A threshold that only counts going down ignores a meter that is rising toward it")]
    public void DownOnlyIgnoresRising()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(10);
        ReachThresholdEvent<int, double> threshold = new(meter, 80, PriorityRank.MeterLimit, timeline) { Direction = ThresholdDirection.Down };

        Assert.True(threshold.TimeRemaining.IsNever);

        threshold.Direction = ThresholdDirection.Up;
        Assert.Equal(new TimeOrNever<int>(3), threshold.TimeRemaining);
    }
    #endregion
    #region Instant changes
    [Fact(DisplayName = "An instant change that takes the meter over the threshold makes the event due now")]
    public void InstantChangeAcrossMakesItDue()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 60, PriorityRank.MeterLimit, timeline);
        Assert.True(threshold.TimeRemaining.IsNever);

        meter.AddInstantChange(30);

        Assert.Equal(new TimeOrNever<int>(0), threshold.TimeRemaining);
    }
    [Fact(DisplayName = "An instant change that lands exactly on the threshold makes the event due now")]
    public void InstantChangeOntoMakesItDue()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 60, PriorityRank.MeterLimit, timeline);

        meter.AddInstantChange(10);

        Assert.Equal(new TimeOrNever<int>(0), threshold.TimeRemaining);
    }
    [Fact(DisplayName = "An instant change that stops short of the threshold doesn't make the event due")]
    public void InstantChangeShortOfIt()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 60, PriorityRank.MeterLimit, timeline);

        meter.AddInstantChange(9);

        Assert.True(threshold.TimeRemaining.IsNever);
    }
    [Fact(DisplayName = "An event made due by an instant change stays due after time advances, until it occurs")]
    public void StaysDueAfterAdvancing()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 60, PriorityRank.MeterLimit, timeline);
        meter.AddInstantChange(30);

        meter.OnAdvance(timeline, 0); //Clears the meter's record of the jump

        Assert.Equal(0, meter.InstantChange);
        Assert.Equal(new TimeOrNever<int>(0), threshold.TimeRemaining);
    }
    [Fact(DisplayName = "A threshold that only counts going up isn't made due by an instant change downward over it")]
    public void UpOnlyIgnoresAJumpDown()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 40, PriorityRank.MeterLimit, timeline) { Direction = ThresholdDirection.Up };

        meter.AddInstantChange(-30);

        Assert.True(threshold.TimeRemaining.IsNever);
    }
    [Fact(DisplayName = "A threshold that only counts going up stops being due if the meter jumps back down over it before it occurs")]
    public void JumpingBackUndoesIt()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 60, PriorityRank.MeterLimit, timeline) { Direction = ThresholdDirection.Up };
        meter.AddInstantChange(30);
        Assert.Equal(new TimeOrNever<int>(0), threshold.TimeRemaining);

        meter.AddInstantChange(-30);

        Assert.True(threshold.TimeRemaining.IsNever);
    }
    #endregion
    #region Occurring
    [Fact(DisplayName = "A ReachThresholdEvent occurs when the timeline reaches its time, with the meter on the threshold")]
    public void OccursWhenTheMeterGetsThere()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(10);
        ReachThresholdEvent<int, double> threshold = new(meter, 80, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> occurred = new();
        threshold.Occurring += occurred.OnTrigger;
        int totalAdvance = 0;

        foreach (StepReport<int> report in timeline.TakeSteps())
        {
            totalAdvance += report.AdvanceAmount;
        }

        Assert.Same(threshold, occurred.MostRecentEvent);
        Assert.Equal(3, totalAdvance);
        Assert.Equal(80, meter.CurrentValue);
    }
    [Fact(DisplayName = "On a whole-number timeline, a threshold reached partway through a time unit occurs at the start of that unit")]
    public void OccursEarlyOnAWholeNumberTimeline()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(10);
        ReachThresholdEvent<int, double> threshold = new(meter, 85, PriorityRank.MeterLimit, timeline) { CouldOccur = true }; //Reached at 3 and a half
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> occurred = new();
        threshold.Occurring += occurred.OnTrigger;
        int totalAdvance = 0;

        foreach (StepReport<int> report in timeline.TakeSteps())
        {
            totalAdvance += report.AdvanceAmount;
        }

        Assert.Same(threshold, occurred.MostRecentEvent);
        Assert.Equal(3, totalAdvance);
        Assert.Equal(80, meter.CurrentValue);
    }
    [Fact(DisplayName = "An event made due by an instant change occurs without the timeline moving")]
    public void OccursAtOnceAfterAnInstantChange()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 60, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> occurred = new();
        threshold.Occurring += occurred.OnTrigger;
        meter.AddInstantChange(30);
        int totalAdvance = 0;

        foreach (StepReport<int> report in timeline.TakeSteps())
        {
            totalAdvance += report.AdvanceAmount;
        }

        Assert.Same(threshold, occurred.MostRecentEvent);
        Assert.Equal(0, totalAdvance);
    }
    [Fact(DisplayName = "After occurring, a ReachThresholdEvent is off the timeline and no longer follows its meter")]
    public void StopsListeningAfterOccurring()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 60, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        meter.AddInstantChange(30);
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }

        Assert.DoesNotContain(threshold, timeline.Events);
        Assert.DoesNotContain(threshold, timeline.PossibleEvents);
        Assert.DoesNotContain(threshold, timeline.GetAdvanceSubscribers());

        //Going back under and over again doesn't make it due a second time
        meter.AddInstantChange(-30);
        meter.AddInstantChange(30);
        Assert.True(threshold.TimeRemaining.IsNever);
    }
    [Fact(DisplayName = "SubscribeToAll makes a ReachThresholdEvent that has occurred follow its meter again, so it can be put back on the timeline")]
    public void SubscribeToAllArmsItAgain()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 60, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        meter.AddInstantChange(30);
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        timeline.SetOpen();
        meter.AddInstantChange(-30);
        MostRecentEventHolder<int> occurredAgain = new();
        threshold.Occurring += occurredAgain.OnTrigger;

        threshold.SubscribeToAll();
        timeline.AddEvent(threshold);
        meter.AddInstantChange(30);
        Assert.Equal(new TimeOrNever<int>(0), threshold.TimeRemaining);
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }

        Assert.Same(threshold, occurredAgain.MostRecentEvent);
    }
    [Fact(DisplayName = "A ReachThresholdEvent can't be triggered before its meter is on the threshold")]
    public void CannotTriggerEarly()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(10);
        ReachThresholdEvent<int, double> threshold = new(meter, 80, PriorityRank.MeterLimit, timeline);

        Assert.Throws<InvalidOperationException>(threshold.Trigger);
    }
    [Fact(DisplayName = "A ReachThresholdEvent refuses to be advanced by a timeline that isn't its own")]
    public void RefusesAnotherTimeline()
    {
        Timeline<int> timeline = new();
        Timeline<int> otherTimeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 60, PriorityRank.MeterLimit, timeline);

        Assert.Throws<InvalidOperationException>(() => threshold.OnAdvance(otherTimeline, 1));
    }
    [Fact(DisplayName = "If the meter is moved past the threshold in one go, the event's time is how long ago it was crossed")]
    public void MovedPastGivesATimeInThePast()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 1000);
        meter.SetDerivative(10);
        ReachThresholdEvent<int, double> threshold = new(meter, 80, PriorityRank.MeterLimit, timeline);

        meter.OnAdvance(timeline, 5); //To 100, which got to 80 two time units ago

        Assert.Equal(new TimeOrNever<int>(-2), threshold.TimeRemaining);
    }
    #endregion
    #region Staying on the timeline
    [Fact(DisplayName = "A ReachThresholdEvent that can't currently occur is thrown away by the timeline unless CouldOccur is set")]
    public void ThrownAwayUnlessCouldOccur()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> kept = new(meter, 60, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        ReachThresholdEvent<int, int> thrownAway = new(meter, 70, PriorityRank.MeterLimit, timeline);
        timeline.AddEvent(kept);
        timeline.AddEvent(thrownAway);
        timeline.AddEvent(new BaseCard<int>(5, timeline)); //So the timeline has something to do
        Assert.False(thrownAway.CouldOccur);
        Assert.Contains(thrownAway, timeline.PossibleEvents);

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }

        Assert.Contains(kept, timeline.PossibleEvents);
        Assert.DoesNotContain(thrownAway, timeline.PossibleEvents);
        Assert.DoesNotContain(thrownAway, timeline.Events);
    }
    #endregion
    #region Transition data
    [Fact(DisplayName = "A ReachThresholdEvent that isn't going to occur has no transition data")]
    public void NoTransitionDataWhenNever()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, double> threshold = new(meter, 80, PriorityRank.MeterLimit, timeline);

        Assert.Null(threshold.TransitionData);
    }
    [Fact(DisplayName = "A threshold the meter is moving steadily toward has transition data with only the rate of change")]
    public void TransitionDataForSteadyChange()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 50, 0, 100);
        meter.SetDerivative(-10);
        ReachThresholdEvent<int, double> threshold = new(meter, 20, PriorityRank.MeterLimit, timeline);

        TransitionData<double>? data = threshold.TransitionData;

        Assert.NotNull(data);
        Assert.Equal(0, data.Before);
        Assert.Equal(0, data.After);
        Assert.Equal(10, data.ProgressiveChange); //Its size, whichever way the meter is going
        Assert.Equal(100, data.Range);
    }
    [Fact(DisplayName = "A threshold crossed by an instant change has transition data saying how far short it was before and how far past it is after")]
    public void TransitionDataForAnInstantChange()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 60, PriorityRank.MeterLimit, timeline);
        meter.AddInstantChange(30);

        TransitionData<int>? data = threshold.TransitionData;

        Assert.NotNull(data);
        Assert.Equal(-10, data.Before);
        Assert.Equal(20, data.After);
        Assert.Equal(0, data.ProgressiveChange);
    }
    #endregion
    #region Cloning
    [Fact(DisplayName = "A clone of a ReachThresholdEvent watches the clone of its meter and keeps its settings")]
    public void CloneWatchesTheClonedMeter()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 60, PriorityRank.PlayerKO, timeline)
        {
            CouldOccur = true,
            Direction = ThresholdDirection.Up,
            HaltsInstantAction = true
        };
        timeline.AddEvent(threshold);
        CloneContext context = new();
        Timeline<int> timelineClone = (Timeline<int>)timeline.Clone(context);

        Assert.True(context.TryGet(threshold, out ReachThresholdEvent<int, int> clone));
        Assert.True(context.TryGet(meter, out InstantChangeMeter<int, int> meterClone)); //Cloning the event brought its meter along
        Assert.NotSame(threshold, clone);
        Assert.Same(meterClone, clone.OwningMeter);
        Assert.Same(timelineClone, clone.OwningTimeline);
        Assert.Equal(60, clone.ThresholdPoint);
        Assert.Equal(PriorityRank.PlayerKO, clone.Priority);
        Assert.Equal(ThresholdDirection.Up, clone.Direction);
        Assert.True(clone.CouldOccur);
        Assert.True(clone.HaltsInstantAction);
        Assert.Equal(threshold.UUID, clone.UUID);
        Assert.Contains(clone, timelineClone.PossibleEvents);
    }
    [Fact(DisplayName = "A cloned ReachThresholdEvent is made due by its own meter and not by the original")]
    public void CloneFollowsOnlyItsOwnMeter()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 60, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        CloneContext context = new();
        timeline.Clone(context);
        Assert.True(context.TryGet(threshold, out ReachThresholdEvent<int, int> clone));
        Assert.True(context.TryGet(meter, out InstantChangeMeter<int, int> meterClone));

        meterClone.AddInstantChange(30);

        Assert.Equal(new TimeOrNever<int>(0), clone.TimeRemaining);
        Assert.True(threshold.TimeRemaining.IsNever);
    }
    [Fact(DisplayName = "A clone of an event that an instant change has already made due is due too")]
    public void CloneKeepsBeingDue()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        ReachThresholdEvent<int, int> threshold = new(meter, 60, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        meter.AddInstantChange(30);
        meter.OnAdvance(timeline, 0); //So the meter itself no longer remembers the jump
        CloneContext context = new();
        timeline.Clone(context);

        Assert.True(context.TryGet(threshold, out ReachThresholdEvent<int, int> clone));
        Assert.Equal(new TimeOrNever<int>(0), clone.TimeRemaining);
    }
    #endregion
    #region DiscreteReachThresholdEvent
    [Fact(DisplayName = "A DiscreteReachThresholdEvent's transition data for a tick still to come has the tick rate and counts that tick as the change")]
    public void DiscreteTransitionDataBeforeTheTick()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 97, 0, 1000);
        meter.SetDerivative(5, 10, out _);
        DiscreteReachThresholdEvent<int, int> threshold = new(meter, 100, PriorityRank.MeterLimit, timeline);

        DiscreteTransitionData<int, int>? data = threshold.TransitionData;

        Assert.NotNull(data);
        Assert.Equal(10, data.TickRate);
        Assert.True(data.JustTicked);
        Assert.Equal(0, data.InstantChange);
        Assert.Equal(5, data.ProgressiveChange);
        Assert.Equal(-3, data.Before); //97 is 3 short
        Assert.Equal(2, data.After);   //102 is 2 past
    }
    [Fact(DisplayName = "A DiscreteReachThresholdEvent's transition data is the same once that tick has landed")]
    public void DiscreteTransitionDataAfterTheTick()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 97, 0, 1000);
        meter.SetDerivative(5, 10, out _);
        DiscreteReachThresholdEvent<int, int> threshold = new(meter, 100, PriorityRank.MeterLimit, timeline);

        meter.OnAdvance(timeline, 10);
        DiscreteTransitionData<int, int>? data = threshold.TransitionData;

        Assert.Equal(new TimeOrNever<int>(0), threshold.TimeRemaining);
        Assert.NotNull(data);
        Assert.True(data.JustTicked);
        Assert.Equal(0, data.InstantChange);
        Assert.Equal(-3, data.Before);
        Assert.Equal(2, data.After);
    }
    [Fact(DisplayName = "A discrete threshold crossed by an instant change partway through a tick has transition data for the jump and not for a tick")]
    public void DiscreteTransitionDataForAnInstantChange()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 97, 0, 1000);
        meter.SetDerivative(5, 10, out _);
        DiscreteReachThresholdEvent<int, int> threshold = new(meter, 100, PriorityRank.MeterLimit, timeline);
        meter.OnAdvance(timeline, 4);

        meter.AddInstantChange(10);
        DiscreteTransitionData<int, int>? data = threshold.TransitionData;

        Assert.Equal(new TimeOrNever<int>(0), threshold.TimeRemaining);
        Assert.NotNull(data);
        Assert.False(data.JustTicked);
        Assert.Equal(10, data.InstantChange);
        Assert.Equal(10, data.TickRate);
        Assert.Equal(-3, data.Before);
        Assert.Equal(7, data.After);
    }
    [Fact(DisplayName = "A clone of a DiscreteReachThresholdEvent is a DiscreteReachThresholdEvent on the clone of its meter")]
    public void DiscreteCloneKeepsItsKind()
    {
        Timeline<int> timeline = new();
        LinearDiscreteMeter<int, int> meter = new(timeline, 97, 0, 1000);
        meter.SetDerivative(5, 10, out _);
        DiscreteReachThresholdEvent<int, int> threshold = new(meter, 100, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        CloneContext context = new();
        timeline.Clone(context);

        Assert.True(context.TryGet(threshold, out DiscreteReachThresholdEvent<int, int> clone));
        Assert.True(context.TryGet(meter, out LinearDiscreteMeter<int, int> meterClone));
        Assert.Same(meterClone, clone.OwningMeter);
        Assert.Equal(threshold.UUID, clone.UUID);
        Assert.Equal(new TimeOrNever<int>(10), clone.TimeRemaining);
    }
    #endregion
}
