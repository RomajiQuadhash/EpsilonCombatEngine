using Combatants;
using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Combatants_Test;

/// <summary>
/// Tests for BleedEvent, a damage phase that also names the combatant it is on and who caused it.
/// What a combatant does about bleeding (going below 0, dying) isn't tested here, since no combatant implements bleeding yet.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class BleedEventTests
{
    [Fact(DisplayName = "A bleed is a damage phase on its target's health, and says who is bleeding and who caused it")]
    public void IsADamagePhaseOnTheTargetsHealth()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 100, 0, 100, targetDefense, new ModifierGroupCounter());
        BaseCombatant<int> target = new(targetHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        StatModificationMeter<int> ownerDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> ownerHealth = new(timeline, 100, 0, 100, ownerDefense, new ModifierGroupCounter());
        BaseCombatant<int> owner = new(ownerHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.EnemyKO);
        //Runs from 1 to 11 at 10 a tick
        BleedEvent<int> bleed = new(target, owner, new ModInstance<int, int>(100, 5), 1, 10, PriorityRank.PlayerBleedStart, PriorityRank.PlayerBleedEnd);
        timeline.AddEvent(bleed);
        Card<int, string> during = new(8, timeline, PriorityRank.None);
        timeline.AddEvent(during);
        Card<int, string> after = new(20, timeline, PriorityRank.None);
        timeline.AddEvent(after);
        MostRecentEventHolder<int> reached = new();
        during.Occurring += reached.OnTrigger;
        after.Occurring += reached.OnTrigger;

        Assert.Same(target, bleed.Target);
        Assert.Same(owner, bleed.Owner);
        Assert.Same(targetHealth, bleed.MeterAppliedTo);
        Assert.Same(timeline, bleed.OwningTimeline);
        Assert.Equal(PriorityRank.PlayerBleedStart, bleed.Priority);

        while (!ReferenceEquals(reached.MostRecentEvent, during))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Single(targetHealth.DamageModifiers);
        Assert.Equal(90, targetHealth.CurrentValue);
        Assert.Equal(PriorityRank.PlayerBleedEnd, bleed.Priority);

        while (!ReferenceEquals(reached.MostRecentEvent, after))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(bleed.IsFinished);
        Assert.Empty(targetHealth.DamageModifiers);
        Assert.Equal(80, targetHealth.CurrentValue);
        Assert.Equal(100, ownerHealth.CurrentValue);
    }
    [Fact(DisplayName = "A combatant can have several bleeds at once, with or without an owner, and each one's damage is on its health")]
    public void SeveralBleedsAtOnce()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 100, 0, 100, targetDefense, new ModifierGroupCounter());
        BaseCombatant<int> target = new(targetHealth, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        BleedEvent<int> first = new(target, target, new ModInstance<int, int>(100, 5), 1, 20, PriorityRank.PlayerBleedStart, PriorityRank.PlayerBleedEnd);
        timeline.AddEvent(first);
        BleedEvent<int> second = new(target, null, new ModInstance<int, int>(50, 5), 2, 20, PriorityRank.PlayerBleedStart, PriorityRank.PlayerBleedEnd);
        timeline.AddEvent(second);
        Card<int, string> during = new(4, timeline, PriorityRank.None);
        timeline.AddEvent(during);
        MostRecentEventHolder<int> reached = new();
        during.Occurring += reached.OnTrigger;

        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Null(second.Owner);
        Assert.Equal(2, targetHealth.DamageModifiers.Count);
        //20 and 10 per time unit before Defense, so 3 after it
        Assert.Equal(new Rational<long>(-3), targetHealth.TrueRate);
    }
    [Fact(DisplayName = "A bleed ended early is continued by another bleed, on the same combatant and from the same owner, for the time that was left")]
    public void ContinuationIsABleed()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 100, 0, 100, targetDefense, new ModifierGroupCounter());
        BaseCombatant<int> target = new(targetHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        StatModificationMeter<int> ownerDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> ownerHealth = new(timeline, 100, 0, 100, ownerDefense, new ModifierGroupCounter());
        BaseCombatant<int> owner = new(ownerHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.EnemyKO);
        //Runs from 1 to 21
        BleedEvent<int> bleed = new(target, owner, new ModInstance<int, int>(100, 5), 1, 20, PriorityRank.PlayerBleedStart, PriorityRank.PlayerBleedEnd);
        timeline.AddEvent(bleed);
        Card<int, string> endAt = new(8, timeline, PriorityRank.None);
        timeline.AddEvent(endAt);
        MostRecentEventHolder<int> reached = new();
        endAt.Occurring += reached.OnTrigger;
        Assert.Throws<InvalidOperationException>(() => bleed.CreateContinuation(3));
        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(13, bleed.EndEarly());

        BleedEvent<int> continuation = Assert.IsType<BleedEvent<int>>(bleed.CreateContinuation(3));

        Assert.Same(target, continuation.Target);
        Assert.Same(owner, continuation.Owner);
        Assert.Same(targetHealth, continuation.MeterAppliedTo);
        Assert.Equal(new ModInstance<int, int>(100, 5), continuation.Damage);
        Assert.Equal(13, continuation.ApplyTime);
        Assert.Equal(3, continuation.TimeToEvent);
        Assert.Equal(PriorityRank.PlayerBleedStart, continuation.ApplyPriority);
        Assert.Equal(PriorityRank.PlayerBleedEnd, continuation.RemovePriority);
    }
    [Fact(DisplayName = "A cloned bleed is a bleed on the cloned combatant's health, names the cloned owner, and ends on the clone without touching the original")]
    public void CloneBleedsTheClone()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 100, 0, 100, targetDefense, new ModifierGroupCounter());
        BaseCombatant<int> target = new(targetHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        StatModificationMeter<int> ownerDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> ownerHealth = new(timeline, 100, 0, 100, ownerDefense, new ModifierGroupCounter());
        BaseCombatant<int> owner = new(ownerHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.EnemyKO);
        //Runs from 1 to 11
        BleedEvent<int> bleed = new(target, owner, new ModInstance<int, int>(100, 5), 1, 10, PriorityRank.PlayerBleedStart, PriorityRank.PlayerBleedEnd);
        timeline.AddEvent(bleed);
        Card<int, string> cloneAt = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(cloneAt);
        MostRecentEventHolder<int> reachedCloneAt = new();
        cloneAt.Occurring += reachedCloneAt.OnTrigger;
        Card<int, string> end = new(20, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> reachedEnd = new();
        end.Occurring += reachedEnd.OnTrigger;
        while (reachedCloneAt.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone(target);
        context.GetOrClone(owner);
        context.GetOrClone(reachedCloneAt);
        context.GetOrClone(reachedEnd);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(timeline, out Timeline<int> timelineClone));
        Assert.True(context.TryGet<AbstractCombatant<int>>(target, out AbstractCombatant<int> targetClone));
        Assert.True(context.TryGet<AbstractCombatant<int>>(owner, out AbstractCombatant<int> ownerClone));
        Assert.True(context.TryGet<DamagePhaseEvent<int>>(bleed, out DamagePhaseEvent<int> clonedEvent));
        Assert.True(context.TryGet(reachedEnd, out MostRecentEventHolder<int> reachedEndClone));

        BleedEvent<int> bleedClone = Assert.IsType<BleedEvent<int>>(clonedEvent);
        Assert.Same(targetClone, bleedClone.Target);
        Assert.Same(ownerClone, bleedClone.Owner);
        Assert.Same(targetClone.HealthMeter, bleedClone.MeterAppliedTo);
        Assert.Same(timelineClone, bleedClone.OwningTimeline);
        Assert.Equal(bleed.Key, bleedClone.Key);

        while (reachedEndClone.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timelineClone.TakeSteps()) { }
            timelineClone.SetOpen();
        }
        Assert.True(bleedClone.IsFinished);
        Assert.Equal(80, targetClone.HealthMeter.CurrentValue);
        Assert.Empty(targetClone.HealthMeter.DamageModifiers);
        Assert.False(bleed.IsFinished);
        Assert.Equal(100, targetHealth.CurrentValue);
        Assert.Single(targetHealth.DamageModifiers);
    }
}
