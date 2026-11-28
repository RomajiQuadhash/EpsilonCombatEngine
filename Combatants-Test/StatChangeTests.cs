using Combatants;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Combatants_Test;

/// <summary>
/// Tests for StatChange, which makes buffs and debuffs for a combatant's stats.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class StatChangeTests
{
    [Fact(DisplayName = "A stat change adds its Ratio to the combatant's stat when it applies and takes it back off when its time is up")]
    public void AppliesAndRemoves()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        //Applies at 2 and is removed at 9
        TempInstantChange<int, Rational<long>> buff = StatChange.Create(combatant, CombatStats.Defense, new Rational<long>(1, 2), 2, 7, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd);
        timeline.AddEvent(buff);
        Card<int, string> beforeApply = new(1, timeline, PriorityRank.None);
        timeline.AddEvent(beforeApply);
        Card<int, string> whileApplied = new(5, timeline, PriorityRank.None);
        timeline.AddEvent(whileApplied);
        Card<int, string> afterRemove = new(10, timeline, PriorityRank.None);
        timeline.AddEvent(afterRemove);
        MostRecentEventHolder<int> reached = new();
        beforeApply.Occurring += reached.OnTrigger;
        whileApplied.Occurring += reached.OnTrigger;
        afterRemove.Occurring += reached.OnTrigger;

        Assert.Same(defense, buff.MeterAppliedTo);
        Assert.Same(timeline, buff.OwningTimeline);
        while (!ReferenceEquals(reached.MostRecentEvent, beforeApply))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(new Rational<long>(10), defense.GetModifiedStat());

        while (!ReferenceEquals(reached.MostRecentEvent, whileApplied))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(new Rational<long>(1, 2), defense.CurrentValue);
        Assert.Equal(new Rational<long>(15), defense.GetModifiedStat());

        while (!ReferenceEquals(reached.MostRecentEvent, afterRemove))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(Rational<long>.Zero, defense.CurrentValue);
        Assert.True(buff.IsFinished);
    }
    [Fact(DisplayName = "Speed is changed through the same call as any other stat, and the change shows on the combatant's speed")]
    public void SpeedIsChangedLikeAnyOtherStat()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        TempInstantChange<int, Rational<long>> debuff = StatChange.Create(combatant, CombatStats.Speed, new Rational<long>(-1), 1, 50, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd);
        timeline.AddEvent(debuff);
        Card<int, string> whileApplied = new(2, timeline, PriorityRank.None);
        timeline.AddEvent(whileApplied);
        MostRecentEventHolder<int> reached = new();
        whileApplied.Occurring += reached.OnTrigger;

        Assert.Same(combatant.Speed, debuff.MeterAppliedTo);
        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(new Rational<long>(5), combatant.Speed.GetModifiedStat());
    }
    [Fact(DisplayName = "Two changes that overlap at a stat's limit each take back only what they actually added, so the stat ends where it started")]
    public void OverlappingChangesAtALimitTakeBackOnlyWhatTheyAdded()
    {
        Timeline<int> timeline = new();
        //The Ratio can't go above +3
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(3));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        //The first is on from 1 to 11 and the second from 2 to 12
        TempInstantChange<int, Rational<long>> first = StatChange.Create(combatant, CombatStats.Defense, new Rational<long>(2), 1, 10, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd);
        timeline.AddEvent(first);
        TempInstantChange<int, Rational<long>> second = StatChange.Create(combatant, CombatStats.Defense, new Rational<long>(2), 2, 10, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd);
        timeline.AddEvent(second);
        Card<int, string> bothApplied = new(5, timeline, PriorityRank.None);
        timeline.AddEvent(bothApplied);
        Card<int, string> firstRemoved = new(11, timeline, PriorityRank.None);
        timeline.AddEvent(firstRemoved);
        Card<int, string> bothRemoved = new(13, timeline, PriorityRank.None);
        timeline.AddEvent(bothRemoved);
        MostRecentEventHolder<int> reached = new();
        bothApplied.Occurring += reached.OnTrigger;
        firstRemoved.Occurring += reached.OnTrigger;
        bothRemoved.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, bothApplied))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //The second only had room for 1 of its 2
        Assert.Equal(new Rational<long>(3), defense.CurrentValue);
        Assert.Equal(new Rational<long>(2), first.AppliedChange);
        Assert.Equal(new Rational<long>(1), second.AppliedChange);

        while (!ReferenceEquals(reached.MostRecentEvent, firstRemoved))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //The first took back its 2
        Assert.Equal(new Rational<long>(1), defense.CurrentValue);

        while (!ReferenceEquals(reached.MostRecentEvent, bothRemoved))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //And the second took back only its 1
        Assert.Equal(Rational<long>.Zero, defense.CurrentValue);
    }
    [Fact(DisplayName = "A stat change for a stat the combatant doesn't have is refused with an exception that names the combatant, the stat, and the stats it does have")]
    public void MissingStatIsRefused()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);

        MissingStatException missing = Assert.Throws<MissingStatException>(() =>
            StatChange.Create(combatant, CombatStats.Attack, new Rational<long>(1), 1, 10, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd));

        Assert.Same(combatant, missing.Combatant);
        Assert.Equal(CombatStats.Attack, missing.Stat);
        Assert.Contains(CombatStats.Speed, missing.StatsItHas);
        Assert.Contains(CombatStats.Defense, missing.StatsItHas);
        Assert.DoesNotContain(CombatStats.Attack, missing.StatsItHas);
        Assert.Contains("Attack", missing.Message);
        //A combatant with no Guard has no GuardStrength either
        Assert.Throws<MissingStatException>(() =>
            StatChange.Create(combatant, CombatStats.GuardStrength, new Rational<long>(1), 1, 10, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd));
    }
}
