using Combatants;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Combatants_Test;

/// <summary>
/// Tests for the damage a Combatant is dealing to others: it remembers the damage phases and bleeds it makes,
/// and they stop when it is knocked out, killed, nullified or paused.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class CombatantDealingTests
{
    [Fact(DisplayName = "A combatant remembers the damage phases and bleeds it makes for as long as they are running, and a bleed it makes is owned by it")]
    public void RemembersWhatItIsDealing()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> attackerDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> attackerHealth = new(timeline, 100, 0, 100, attackerDefense, new ModifierGroupCounter());
        Combatant<int> attacker = new(attackerHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 1000, 0, 1000, targetDefense, new ModifierGroupCounter());
        Combatant<int> target = new(targetHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.EnemyKO, PriorityRank.EnemyDeath);
        Assert.Empty(attacker.Dealing);

        //From 1 to 6, and a bleed from 1 to 21
        DamagePhaseEvent<int> damagePhase = attacker.CreateDamagePhase(targetHealth, new ModInstance<int, int>(100, 5), 1, 5, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd);
        timeline.AddEvent(damagePhase);
        BleedEvent<int> bleed = attacker.CreateBleed(target, new ModInstance<int, int>(50, 5), 1, 20, PriorityRank.EnemyBleedStart, PriorityRank.EnemyBleedEnd);
        timeline.AddEvent(bleed);
        Card<int, string> afterFirstEnds = new(8, timeline, PriorityRank.None);
        timeline.AddEvent(afterFirstEnds);
        MostRecentEventHolder<int> reached = new();
        afterFirstEnds.Occurring += reached.OnTrigger;

        Assert.Same(targetHealth, damagePhase.MeterAppliedTo);
        Assert.Same(attacker, bleed.Owner);
        Assert.Same(target, bleed.Target);
        Assert.Equal(2, attacker.Dealing.Count);
        Assert.Empty(target.Dealing);

        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(damagePhase.IsFinished);
        Assert.Same(bleed, Assert.Single(attacker.Dealing));
    }
    [Fact(DisplayName = "When a combatant is knocked out, the damage it was dealing ends, and what it had left is never dealt")]
    public void KnockOutEndsWhatItWasDealing()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> attackerDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> attackerHealth = new(timeline, 100, 0, 100, attackerDefense, new ModifierGroupCounter());
        Combatant<int> attacker = new(attackerHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 1000, 0, 1000, targetDefense, new ModifierGroupCounter());
        //Would run from 1 to 51 at 10 a tick
        DamagePhaseEvent<int> damagePhase = attacker.CreateDamagePhase(targetHealth, new ModInstance<int, int>(100, 5), 1, 50, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd);
        timeline.AddEvent(damagePhase);
        Card<int, string> knockOutAt = new(8, timeline, PriorityRank.None);
        timeline.AddEvent(knockOutAt);
        Card<int, string> longAfter = new(60, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        knockOutAt.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;
        while (!ReferenceEquals(reached.MostRecentEvent, knockOutAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(990, targetHealth.CurrentValue);

        attackerHealth.ApplyInstantDamage(new Rational<long>(10000));

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(attacker.IsKOd);
        Assert.True(damagePhase.HasEndedEarly);
        Assert.True(damagePhase.IsFinished);
        Assert.Empty(attacker.Dealing);
        Assert.Empty(targetHealth.DamageModifiers);
        //The tick at 6, and the one that was in progress at 8 and landed at 11. Nothing after that
        Assert.Equal(980, targetHealth.CurrentValue);
    }
    [Fact(DisplayName = "Killing a combatant ends the damage it was dealing")]
    public void KillEndsWhatItWasDealing()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> attackerDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> attackerHealth = new(timeline, 100, 0, 100, attackerDefense, new ModifierGroupCounter());
        Combatant<int> attacker = new(attackerHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath) { CanBeKilled = true };
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 1000, 0, 1000, targetDefense, new ModifierGroupCounter());
        DamagePhaseEvent<int> damagePhase = attacker.CreateDamagePhase(targetHealth, new ModInstance<int, int>(100, 5), 1, 50, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd);
        timeline.AddEvent(damagePhase);
        Card<int, string> killAt = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(killAt);
        MostRecentEventHolder<int> reached = new();
        killAt.Occurring += reached.OnTrigger;
        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Single(targetHealth.DamageModifiers);

        Assert.True(attacker.Kill());

        Assert.True(damagePhase.HasEndedEarly);
        Assert.Empty(targetHealth.DamageModifiers);
        Assert.Empty(attacker.Dealing);
    }
    [Fact(DisplayName = "While a combatant is Nullified its attack keeps running but deals nothing, so only what is left of the attack when Nullified ends is dealt")]
    public void NullifiedLosesTheDamageFromWhileItLasted()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> attackerDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> attackerHealth = new(timeline, 100, 0, 100, attackerDefense, new ModifierGroupCounter());
        Combatant<int> attacker = new(attackerHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 1000, 0, 1000, targetDefense, new ModifierGroupCounter());
        //Runs from 1 to 21 at 10 a tick, which left alone would be 4 ticks
        DamagePhaseEvent<int> damagePhase = attacker.CreateDamagePhase(targetHealth, new ModInstance<int, int>(100, 5), 1, 20, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd);
        timeline.AddEvent(damagePhase);
        //Nullified from 0 to 16, which leaves the attack 5 of its 20, enough for one tick
        timeline.AddEvent(new StatusCard<int>(attacker, StatusEffects.Nullified, null, 0, 16, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline));
        Card<int, string> whileNullified = new(10, timeline, PriorityRank.None);
        timeline.AddEvent(whileNullified);
        Card<int, string> afterNullified = new(17, timeline, PriorityRank.None);
        timeline.AddEvent(afterNullified);
        Card<int, string> longAfter = new(40, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        whileNullified.Occurring += reached.OnTrigger;
        afterNullified.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, whileNullified))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //The attack has started and is still the one the attacker is dealing, but none of its damage is on the target
        Assert.True(attacker.HasStatus(StatusEffects.Nullified));
        Assert.True(damagePhase.IsSuppressed);
        Assert.False(damagePhase.HasEndedEarly);
        Assert.Equal(ApplyOrRemove.Remove, damagePhase.LifeCyclePhase);
        Assert.Same(damagePhase, Assert.Single(attacker.Dealing));
        Assert.Empty(targetHealth.DamageModifiers);
        Assert.Equal(1000, targetHealth.CurrentValue);

        while (!ReferenceEquals(reached.MostRecentEvent, afterNullified))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.False(damagePhase.IsSuppressed);
        Assert.Single(targetHealth.DamageModifiers);
        //It still ends when it always was going to
        Assert.Equal(4, damagePhase.TimeToEvent);

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(damagePhase.IsFinished);
        Assert.Empty(targetHealth.DamageModifiers);
        Assert.Empty(attacker.Dealing);
        //One tick, at 21, where the whole attack would have been four
        Assert.Equal(990, targetHealth.CurrentValue);
    }
    [Fact(DisplayName = "An attack that runs out while its dealer is Nullified deals nothing at all, and an attack made while Nullified starts out dealing nothing")]
    public void NullifiedCanOutlastAnAttack()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> attackerDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> attackerHealth = new(timeline, 100, 0, 100, attackerDefense, new ModifierGroupCounter());
        Combatant<int> attacker = new(attackerHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 1000, 0, 1000, targetDefense, new ModifierGroupCounter());
        //Runs from 1 to 21, all of it inside the Nullified from 0 to 30
        DamagePhaseEvent<int> damagePhase = attacker.CreateDamagePhase(targetHealth, new ModInstance<int, int>(100, 5), 1, 20, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd);
        timeline.AddEvent(damagePhase);
        timeline.AddEvent(new StatusCard<int>(attacker, StatusEffects.Nullified, null, 0, 30, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline));
        Card<int, string> whileNullified = new(10, timeline, PriorityRank.None);
        timeline.AddEvent(whileNullified);
        Card<int, string> longAfter = new(60, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        whileNullified.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;
        while (!ReferenceEquals(reached.MostRecentEvent, whileNullified))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        //Made at 10, to run from 11 to 51. Nullified ends at 30, so it deals for its last 21
        DamagePhaseEvent<int> madeWhileNullified = attacker.CreateDamagePhase(targetHealth, new ModInstance<int, int>(100, 5), 1, 40, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd);
        timeline.AddEvent(madeWhileNullified);
        Assert.True(madeWhileNullified.IsSuppressed);

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //The first ended on time without ever dealing anything, and wasn't cut short
        Assert.True(damagePhase.IsFinished);
        Assert.False(damagePhase.HasEndedEarly);
        Assert.Null(damagePhase.Key);
        //The second dealt from 30: ticks at 35, 40, 45 and 50, and the one in progress when it ended at 51 landed at 55
        Assert.True(madeWhileNullified.IsFinished);
        Assert.Equal(950, targetHealth.CurrentValue);
        Assert.Empty(targetHealth.DamageModifiers);
        Assert.Empty(attacker.Dealing);
    }
    [Fact(DisplayName = "A combatant that is Paused has the damage it was dealing stop, and carry on for the time it had left when the pause ends")]
    public void PausedStopsAndResumesWhatItWasDealing()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> attackerDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> attackerHealth = new(timeline, 100, 0, 100, attackerDefense, new ModifierGroupCounter());
        Combatant<int> attacker = new(attackerHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 1000, 0, 1000, targetDefense, new ModifierGroupCounter());
        //Would run from 1 to 21
        DamagePhaseEvent<int> damagePhase = attacker.CreateDamagePhase(targetHealth, new ModInstance<int, int>(100, 5), 1, 20, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd);
        timeline.AddEvent(damagePhase);
        //Paused from 8 to 18, when the damage phase had 13 left
        timeline.AddEvent(new StatusCard<int>(attacker, StatusEffects.Paused, null, 8, 10, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline));
        Card<int, string> duringPause = new(13, timeline, PriorityRank.None);
        timeline.AddEvent(duringPause);
        Card<int, string> afterPause = new(20, timeline, PriorityRank.None);
        timeline.AddEvent(afterPause);
        Card<int, string> longAfter = new(50, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        duringPause.Occurring += reached.OnTrigger;
        afterPause.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, duringPause))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(attacker.IsOtherwiseIncapacitated);
        Assert.True(damagePhase.HasEndedEarly);
        Assert.Equal(13, damagePhase.TimeLeftWhenEnded);
        Assert.Empty(targetHealth.DamageModifiers);
        Assert.Empty(attacker.Dealing);

        while (!ReferenceEquals(reached.MostRecentEvent, afterPause))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //A new damage phase for the 13 that were left, from 18 to 31
        Assert.Single(targetHealth.DamageModifiers);
        DamagePhaseEvent<int> continuation = Assert.Single(attacker.Dealing);
        Assert.NotSame(damagePhase, continuation);
        Assert.Equal(13, continuation.ApplyTime);

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(continuation.IsFinished);
        Assert.Empty(targetHealth.DamageModifiers);
        Assert.Empty(attacker.Dealing);
        //Ticks at 6 and 11 before the pause (the one in progress at 8 still landed), and at 23, 28 and 33 after it
        Assert.Equal(950, targetHealth.CurrentValue);
    }
}
