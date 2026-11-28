using Combatants;
using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Combatants_Test;

/// <summary>
/// Tests for the three status effect events. Each applies its status after a set time and occurs a second time to remove it:
/// after another set time, when a meter reaches a value, or when a ticking meter of whole numbers does.
/// What a combatant does when one occurs isn't tested here, since no combatant implements status effects yet.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class StatusEventTests
{
    #region A set time to remove
    [Fact(DisplayName = "A status card says which status it is, for which combatant and which owner, and starts out waiting to apply")]
    public void CardSaysWhatItIs()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 100, 0, 100, targetDefense, new ModifierGroupCounter());
        BaseCombatant<int> target = new(targetHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        StatModificationMeter<int> ownerDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> ownerHealth = new(timeline, 100, 0, 100, ownerDefense, new ModifierGroupCounter());
        BaseCombatant<int> owner = new(ownerHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.EnemyKO);

        StatusCard<int> status = new(target, StatusEffects.Silence, owner, 3, 7, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);

        IStatusEvent<int> asStatus = status; //This is fine since it is a unit test and checking the interface
        Assert.Equal(StatusEffects.Silence, asStatus.Status);
        Assert.Equal(StatusEffects.Silence, status.Face);
        Assert.Same(target, asStatus.Target);
        Assert.Same(owner, asStatus.Owner);
        Assert.Same(status, asStatus.AsOkazo);
        Assert.Equal(ApplyOrRemove.Apply, asStatus.LifeCyclePhase);
        Assert.False(asStatus.IsFinished);
        Assert.Equal(3, status.TimeToEvent);
        Assert.Equal(7, status.ApplyTime);
        Assert.Equal(PriorityRank.StatusEffectStart, status.Priority);
        Assert.Throws<ArgumentOutOfRangeException>(() => new StatusCard<int>(target, StatusEffects.Silence, owner, 3, -1, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline));
    }
    [Fact(DisplayName = "A status card occurs once to apply after its waiting time, and once more to remove after its apply time")]
    public void CardOccursTwice()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 100, 0, 100, targetDefense, new ModifierGroupCounter());
        BaseCombatant<int> target = new(targetHealth, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        //Applies at 3 and is removed at 10
        StatusCard<int> status = new(target, StatusEffects.Silence, null, 3, 7, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(status);
        MostRecentEventHolder<int> occurred = new();
        status.Occurring += occurred.OnTrigger;
        Card<int, string> beforeApply = new(2, timeline, PriorityRank.None);
        timeline.AddEvent(beforeApply);
        Card<int, string> whileApplied = new(9, timeline, PriorityRank.None);
        timeline.AddEvent(whileApplied);
        Card<int, string> afterRemove = new(11, timeline, PriorityRank.None);
        timeline.AddEvent(afterRemove);
        MostRecentEventHolder<int> reached = new();
        beforeApply.Occurring += reached.OnTrigger;
        whileApplied.Occurring += reached.OnTrigger;
        afterRemove.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, beforeApply))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Null(occurred.MostRecentEvent);
        Assert.Equal(ApplyOrRemove.Apply, status.LifeCyclePhase);

        while (!ReferenceEquals(reached.MostRecentEvent, whileApplied))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Same(status, occurred.MostRecentEvent);
        Assert.Equal(ApplyOrRemove.Remove, status.LifeCyclePhase);
        Assert.Equal(PriorityRank.StatusEffectEnd, status.Priority);
        Assert.Equal(1, status.TimeToEvent);
        Assert.False(status.IsFinished);
        occurred.MostRecentEvent = null;

        while (!ReferenceEquals(reached.MostRecentEvent, afterRemove))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Same(status, occurred.MostRecentEvent);
        Assert.True(status.IsFinished);
    }
    #endregion
    #region A meter's value to remove
    [Fact(DisplayName = "A status on a meter's threshold applies after its waiting time, whatever the meter does before then, and is removed when the meter reaches the value after that")]
    public void ThresholdAppliesByTimeAndRemovesByMeter()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 100, 0, 100, targetDefense, new ModifierGroupCounter());
        BaseCombatant<int> target = new(targetHealth, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        LinearContinuousMeter<int, double> meter = new(timeline, 0, -100, 100);
        //Applies at 4. The meter jumps over 50 before that, and comes back down to it at 16
        StatusThresholdEvent<int, double> status = new(target, StatusEffects.Sleep, null, 4, meter, 50, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(status);
        MostRecentEventHolder<int> occurred = new();
        status.Occurring += occurred.OnTrigger;
        Card<int, string> beforeApply = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(beforeApply);
        Card<int, string> afterApply = new(5, timeline, PriorityRank.None);
        timeline.AddEvent(afterApply);
        Card<int, string> beforeRemove = new(15, timeline, PriorityRank.None);
        timeline.AddEvent(beforeRemove);
        Card<int, string> afterRemove = new(17, timeline, PriorityRank.None);
        timeline.AddEvent(afterRemove);
        MostRecentEventHolder<int> reached = new();
        beforeApply.Occurring += reached.OnTrigger;
        afterApply.Occurring += reached.OnTrigger;
        beforeRemove.Occurring += reached.OnTrigger;
        afterRemove.Occurring += reached.OnTrigger;

        IStatusEvent<int> asStatus = status;
        Assert.Equal(StatusEffects.Sleep, asStatus.Status);
        Assert.Same(target, asStatus.Target);
        Assert.Null(asStatus.Owner);
        Assert.Same(status, asStatus.AsOkazo);
        Assert.Equal(new TimeOrNever<int>(4), status.TimeRemaining);
        Assert.Null(status.TransitionData);

        //Jumps straight over the value while the status is still waiting to apply
        meter.AddInstantChange(94);
        while (!ReferenceEquals(reached.MostRecentEvent, beforeApply))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Null(occurred.MostRecentEvent);
        Assert.Equal(ApplyOrRemove.Apply, status.LifeCyclePhase);
        Assert.Equal(new TimeOrNever<int>(1), status.TimeRemaining);

        while (!ReferenceEquals(reached.MostRecentEvent, afterApply))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Same(status, occurred.MostRecentEvent);
        Assert.Equal(ApplyOrRemove.Remove, status.LifeCyclePhase);
        Assert.Equal(PriorityRank.StatusEffectEnd, status.Priority);
        Assert.False(status.IsFinished);
        occurred.MostRecentEvent = null;

        //From 94 at 5, down 4 a time unit, so 50 at 16
        meter.SetDerivative(-4);
        while (!ReferenceEquals(reached.MostRecentEvent, beforeRemove))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Null(occurred.MostRecentEvent);

        while (!ReferenceEquals(reached.MostRecentEvent, afterRemove))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Same(status, occurred.MostRecentEvent);
        Assert.True(status.IsFinished);
    }
    [Fact(DisplayName = "A status on a ticking meter's threshold applies after its waiting time and is removed at the tick that reaches the value")]
    public void DiscreteThresholdAppliesByTimeAndRemovesAtTheTick()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 100, 0, 100, targetDefense, new ModifierGroupCounter());
        BaseCombatant<int> target = new(targetHealth, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        //10 a tick: 90 at 5, 80 at 10, 70 at 15
        targetHealth.AddDamage(new ModInstance<int, int>(100, 5));
        //Applies at 7, and is removed when health falls to 70
        StatusDiscreteThresholdEvent<int, int> status = new(target, StatusEffects.Unaware, target, 7, targetHealth, 70, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline)
        {
            Direction = ThresholdDirection.Down
        };
        timeline.AddEvent(status);
        MostRecentEventHolder<int> occurred = new();
        status.Occurring += occurred.OnTrigger;
        Card<int, string> beforeApply = new(6, timeline, PriorityRank.None);
        timeline.AddEvent(beforeApply);
        Card<int, string> beforeRemove = new(14, timeline, PriorityRank.None);
        timeline.AddEvent(beforeRemove);
        Card<int, string> afterRemove = new(16, timeline, PriorityRank.None);
        timeline.AddEvent(afterRemove);
        MostRecentEventHolder<int> reached = new();
        beforeApply.Occurring += reached.OnTrigger;
        beforeRemove.Occurring += reached.OnTrigger;
        afterRemove.Occurring += reached.OnTrigger;

        IStatusEvent<int> asStatus = status;
        Assert.Equal(StatusEffects.Unaware, asStatus.Status);
        Assert.Same(target, asStatus.Target);
        Assert.Same(target, asStatus.Owner);
        Assert.Null(status.TransitionData);

        while (!ReferenceEquals(reached.MostRecentEvent, beforeApply))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Null(occurred.MostRecentEvent);

        while (!ReferenceEquals(reached.MostRecentEvent, beforeRemove))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Same(status, occurred.MostRecentEvent);
        Assert.Equal(ApplyOrRemove.Remove, status.LifeCyclePhase);
        Assert.False(status.IsFinished);
        Assert.Equal(80, targetHealth.CurrentValue);
        Assert.Equal(new TimeOrNever<int>(1), status.TimeRemaining);
        occurred.MostRecentEvent = null;

        while (!ReferenceEquals(reached.MostRecentEvent, afterRemove))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Same(status, occurred.MostRecentEvent);
        Assert.True(status.IsFinished);
        Assert.Equal(70, targetHealth.CurrentValue);
    }
    [Fact(DisplayName = "A status event can be made to apply in the past, and each kind then applies as soon as the timeline runs")]
    public void WaitingTimeCanBeNegative()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 100, 0, 100, targetDefense, new ModifierGroupCounter());
        BaseCombatant<int> target = new(targetHealth, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        LinearContinuousMeter<int, double> meter = new(timeline, 0, -100, 100);
        StatusCard<int> card = new(target, StatusEffects.Silence, null, -2, 50, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(card);
        StatusThresholdEvent<int, double> threshold = new(target, StatusEffects.Sleep, null, -2, meter, 50, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(threshold);
        StatusDiscreteThresholdEvent<int, int> discrete = new(target, StatusEffects.Unaware, null, -2, targetHealth, 50, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(discrete);
        Card<int, string> later = new(5, timeline, PriorityRank.None);
        timeline.AddEvent(later);
        MostRecentEventHolder<int> reached = new();
        later.Occurring += reached.OnTrigger;

        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(ApplyOrRemove.Remove, card.LifeCyclePhase);
        Assert.Equal(ApplyOrRemove.Remove, threshold.LifeCyclePhase);
        Assert.Equal(ApplyOrRemove.Remove, discrete.LifeCyclePhase);
        Assert.False(card.IsFinished);
        Assert.False(threshold.IsFinished);
        Assert.False(discrete.IsFinished);
    }
    #endregion
    #region Cloning
    [Fact(DisplayName = "Cloned status events of all three kinds keep their kind, their status and where they are in applying and removing, and name the cloned combatants and meter")]
    public void ClonesNameTheClonedCombatants()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 100, 0, 100, targetDefense, new ModifierGroupCounter());
        BaseCombatant<int> target = new(targetHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        StatModificationMeter<int> ownerDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> ownerHealth = new(timeline, 100, 0, 100, ownerDefense, new ModifierGroupCounter());
        BaseCombatant<int> owner = new(ownerHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.EnemyKO);
        LinearContinuousMeter<int, double> meter = new(timeline, 0, -100, 100);
        //By 5, this one has applied and is waiting to be removed at 9
        StatusCard<int> applied = new(target, StatusEffects.Silence, owner, 2, 7, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(applied);
        //And these are still waiting to apply
        StatusCard<int> waiting = new(target, StatusEffects.Paused, null, 8, 7, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(waiting);
        StatusThresholdEvent<int, double> threshold = new(target, StatusEffects.Sleep, owner, 8, meter, 50, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(threshold);
        StatusDiscreteThresholdEvent<int, int> discrete = new(target, StatusEffects.Unaware, owner, 8, targetHealth, 80, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline)
        {
            Direction = ThresholdDirection.Down
        };
        timeline.AddEvent(discrete);
        Card<int, string> cloneAt = new(5, timeline, PriorityRank.None);
        timeline.AddEvent(cloneAt);
        MostRecentEventHolder<int> reached = new();
        cloneAt.Occurring += reached.OnTrigger;
        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone(target);
        context.GetOrClone(owner);
        context.GetOrClone(reached);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(timeline, out Timeline<int> timelineClone));
        Assert.True(context.TryGet<AbstractCombatant<int>>(target, out AbstractCombatant<int> targetClone));
        Assert.True(context.TryGet<AbstractCombatant<int>>(owner, out AbstractCombatant<int> ownerClone));
        Assert.True(context.TryGet(meter, out LinearContinuousMeter<int, double> meterClone));
        Assert.True(context.TryGet(applied, out StatusCard<int> appliedClone));
        Assert.True(context.TryGet(waiting, out StatusCard<int> waitingClone));
        Assert.True(context.TryGet(threshold, out StatusThresholdEvent<int, double> thresholdClone));
        Assert.True(context.TryGet(discrete, out StatusDiscreteThresholdEvent<int, int> discreteClone));

        Assert.NotSame(target, targetClone);
        Assert.NotSame(owner, ownerClone);

        Assert.Equal(StatusEffects.Silence, appliedClone.Status);
        Assert.Same(targetClone, appliedClone.Target);
        Assert.Same(ownerClone, appliedClone.Owner);
        Assert.Same(timelineClone, appliedClone.OwningTimeline);
        Assert.Equal(ApplyOrRemove.Remove, appliedClone.LifeCyclePhase);
        Assert.Equal(PriorityRank.StatusEffectEnd, appliedClone.Priority);
        Assert.Equal(4, appliedClone.TimeToEvent);
        Assert.Equal(7, appliedClone.ApplyTime);

        Assert.Equal(StatusEffects.Paused, waitingClone.Status);
        Assert.Null(waitingClone.Owner);
        Assert.Equal(ApplyOrRemove.Apply, waitingClone.LifeCyclePhase);
        Assert.Equal(3, waitingClone.TimeToEvent);

        Assert.Equal(StatusEffects.Sleep, thresholdClone.Status);
        Assert.Same(targetClone, thresholdClone.Target);
        Assert.Same(ownerClone, thresholdClone.Owner);
        Assert.Same(meterClone, thresholdClone.OwningMeter);
        Assert.Equal(50, thresholdClone.ThresholdPoint);
        Assert.Equal(ApplyOrRemove.Apply, thresholdClone.LifeCyclePhase);
        Assert.Equal(3, thresholdClone.TimeToApply);

        Assert.Equal(StatusEffects.Unaware, discreteClone.Status);
        Assert.Same(targetClone, discreteClone.Target);
        Assert.Same(ownerClone, discreteClone.Owner);
        Assert.Same(targetClone.HealthMeter, discreteClone.OwningMeter);
        Assert.Equal(ThresholdDirection.Down, discreteClone.Direction);
        Assert.Equal(3, discreteClone.TimeToApply);

        //The originals still name the originals
        Assert.Same(target, applied.Target);
        Assert.Same(owner, threshold.Owner);
        Assert.Same(targetHealth, discrete.OwningMeter);
    }
    [Fact(DisplayName = "A cloned status card that had already applied is removed on the clone's timeline, and the original is still waiting")]
    public void ClonedCardFinishesOnTheClone()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 100, 0, 100, targetDefense, new ModifierGroupCounter());
        BaseCombatant<int> target = new(targetHealth, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        StatusCard<int> status = new(target, StatusEffects.Silence, null, 2, 7, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(status);
        Card<int, string> cloneAt = new(5, timeline, PriorityRank.None);
        timeline.AddEvent(cloneAt);
        MostRecentEventHolder<int> reachedCloneAt = new();
        cloneAt.Occurring += reachedCloneAt.OnTrigger;
        Card<int, string> afterRemove = new(10, timeline, PriorityRank.None);
        timeline.AddEvent(afterRemove);
        MostRecentEventHolder<int> reachedEnd = new();
        afterRemove.Occurring += reachedEnd.OnTrigger;
        while (reachedCloneAt.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone(target);
        context.GetOrClone(reachedCloneAt);
        context.GetOrClone(reachedEnd);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(timeline, out Timeline<int> timelineClone));
        Assert.True(context.TryGet(status, out StatusCard<int> statusClone));
        Assert.True(context.TryGet(reachedEnd, out MostRecentEventHolder<int> reachedEndClone));

        while (reachedEndClone.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timelineClone.TakeSteps()) { }
            timelineClone.SetOpen();
        }
        Assert.True(statusClone.IsFinished);
        Assert.False(status.IsFinished);
        Assert.Equal(ApplyOrRemove.Remove, status.LifeCyclePhase);
        Assert.Equal(4, status.TimeToEvent);
    }
    #endregion
}
