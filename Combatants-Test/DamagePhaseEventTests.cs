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
    [Fact(DisplayName = "A damage phase on a health meter with a Guard chips the Guard when it starts and lets it heal back after it ends, with nothing else needed")]
    public void ChipsAndHealsTheGuardByItself()
    {
        double scale = (double)BattleTuning.GuardScale.Numerator / BattleTuning.GuardScale.Denominator;
        int healTime = (int)(BattleTuning.GuardHealDuration.Numerator / BattleTuning.GuardHealDuration.Denominator);
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter(), guard, guardStrength);
        //Runs from 1 to 11 at a rate of 4 before Defense
        DamagePhaseEvent<int> damagePhase = new(health, new ModInstance<int, int>(20, 5), 1, 10, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd, timeline);
        timeline.AddEvent(damagePhase);
        Card<int, string> during = new(5, timeline, PriorityRank.None);
        timeline.AddEvent(during);
        Card<int, string> afterHeal = new(12 + healTime, timeline, PriorityRank.None);
        timeline.AddEvent(afterHeal);
        MostRecentEventHolder<int> reached = new();
        during.Occurring += reached.OnTrigger;
        afterHeal.Occurring += reached.OnTrigger;

        Assert.Equal(10, guard.CurrentValue);
        while (!ReferenceEquals(reached.MostRecentEvent, during))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(10 - 4 * scale, guard.CurrentValue, 9);

        while (!ReferenceEquals(reached.MostRecentEvent, afterHeal))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(damagePhase.IsFinished);
        Assert.Equal(10, guard.CurrentValue, 9);
        //4 over a Defense of 20 for 10 is 2
        Assert.Equal(998, health.CurrentValue);
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
    [Fact(DisplayName = "A damage phase ended early takes its damage off the health at once and says how much time it had left, and its continuation deals the rest later")]
    public void EndedEarlyAndContinued()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        //Runs from 1 to 21, at 10 a tick
        DamagePhaseEvent<int> damagePhase = new(health, new ModInstance<int, int>(100, 5), 1, 20, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd, timeline);
        timeline.AddEvent(damagePhase);
        Card<int, string> endAt = new(8, timeline, PriorityRank.None);
        timeline.AddEvent(endAt);
        Card<int, string> justAfter = new(9, timeline, PriorityRank.None);
        timeline.AddEvent(justAfter);
        Card<int, string> longAfter = new(40, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        endAt.Occurring += reached.OnTrigger;
        justAfter.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;
        Assert.Throws<InvalidOperationException>(() => damagePhase.CreateContinuation(3));

        while (!ReferenceEquals(reached.MostRecentEvent, endAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Single(health.DamageModifiers);

        Assert.Equal(13, damagePhase.EndEarly());

        Assert.True(damagePhase.HasEndedEarly);
        Assert.Equal(13, damagePhase.TimeLeftWhenEnded);
        Assert.Equal(0, damagePhase.WaitLeftWhenEnded);
        Assert.Empty(health.DamageModifiers);
        Assert.Equal(13, damagePhase.EndEarly());

        while (!ReferenceEquals(reached.MostRecentEvent, justAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(damagePhase.IsFinished);

        //Starts again at 12 and runs the 13 that were left, to 25
        DamagePhaseEvent<int> continuation = damagePhase.CreateContinuation(3);
        timeline.AddEvent(continuation);
        Assert.Same(health, continuation.MeterAppliedTo);
        Assert.Equal(new ModInstance<int, int>(100, 5), continuation.Damage);
        Assert.Equal(13, continuation.ApplyTime);
        Assert.Equal(PriorityRank.DamagePhaseStart, continuation.ApplyPriority);
        Assert.Equal(PriorityRank.DamagePhaseEnd, continuation.RemovePriority);

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(continuation.IsFinished);
        Assert.Empty(health.DamageModifiers);
        //Ticks at 6 and 11 from the first part (the tick in progress at 8 still landed), and at 17, 22 and 27 from the second (the same at 25)
        Assert.Equal(950, health.CurrentValue);
        Assert.Equal(0, health.MidAdvanceRateChanges);
    }
    [Fact(DisplayName = "A damage phase ended early before it started never deals any damage, and says how long it still had to wait and to run")]
    public void EndedEarlyBeforeItStarted()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        //Would run from 5 to 25
        DamagePhaseEvent<int> damagePhase = new(health, new ModInstance<int, int>(100, 5), 5, 20, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd, timeline);
        timeline.AddEvent(damagePhase);
        Card<int, string> endAt = new(2, timeline, PriorityRank.None);
        timeline.AddEvent(endAt);
        Card<int, string> longAfter = new(40, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        endAt.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;
        while (!ReferenceEquals(reached.MostRecentEvent, endAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        Assert.Equal(20, damagePhase.EndEarly());

        Assert.Equal(20, damagePhase.TimeLeftWhenEnded);
        Assert.Equal(3, damagePhase.WaitLeftWhenEnded);
        Assert.Equal(20, damagePhase.CreateContinuation(1).ApplyTime);

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(damagePhase.IsFinished);
        Assert.Null(damagePhase.Key);
        Assert.Empty(health.DamageModifiers);
        Assert.Equal(1000, health.CurrentValue);
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
