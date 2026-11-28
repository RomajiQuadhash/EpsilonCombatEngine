using Combatants;
using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Combatants_Test;

/// <summary>
/// Tests for BaseCombatant, the bare minimum of a combatant: health, speed, and being up, knocked out or dead.
/// </summary>
/// <remarks>Claude written, Romaji </remarks>
public class BaseCombatantTests
{
    #region Up
    [Fact(DisplayName = "A new combatant is up, counts its health meter's value as its health, and is on its health meter's timeline")]
    public void StartsUp()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 80, 0, 100, defense, new ModifierGroupCounter());

        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);

        Assert.Equal(CombatantState.Up, combatant.State);
        Assert.True(combatant.IsActive);
        Assert.False(combatant.IsKOd);
        Assert.False(combatant.IsDead);
        Assert.Equal(80, combatant.Health);
        Assert.Same(health, combatant.HealthMeter);
        Assert.Same(timeline, combatant.Speed.OwningTimeline);
        Assert.Same(timeline, combatant.OwningTimeline);
        Assert.NotNull(combatant.PendingKnockOut);
    }
    [Fact(DisplayName = "A combatant makes its own speed meter on its health's timeline, from the speed stat and the shared aggregator it is given")]
    public void MakesItsSpeedMeter()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 80, 0, 100, defense, new ModifierGroupCounter());
        StatModificationMeter<int> otherDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> otherHealth = new(timeline, 80, 0, 100, otherDefense, new ModifierGroupCounter());
        MaxStatAggregator<int, SpeedMeter<int>> aggregator = new();

        BaseCombatant<int> slow = new(health, aggregator, 10, new Rational<long>(-4), new Rational<long>(3), PriorityRank.PlayerKO);
        BaseCombatant<int> fast = new(otherHealth, aggregator, 20, new Rational<long>(-4), new Rational<long>(3), PriorityRank.EnemyKO);

        Assert.Equal(10, slow.Speed.BaseStat);
        Assert.Equal(new Rational<long>(-4), slow.Speed.MinValue);
        Assert.Equal(new Rational<long>(3), slow.Speed.MaxValue);
        Assert.Same(timeline, slow.Speed.OwningTimeline);
        Assert.Same(aggregator, slow.Speed.SpeedAggregator);
        Assert.Contains(slow.Speed, aggregator.Meters);
        Assert.Contains(fast.Speed, aggregator.Meters);
        Assert.Equal(new Rational<long>(1, 2), slow.Speed.SpeedFraction);
        Assert.Equal(Rational<long>.One, fast.Speed.SpeedFraction);
    }
    [Fact(DisplayName = "A combatant shows the combat stats it has, whichever of its parts holds them: Defense always, and GuardStrength and the Guard meter if its health has a Guard")]
    public void ShowsItsCombatStats()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 80, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        StatModificationMeter<int> guardedDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int>.BuildGuard(timeline, 25, new Rational<long>(-4), new Rational<long>(3), out LinearContinuousMeter<int, double> guard, out StatModificationMeter<int> guardStrength);
        HealthMeter<int> guardedHealth = new(timeline, 80, 0, 100, guardedDefense, new ModifierGroupCounter(), guard, guardStrength);
        BaseCombatant<int> guarded = new(guardedHealth, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);

        Assert.Same(defense, combatant.ChangeableStats[CombatStats.Defense]);
        Assert.Single(combatant.ChangeableStats);
        Assert.Empty(combatant.AdditionalMeters);

        Assert.Same(guardedDefense, guarded.ChangeableStats[CombatStats.Defense]);
        Assert.Same(guardStrength, guarded.ChangeableStats[CombatStats.GuardStrength]);
        Assert.Equal(2, guarded.ChangeableStats.Count);
        Assert.Same(guard, guarded.AdditionalMeters[MeterTypes.Guard]);
        Assert.Single(guarded.AdditionalMeters);
    }
    [Fact(DisplayName = "What a base combatant doesn't implement reads as empty, null or false, and setting it is ignored")]
    public void UnimplementedPartsAreIgnored()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 80, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);

        combatant.StatusEffects = new Dictionary<StatusEffects, AbstractCombatant<int>?> { [StatusEffects.Sleep] = combatant };
        combatant.StatusEffects.Add(StatusEffects.Silence, null);
        combatant.ChangeableStats = new Dictionary<CombatStats, StatModificationMeter<int>>();
        combatant.AdditionalMeters = new Dictionary<MeterTypes, IMeter<int>> { [MeterTypes.MP] = defense };
        combatant.OnOccur(combatant, new BaseCard<int>(5, timeline));

        Assert.Empty(combatant.StatusEffects);
        Assert.Null(combatant.TurnCard);
        Assert.Null(combatant.TurnHolder.Current);
        Assert.False(combatant.IsBleeding);
        Assert.False(combatant.IsOtherwiseIncapacitated);
        Assert.Same(defense, combatant.ChangeableStats[CombatStats.Defense]);
        Assert.Empty(combatant.AdditionalMeters);
        Assert.True(combatant.IsActive);
    }
    #endregion
    #region Knocked out
    [Fact(DisplayName = "A combatant whose health is taken to 0 by damage over time is knocked out, and nothing over time changes its health after that")]
    public void KnockedOutByDamageOverTime()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 20, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        //10 a tick, so 0 at 10
        health.AddDamage(new ModInstance<int, int>(100, 5));
        Card<int, string> beforeKnockOut = new(9, timeline, PriorityRank.None);
        timeline.AddEvent(beforeKnockOut);
        Card<int, string> longAfter = new(40, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        beforeKnockOut.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, beforeKnockOut))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsActive);
        Assert.Equal(10, combatant.Health);

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(CombatantState.KnockedOut, combatant.State);
        Assert.True(combatant.IsKOd);
        Assert.False(combatant.IsActive);
        Assert.Equal(0, combatant.Health);
        Assert.Null(combatant.PendingKnockOut);
        Assert.True(health.IsDown);
    }
    [Fact(DisplayName = "An instant heal on a knocked out combatant changes its health meter, but the combatant still counts as having 0 health and stays knocked out")]
    public void HealingDoesNotBringBackFromKnockOut()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 20, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        Card<int, string> afterKnockOut = new(1, timeline, PriorityRank.None);
        timeline.AddEvent(afterKnockOut);
        Card<int, string> afterHeal = new(20, timeline, PriorityRank.None);
        timeline.AddEvent(afterHeal);
        MostRecentEventHolder<int> reached = new();
        afterKnockOut.Occurring += reached.OnTrigger;
        afterHeal.Occurring += reached.OnTrigger;
        health.ApplyInstantDamage(new Rational<long>(1000));
        while (!ReferenceEquals(reached.MostRecentEvent, afterKnockOut))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsKOd);

        health.AddInstantChange(50);
        //Healing over time is added too, and never ticks
        health.Add(new ModInstance<int, int>(5, 1));

        while (!ReferenceEquals(reached.MostRecentEvent, afterHeal))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(50, health.CurrentValue);
        Assert.Equal(0, combatant.Health);
        Assert.True(combatant.IsKOd);
    }
    #endregion
    #region Revive
    [Fact(DisplayName = "Reviving a knocked out combatant brings it back up at the given health, and it can be knocked out again")]
    public void ReviveBringsBackAndCanBeKnockedOutAgain()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 20, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        Card<int, string> afterFirstKnockOut = new(1, timeline, PriorityRank.None);
        timeline.AddEvent(afterFirstKnockOut);
        Card<int, string> afterSecondKnockOut = new(2, timeline, PriorityRank.None);
        timeline.AddEvent(afterSecondKnockOut);
        MostRecentEventHolder<int> reached = new();
        afterFirstKnockOut.Occurring += reached.OnTrigger;
        afterSecondKnockOut.Occurring += reached.OnTrigger;
        health.ApplyInstantDamage(new Rational<long>(1000));
        while (!ReferenceEquals(reached.MostRecentEvent, afterFirstKnockOut))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsKOd);

        Assert.True(combatant.Revive(30));

        Assert.Equal(CombatantState.Up, combatant.State);
        Assert.Equal(30, combatant.Health);
        Assert.False(health.IsDown);
        Assert.NotNull(combatant.PendingKnockOut);

        health.ApplyInstantDamage(new Rational<long>(1000));
        while (!ReferenceEquals(reached.MostRecentEvent, afterSecondKnockOut))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsKOd);
        Assert.Equal(0, combatant.Health);
    }
    [Fact(DisplayName = "Reviving does nothing to a combatant that is up, and the health to come back at has to be above 0 and within the maximum")]
    public void ReviveOnlyWorksOnTheKnockedOut()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 20, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        health.AddDamage(new ModInstance<int, int>(10, 5));

        Assert.False(combatant.Revive(50));

        Assert.Equal(20, combatant.Health);
        Assert.Single(health.DamageModifiers);
        Assert.Throws<ArgumentOutOfRangeException>(() => combatant.Revive(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => combatant.Revive(101));
        Assert.Throws<ArgumentOutOfRangeException>(() => combatant.CreateReviveCard(5, 0, PriorityRank.PlayerRes));
        Assert.Throws<ArgumentOutOfRangeException>(() => combatant.CreateReviveCard(5, 101, PriorityRank.PlayerRes));
    }
    [Fact(DisplayName = "A revive card brings a knocked out combatant back up when it occurs, at the health on its face")]
    public void ReviveCardRevivesWhenItOccurs()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 20, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        timeline.AddEvent(combatant.CreateReviveCard(5, 60, PriorityRank.PlayerRes));
        Card<int, string> beforeRevive = new(4, timeline, PriorityRank.None);
        timeline.AddEvent(beforeRevive);
        Card<int, string> afterRevive = new(6, timeline, PriorityRank.None);
        timeline.AddEvent(afterRevive);
        MostRecentEventHolder<int> reached = new();
        beforeRevive.Occurring += reached.OnTrigger;
        afterRevive.Occurring += reached.OnTrigger;
        health.ApplyInstantDamage(new Rational<long>(1000));

        while (!ReferenceEquals(reached.MostRecentEvent, beforeRevive))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsKOd);
        Assert.Equal(0, combatant.Health);

        while (!ReferenceEquals(reached.MostRecentEvent, afterRevive))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsActive);
        Assert.Equal(60, combatant.Health);
    }
    #endregion
    #region Dead
    [Fact(DisplayName = "A combatant that can't be killed isn't, and one that can is dead from then on: 0 health, no revive, and its revive card does nothing")]
    public void KillOnlyWorksIfItCanBeKilled()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 20, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        timeline.AddEvent(combatant.CreateReviveCard(5, 60, PriorityRank.PlayerRes));
        Card<int, string> afterRevive = new(6, timeline, PriorityRank.None);
        timeline.AddEvent(afterRevive);
        MostRecentEventHolder<int> reached = new();
        afterRevive.Occurring += reached.OnTrigger;

        Assert.False(combatant.Kill());
        Assert.True(combatant.IsActive);

        combatant.CanBeKilled = true;
        Assert.True(combatant.Kill());

        Assert.Equal(CombatantState.Dead, combatant.State);
        Assert.True(combatant.IsDead);
        Assert.False(combatant.IsActive);
        Assert.Equal(0, combatant.Health);
        Assert.Null(combatant.PendingKnockOut);
        Assert.True(health.IsDown);
        Assert.False(combatant.Revive(50));

        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsDead);
        Assert.Equal(0, combatant.Health);
    }
    [Fact(DisplayName = "A knocked out combatant can still be killed, and then can't be revived")]
    public void KnockedOutCanBeKilled()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 20, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO) { CanBeKilled = true };
        Card<int, string> afterKnockOut = new(1, timeline, PriorityRank.None);
        timeline.AddEvent(afterKnockOut);
        MostRecentEventHolder<int> reached = new();
        afterKnockOut.Occurring += reached.OnTrigger;
        health.ApplyInstantDamage(new Rational<long>(1000));
        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsKOd);

        Assert.True(combatant.Kill());

        Assert.True(combatant.IsDead);
        Assert.False(combatant.IsKOd);
        Assert.False(combatant.Revive(50));
    }
    #endregion
    #region Cloning
    [Fact(DisplayName = "A cloned combatant has its own meters and the same UUID, and being knocked out on the clone leaves the original up")]
    public void CloneIsKOdSeparately()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 20, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO) { CanBeKilled = true };
        health.AddDamage(new ModInstance<int, int>(100, 5));
        Card<int, string> longAfter = new(40, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        longAfter.Occurring += reached.OnTrigger;

        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone(combatant);
        context.GetOrClone(reached);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(timeline, out Timeline<int> timelineClone));
        Assert.True(context.TryGet(combatant, out BaseCombatant<int> combatantClone));
        Assert.True(context.TryGet(health, out HealthMeter<int> healthClone));
        Assert.True(context.TryGet(combatant.Speed, out SpeedMeter<int> speedClone));
        Assert.True(context.TryGet(reached, out MostRecentEventHolder<int> reachedClone));

        Assert.NotSame(combatant, combatantClone);
        Assert.Equal(combatant.UUID, combatantClone.UUID);
        Assert.Same(healthClone, combatantClone.HealthMeter);
        Assert.Same(speedClone, combatantClone.Speed);
        Assert.Same(timelineClone, combatantClone.OwningTimeline);
        Assert.True(combatantClone.CanBeKilled);
        Assert.Equal(PriorityRank.PlayerKO, combatantClone.KnockOutPriority);
        Assert.NotNull(combatantClone.PendingKnockOut);
        Assert.NotSame(combatant.PendingKnockOut, combatantClone.PendingKnockOut);

        while (reachedClone.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timelineClone.TakeSteps()) { }
            timelineClone.SetOpen();
        }
        Assert.True(combatantClone.IsKOd);
        Assert.Equal(0, combatantClone.Health);
        Assert.True(combatant.IsActive);
        Assert.Equal(20, combatant.Health);
        Assert.NotNull(combatant.PendingKnockOut);
    }
    [Fact(DisplayName = "A combatant cloned while knocked out is knocked out too, and reviving the clone leaves the original knocked out")]
    public void CloneOfAKnockedOutCombatantIsKOd()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 20, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        Card<int, string> afterKnockOut = new(1, timeline, PriorityRank.None);
        timeline.AddEvent(afterKnockOut);
        MostRecentEventHolder<int> reached = new();
        afterKnockOut.Occurring += reached.OnTrigger;
        health.ApplyInstantDamage(new Rational<long>(1000));
        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone(combatant);
        context.GetOrClone(reached);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(combatant, out BaseCombatant<int> combatantClone));

        Assert.True(combatantClone.IsKOd);
        Assert.True(combatantClone.Revive(40));

        Assert.True(combatantClone.IsActive);
        Assert.Equal(40, combatantClone.Health);
        Assert.True(combatant.IsKOd);
        Assert.Equal(0, combatant.Health);
    }
    #endregion
}
