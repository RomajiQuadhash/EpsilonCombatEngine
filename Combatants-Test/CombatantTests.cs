using Combatants;
using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Combatants_Test;

/// <summary>
/// Tests for the stats and meters a Combatant is given, and for cloning one along with its statuses, its bleeds and what it is dealing.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class CombatantTests
{
    [Fact(DisplayName = "A combatant shows the stats and meters it was given alongside the ones from its speed and health, and a stat change works on them")]
    public void ShowsTheStatsAndMetersItWasGiven()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int>.BuildGuard(timeline, 25, new Rational<long>(-4), new Rational<long>(3), out LinearContinuousMeter<int, double> guard, out StatModificationMeter<int> guardStrength);
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter(), guard, guardStrength);
        StatModificationMeter<int> attack = new(timeline, 12, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> precision = new(timeline, 8, new Rational<long>(-6), new Rational<long>(6));
        LinearDiscreteMeter<int, int> mp = new(timeline, 30, 0, 50);
        Combatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath,
            new Dictionary<CombatStats, StatModificationMeter<int>> { [CombatStats.Attack] = attack, [CombatStats.Precision] = precision },
            new Dictionary<MeterTypes, IMeter<int>> { [MeterTypes.MP] = mp });

        Assert.Same(attack, combatant.ChangeableStats[CombatStats.Attack]);
        Assert.Same(precision, combatant.ChangeableStats[CombatStats.Precision]);
        Assert.Same(combatant.Speed, combatant.ChangeableStats[CombatStats.Speed]);
        Assert.Same(defense, combatant.ChangeableStats[CombatStats.Defense]);
        Assert.Same(guardStrength, combatant.ChangeableStats[CombatStats.GuardStrength]);
        Assert.Equal(5, combatant.ChangeableStats.Count);
        Assert.Same(mp, combatant.AdditionalMeters[MeterTypes.MP]);
        Assert.Same(guard, combatant.AdditionalMeters[MeterTypes.Guard]);
        Assert.Equal(2, combatant.AdditionalMeters.Count);
        Assert.Equal(PriorityRank.PlayerDeath, combatant.DeathPriority);

        Assert.Same(attack, StatChange.Create(combatant, CombatStats.Attack, new Rational<long>(1), 1, 10, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd).MeterAppliedTo);
        //It wasn't given Evasion
        Assert.Throws<MissingStatException>(() => StatChange.Create(combatant, CombatStats.Evasion, new Rational<long>(1), 1, 10, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd));
    }
    [Fact(DisplayName = "A combatant can't be given a stat or meter that already comes from its speed or its health")]
    public void StatsFromSpeedAndHealthCannotBeGivenAgain()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        StatModificationMeter<int> another = new(timeline, 12, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> anotherGuard = new(timeline, 10, 0, 10);

        Assert.Throws<ArgumentException>(() => new Combatant<int>(health, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath,
            new Dictionary<CombatStats, StatModificationMeter<int>> { [CombatStats.Defense] = another }));
        Assert.Throws<ArgumentException>(() => new Combatant<int>(health, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath,
            new Dictionary<CombatStats, StatModificationMeter<int>> { [CombatStats.Speed] = another }));
        Assert.Throws<ArgumentException>(() => new Combatant<int>(health, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath,
            null, new Dictionary<MeterTypes, IMeter<int>> { [MeterTypes.Guard] = anotherGuard }));
    }
    [Fact(DisplayName = "A cloned combatant has its own stats and meters, its statuses name the cloned owners, and what happens to the clone leaves the original as it was")]
    public void CloneCarriesStatusesBleedsAndDealing()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> attackerDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> attackerHealth = new(timeline, 100, 0, 100, attackerDefense, new ModifierGroupCounter());
        StatModificationMeter<int> attack = new(timeline, 12, new Rational<long>(-6), new Rational<long>(6));
        LinearDiscreteMeter<int, int> mp = new(timeline, 30, 0, 50);
        Combatant<int> attacker = new(attackerHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath,
            new Dictionary<CombatStats, StatModificationMeter<int>> { [CombatStats.Attack] = attack },
            new Dictionary<MeterTypes, IMeter<int>> { [MeterTypes.MP] = mp });
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 100, 0, 100, targetDefense, new ModifierGroupCounter());
        Combatant<int> target = new(targetHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.EnemyKO, PriorityRank.EnemyDeath) { CanBeKilled = true };
        //The attacker's bleed on the target runs from 1 to 11 at 1 a tick, and its Silence on the target from 1 to 31
        BleedEvent<int> bleed = attacker.CreateBleed(target, new ModInstance<int, int>(10, 5), 1, 10, PriorityRank.EnemyBleedStart, PriorityRank.EnemyBleedEnd);
        timeline.AddEvent(bleed);
        StatusCard<int> silence = new(target, StatusEffects.Silence, attacker, 1, 30, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(silence);
        Card<int, string> cloneAt = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(cloneAt);
        MostRecentEventHolder<int> reachedCloneAt = new();
        cloneAt.Occurring += reachedCloneAt.OnTrigger;
        Card<int, string> afterBleed = new(15, timeline, PriorityRank.None);
        timeline.AddEvent(afterBleed);
        MostRecentEventHolder<int> reachedAfterBleed = new();
        afterBleed.Occurring += reachedAfterBleed.OnTrigger;
        while (reachedCloneAt.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(target.IsBleeding);
        Assert.True(target.HasStatus(StatusEffects.Silence));

        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone<AbstractCombatant<int>>(attacker);
        context.GetOrClone<AbstractCombatant<int>>(target);
        context.GetOrClone(reachedCloneAt);
        context.GetOrClone(reachedAfterBleed);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(timeline, out Timeline<int> timelineClone));
        Assert.True(context.TryGet<AbstractCombatant<int>>(attacker, out AbstractCombatant<int> attackerCloneAsBase));
        Assert.True(context.TryGet<AbstractCombatant<int>>(target, out AbstractCombatant<int> targetCloneAsBase));
        Assert.True(context.TryGet(attack, out StatModificationMeter<int> attackClone));
        Assert.True(context.TryGet(mp, out LinearDiscreteMeter<int, int> mpClone));
        Assert.True(context.TryGet(reachedAfterBleed, out MostRecentEventHolder<int> reachedAfterBleedClone));
        Combatant<int> attackerClone = Assert.IsType<Combatant<int>>(attackerCloneAsBase);
        Combatant<int> targetClone = Assert.IsType<Combatant<int>>(targetCloneAsBase);

        Assert.Equal(attacker.UUID, attackerClone.UUID);
        Assert.Same(attackClone, attackerClone.ChangeableStats[CombatStats.Attack]);
        Assert.Same(mpClone, attackerClone.AdditionalMeters[MeterTypes.MP]);
        Assert.Equal(PriorityRank.PlayerDeath, attackerClone.DeathPriority);
        Assert.Equal(PriorityRank.EnemyDeath, targetClone.DeathPriority);
        Assert.True(targetClone.CanBeKilled);

        //The clone's status names the cloned owner, and its event is the cloned event
        Assert.True(targetClone.HasStatus(StatusEffects.Silence));
        Assert.Same(attackerClone, targetClone.StatusEffects[StatusEffects.Silence]);
        Assert.NotSame(silence, targetClone.StatusEventFor(StatusEffects.Silence));
        Assert.Same(targetClone, targetClone.StatusEventFor(StatusEffects.Silence)!.Target);

        //The clone is bleeding from the cloned bleed, which the cloned attacker is dealing
        Assert.True(targetClone.IsBleeding);
        Assert.Equal(targetClone.BleedMinimum, targetClone.HealthMeter.MinValue);
        BleedEvent<int> bleedClone = Assert.IsType<BleedEvent<int>>(Assert.Single(attackerClone.Dealing));
        Assert.NotSame(bleed, bleedClone);
        Assert.Same(targetClone, bleedClone.Target);
        Assert.Same(attackerClone, bleedClone.Owner);

        while (reachedAfterBleedClone.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timelineClone.TakeSteps()) { }
            timelineClone.SetOpen();
        }
        //On the clone the bleed has ended. The original is still where it was
        Assert.False(targetClone.IsBleeding);
        Assert.Equal(0, targetClone.HealthMeter.MinValue);
        Assert.True(targetClone.IsActive);
        Assert.Empty(attackerClone.Dealing);
        Assert.True(targetClone.HasStatus(StatusEffects.Silence));
        Assert.True(target.IsBleeding);
        Assert.Equal(target.BleedMinimum, targetHealth.MinValue);
        Assert.Same(bleed, Assert.Single(attacker.Dealing));
    }
    [Fact(DisplayName = "When the timeline is cloned before the combatants, each event a combatant holds is cloned exactly once: the one the combatant's clone holds is the one on the cloned timeline")]
    public void EventsHeldByACombatantAreClonedOnce()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> attackerDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> attackerHealth = new(timeline, 100, 0, 100, attackerDefense, new ModifierGroupCounter());
        Combatant<int> attacker = new(attackerHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 100, 0, 100, targetDefense, new ModifierGroupCounter());
        Combatant<int> target = new(targetHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.EnemyKO, PriorityRank.EnemyDeath) { CanBeKilled = true };
        LinearContinuousMeter<int, double> meter = new(timeline, 0, -100, 100);
        //One status of each kind on the target, all owned by the attacker, and a bleed. Everything applies at 1 and none of it ends before 3
        StatusCard<int> byTime = new(target, StatusEffects.Silence, attacker, 1, 30, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(byTime);
        StatusThresholdEvent<int, double> byMeter = new(target, StatusEffects.Sleep, attacker, 1, meter, 50, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(byMeter);
        StatusDiscreteThresholdEvent<int, int> byTicks = new(target, StatusEffects.Unaware, attacker, 1, attackerHealth, 10, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(byTicks);
        BleedEvent<int> bleed = attacker.CreateBleed(target, new ModInstance<int, int>(10, 5), 1, 30, PriorityRank.EnemyBleedStart, PriorityRank.EnemyBleedEnd);
        timeline.AddEvent(bleed);
        Card<int, string> cloneAt = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(cloneAt);
        MostRecentEventHolder<int> reached = new();
        cloneAt.Occurring += reached.OnTrigger;
        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(3, target.StatusEffects.Count);
        Assert.True(target.IsBleeding);

        //The timeline first, so the events are reached before the combatants they are for
        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone<AbstractCombatant<int>>(attacker);
        context.GetOrClone<AbstractCombatant<int>>(target);
        context.GetOrClone(reached);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet<AbstractCombatant<int>>(attacker, out AbstractCombatant<int> attackerCloneAsBase));
        Assert.True(context.TryGet<AbstractCombatant<int>>(target, out AbstractCombatant<int> targetCloneAsBase));
        Assert.True(context.TryGet(byTime, out StatusCard<int> byTimeClone));
        Assert.True(context.TryGet(byMeter, out StatusThresholdEvent<int, double> byMeterClone));
        Assert.True(context.TryGet(byTicks, out StatusDiscreteThresholdEvent<int, int> byTicksClone));
        Assert.True(context.TryGet<DamagePhaseEvent<int>>(bleed, out DamagePhaseEvent<int> bleedClone));
        Combatant<int> attackerClone = Assert.IsType<Combatant<int>>(attackerCloneAsBase);
        Combatant<int> targetClone = Assert.IsType<Combatant<int>>(targetCloneAsBase);

        Assert.Same(byTimeClone, targetClone.StatusEventFor(StatusEffects.Silence));
        Assert.Same(byMeterClone, targetClone.StatusEventFor(StatusEffects.Sleep));
        Assert.Same(byTicksClone, targetClone.StatusEventFor(StatusEffects.Unaware));
        Assert.Same(bleedClone, Assert.Single(attackerClone.Dealing));
        Assert.Same(targetClone, byMeterClone.Target);
        Assert.Same(attackerClone, byMeterClone.Owner);
        Assert.Same(targetClone, byTicksClone.Target);
        Assert.Same(attackerClone, byTicksClone.Owner);
        Assert.Same(attackerClone, targetClone.StatusEffects[StatusEffects.Sleep]);
    }
    [Fact(DisplayName = "A cloned combatant that was paused still carries on what it was dealing when the pause ends on the clone")]
    public void CloneOfAPausedCombatantResumesDealing()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> attackerDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> attackerHealth = new(timeline, 100, 0, 100, attackerDefense, new ModifierGroupCounter());
        Combatant<int> attacker = new(attackerHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        StatModificationMeter<int> targetDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> targetHealth = new(timeline, 1000, 0, 1000, targetDefense, new ModifierGroupCounter());
        //Would run from 1 to 21, and is paused from 8 to 18
        timeline.AddEvent(attacker.CreateDamagePhase(targetHealth, new ModInstance<int, int>(100, 5), 1, 20, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd));
        timeline.AddEvent(new StatusCard<int>(attacker, StatusEffects.Paused, null, 8, 10, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline));
        Card<int, string> cloneAt = new(13, timeline, PriorityRank.None);
        timeline.AddEvent(cloneAt);
        MostRecentEventHolder<int> reachedCloneAt = new();
        cloneAt.Occurring += reachedCloneAt.OnTrigger;
        Card<int, string> afterPause = new(20, timeline, PriorityRank.None);
        timeline.AddEvent(afterPause);
        MostRecentEventHolder<int> reachedAfterPause = new();
        afterPause.Occurring += reachedAfterPause.OnTrigger;
        while (reachedCloneAt.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone<AbstractCombatant<int>>(attacker);
        context.GetOrClone(targetHealth);
        context.GetOrClone(reachedCloneAt);
        context.GetOrClone(reachedAfterPause);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(timeline, out Timeline<int> timelineClone));
        Assert.True(context.TryGet<AbstractCombatant<int>>(attacker, out AbstractCombatant<int> attackerCloneAsBase));
        Assert.True(context.TryGet(targetHealth, out HealthMeter<int> targetHealthClone));
        Assert.True(context.TryGet(reachedAfterPause, out MostRecentEventHolder<int> reachedAfterPauseClone));
        Combatant<int> attackerClone = Assert.IsType<Combatant<int>>(attackerCloneAsBase);
        Assert.True(attackerClone.HasStatus(StatusEffects.Paused));
        Assert.Empty(attackerClone.Dealing);

        while (reachedAfterPauseClone.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timelineClone.TakeSteps()) { }
            timelineClone.SetOpen();
        }
        Assert.False(attackerClone.HasStatus(StatusEffects.Paused));
        Assert.Equal(13, Assert.Single(attackerClone.Dealing).ApplyTime);
        Assert.Single(targetHealthClone.DamageModifiers);
        //The original is still paused at 13, dealing nothing
        Assert.True(attacker.HasStatus(StatusEffects.Paused));
        Assert.Empty(attacker.Dealing);
        Assert.Empty(targetHealth.DamageModifiers);
    }
}
