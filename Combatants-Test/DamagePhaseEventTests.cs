using Combatants;
using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Combatants_Test;

/// <summary>
/// Tests for DamagePhaseEvent, the damage phase of an attack on a HealthMeter.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class DamagePhaseEventTests
{
    [Fact(DisplayName = "A damage phase does nothing until it starts, deals its damage divided by the Defense while it lasts, and stops when it ends")]
    public void DamagesOnlyWhileItLasts()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        DamagePhaseEvent<int> damagePhase = new(health, new ModInstance<int, int>(100, 5), 2, 10, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd, timeline);
        timeline.AddEvent(damagePhase);
        Card<int, string> beforeStart = new(1, timeline, PriorityRank.None);
        timeline.AddEvent(beforeStart);
        Card<int, string> during = new(8, timeline, PriorityRank.None);
        timeline.AddEvent(during);
        Card<int, string> longAfter = new(30, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        beforeStart.Occurring += reached.OnTrigger;
        during.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, beforeStart))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Empty(health.DamageModifiers);
        Assert.Null(damagePhase.Key);

        while (!ReferenceEquals(reached.MostRecentEvent, during))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //Started at 2, so the first tick was at 7
        Assert.Single(health.DamageModifiers);
        Assert.Equal(990, health.CurrentValue);
        Assert.False(damagePhase.IsFinished);

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //Ended at 12, as the second tick landed
        Assert.Empty(health.DamageModifiers);
        Assert.Equal(980, health.CurrentValue);
        Assert.True(damagePhase.IsFinished);
        Assert.Equal(0, health.Derivative);
        Assert.Equal(0, health.MidAdvanceRateChanges);
    }
    [Fact(DisplayName = "A damage phase can't be made to last 0 or less, or with damage of 0 or less")]
    public void RefusesNoDurationOrNoDamage()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());

        Assert.Throws<ArgumentOutOfRangeException>(() => new DamagePhaseEvent<int>(health, new ModInstance<int, int>(100, 5), 1, 0, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd, timeline));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DamagePhaseEvent<int>(health, new ModInstance<int, int>(100, 5), 1, -3, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd, timeline));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DamagePhaseEvent<int>(health, new ModInstance<int, int>(0, 5), 1, 10, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd, timeline));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DamagePhaseEvent<int>(health, new ModInstance<int, int>(-100, 5), 1, 10, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd, timeline));
    }
    [Fact(DisplayName = "Damage that doesn't divide evenly by the Defense is still dealt exactly over the damage phase, with nothing left owed")]
    public void UnevenDamageIsDealtExactly()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 3, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        //7 every 5 against a Defense of 3 is 2 and 1/3 a tick. Over 30 that is 42 before Defense, so 14
        DamagePhaseEvent<int> damagePhase = new(health, new ModInstance<int, int>(7, 5), 1, 30, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd, timeline);
        timeline.AddEvent(damagePhase);
        Card<int, string> afterEnd = new(32, timeline, PriorityRank.None);
        timeline.AddEvent(afterEnd);
        MostRecentEventHolder<int> reached = new();
        afterEnd.Occurring += reached.OnTrigger;

        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(986, health.CurrentValue);
        Assert.Equal(Rational<long>.Zero, health.CurrentSpilloverFraction);
        Assert.True(damagePhase.IsFinished);
        Assert.Equal(0, health.MidAdvanceRateChanges);
    }
    [Fact(DisplayName = "A damage phase cloned partway through ends on the cloned health meter, and leaves the original's damage alone")]
    public void CloneEndsOnTheClonedMeter()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        DamagePhaseEvent<int> damagePhase = new(health, new ModInstance<int, int>(100, 5), 1, 10, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd, timeline);
        timeline.AddEvent(damagePhase);
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
        context.GetOrClone(health);
        context.GetOrClone(reachedCloneAt);
        context.GetOrClone(reachedEnd);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(timeline, out Timeline<int> timelineClone));
        Assert.True(context.TryGet(health, out HealthMeter<int> healthClone));
        Assert.True(context.TryGet(damagePhase, out DamagePhaseEvent<int> damagePhaseClone));
        Assert.True(context.TryGet(reachedEnd, out MostRecentEventHolder<int> reachedEndClone));
        Assert.Same(healthClone, damagePhaseClone.MeterAppliedTo);
        Assert.Equal(damagePhase.Key, damagePhaseClone.Key);

        while (reachedEndClone.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timelineClone.TakeSteps()) { }
            timelineClone.SetOpen();
        }
        //Ticks at 6 and 11 on the clone, and the damage phase ended at 11
        Assert.Equal(980, healthClone.CurrentValue);
        Assert.Empty(healthClone.DamageModifiers);
        Assert.True(damagePhaseClone.IsFinished);
        //The original is still at 3 with its damage running
        Assert.Equal(1000, health.CurrentValue);
        Assert.Single(health.DamageModifiers);
        Assert.False(damagePhase.IsFinished);
    }
}
