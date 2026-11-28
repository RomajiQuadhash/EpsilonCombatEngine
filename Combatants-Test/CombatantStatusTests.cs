using Combatants;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Combatants_Test;

/// <summary>
/// Tests for the status effects on a Combatant: getting one from a status effect event and losing it when that event comes to remove it,
/// being immune to one it already has, and what each status does to the combatant.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class CombatantStatusTests
{
    [Fact(DisplayName = "A combatant has a status from when its event applies until that event removes it, and says who owns it and which event it was")]
    public void StatusComesAndGoesWithItsEvent()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        Combatant<int> combatant = new(health, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        StatModificationMeter<int> ownerDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> ownerHealth = new(timeline, 100, 0, 100, ownerDefense, new ModifierGroupCounter());
        Combatant<int> owner = new(ownerHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.EnemyKO, PriorityRank.EnemyDeath);
        //On from 2 to 7
        StatusCard<int> silence = new(combatant, StatusEffects.Silence, owner, 2, 5, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(silence);
        Card<int, string> before = new(1, timeline, PriorityRank.None);
        timeline.AddEvent(before);
        Card<int, string> during = new(4, timeline, PriorityRank.None);
        timeline.AddEvent(during);
        Card<int, string> after = new(8, timeline, PriorityRank.None);
        timeline.AddEvent(after);
        MostRecentEventHolder<int> reached = new();
        before.Occurring += reached.OnTrigger;
        during.Occurring += reached.OnTrigger;
        after.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, before))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.False(combatant.HasStatus(StatusEffects.Silence));
        Assert.Empty(combatant.StatusEffects);
        Assert.Null(combatant.StatusEventFor(StatusEffects.Silence));

        while (!ReferenceEquals(reached.MostRecentEvent, during))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.HasStatus(StatusEffects.Silence));
        Assert.Same(owner, combatant.StatusEffects[StatusEffects.Silence]);
        Assert.Same(silence, combatant.StatusEventFor(StatusEffects.Silence));
        Assert.Empty(owner.StatusEffects);

        while (!ReferenceEquals(reached.MostRecentEvent, after))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.False(combatant.HasStatus(StatusEffects.Silence));
        Assert.Empty(combatant.StatusEffects);
    }
    [Fact(DisplayName = "A combatant that already has a status doesn't get it again: the second event is cancelled, and the status stays until the first event removes it")]
    public void ImmuneToAStatusItAlreadyHas()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        Combatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        //The first is on from 1 to 11. The second, from the same owner, would have been on from 2 to 5
        StatusCard<int> first = new(combatant, StatusEffects.Silence, combatant, 1, 10, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(first);
        StatusCard<int> second = new(combatant, StatusEffects.Silence, combatant, 2, 3, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(second);
        Card<int, string> afterSecondApplied = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(afterSecondApplied);
        Card<int, string> afterSecondWouldEnd = new(6, timeline, PriorityRank.None);
        timeline.AddEvent(afterSecondWouldEnd);
        Card<int, string> afterFirstEnds = new(12, timeline, PriorityRank.None);
        timeline.AddEvent(afterFirstEnds);
        MostRecentEventHolder<int> reached = new();
        afterSecondApplied.Occurring += reached.OnTrigger;
        afterSecondWouldEnd.Occurring += reached.OnTrigger;
        afterFirstEnds.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, afterSecondApplied))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(second.IsCancelled);
        Assert.True(second.IsFinished);
        Assert.False(first.IsCancelled);
        Assert.True(combatant.HasStatus(StatusEffects.Silence));
        Assert.Same(first, combatant.StatusEventFor(StatusEffects.Silence));

        while (!ReferenceEquals(reached.MostRecentEvent, afterSecondWouldEnd))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.HasStatus(StatusEffects.Silence));

        while (!ReferenceEquals(reached.MostRecentEvent, afterFirstEnds))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.False(combatant.HasStatus(StatusEffects.Silence));
    }
    [Fact(DisplayName = "Once a status has been removed, the combatant can get it again from a new event")]
    public void StatusCanBeAppliedAgainAfterItEnds()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        Combatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        //On from 1 to 3, and then again from 5 to 9
        timeline.AddEvent(new StatusCard<int>(combatant, StatusEffects.Sleep, null, 1, 2, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline));
        StatusCard<int> again = new(combatant, StatusEffects.Sleep, null, 5, 4, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(again);
        Card<int, string> between = new(4, timeline, PriorityRank.None);
        timeline.AddEvent(between);
        Card<int, string> duringSecond = new(7, timeline, PriorityRank.None);
        timeline.AddEvent(duringSecond);
        MostRecentEventHolder<int> reached = new();
        between.Occurring += reached.OnTrigger;
        duringSecond.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, between))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.False(combatant.HasStatus(StatusEffects.Sleep));
        Assert.False(combatant.IsOtherwiseIncapacitated);

        while (!ReferenceEquals(reached.MostRecentEvent, duringSecond))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.HasStatus(StatusEffects.Sleep));
        Assert.False(again.IsCancelled);
        Assert.Null(combatant.StatusEffects[StatusEffects.Sleep]);
        //Asleep counts as unable to act without being knocked out or dead
        Assert.True(combatant.IsOtherwiseIncapacitated);
        Assert.True(combatant.IsActive);
    }
    [Fact(DisplayName = "Whoever owns a status can change when it ends, through the event the combatant gives for it")]
    public void OwnerCanChangeWhenAStatusEnds()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        Combatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        //Would be on from 1 to 21
        timeline.AddEvent(new StatusCard<int>(combatant, StatusEffects.Silence, combatant, 1, 20, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline));
        Card<int, string> changeAt = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(changeAt);
        Card<int, string> afterNewEnd = new(6, timeline, PriorityRank.None);
        timeline.AddEvent(afterNewEnd);
        MostRecentEventHolder<int> reached = new();
        changeAt.Occurring += reached.OnTrigger;
        afterNewEnd.Occurring += reached.OnTrigger;
        while (!ReferenceEquals(reached.MostRecentEvent, changeAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        //Now it ends at 5
        StatusCard<int> statusEvent = Assert.IsType<StatusCard<int>>(combatant.StatusEventFor(StatusEffects.Silence));
        statusEvent.TimeToEvent = 2;

        while (!ReferenceEquals(reached.MostRecentEvent, afterNewEnd))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.False(combatant.HasStatus(StatusEffects.Silence));
        Assert.True(statusEvent.IsFinished);
    }
    [Fact(DisplayName = "While a combatant is Intangible, time doesn't change its health, and it carries on from the same point afterwards")]
    public void IntangiblePausesHealth()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        Combatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        //10 a tick, with the first due at 5
        health.AddDamage(new ModInstance<int, int>(100, 5));
        //Intangible from 3 to 10
        timeline.AddEvent(new StatusCard<int>(combatant, StatusEffects.Intangible, null, 3, 7, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline));
        Card<int, string> during = new(9, timeline, PriorityRank.None);
        timeline.AddEvent(during);
        Card<int, string> after = new(13, timeline, PriorityRank.None);
        timeline.AddEvent(after);
        MostRecentEventHolder<int> reached = new();
        during.Occurring += reached.OnTrigger;
        after.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, during))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(health.IsIntangible);
        Assert.True(health.IsPaused);
        Assert.Equal(100, combatant.Health);

        while (!ReferenceEquals(reached.MostRecentEvent, after))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //3 of the tick before and 2 after, so it landed at 12
        Assert.False(health.IsIntangible);
        Assert.False(health.IsPaused);
        Assert.Equal(90, combatant.Health);
    }
    [Fact(DisplayName = "A dead combatant takes no notice of a status effect event")]
    public void DeadCombatantGetsNoStatus()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        Combatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath) { CanBeKilled = true };
        timeline.AddEvent(new StatusCard<int>(combatant, StatusEffects.Silence, null, 1, 20, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline));
        Card<int, string> during = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(during);
        MostRecentEventHolder<int> reached = new();
        during.Occurring += reached.OnTrigger;
        Assert.True(combatant.Kill());

        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.False(combatant.HasStatus(StatusEffects.Silence));
    }
    [Fact(DisplayName = "A knocked out combatant keeps its statuses, and reviving it clears them all and cancels their events, so it can get them afresh")]
    public void ReviveClearsStatuses()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        Combatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        //Both would be on from 1 to 31
        StatusCard<int> silence = new(combatant, StatusEffects.Silence, null, 1, 30, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(silence);
        StatusCard<int> intangible = new(combatant, StatusEffects.Intangible, null, 1, 30, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(intangible);
        Card<int, string> knockOutAt = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(knockOutAt);
        Card<int, string> reviveAt = new(5, timeline, PriorityRank.None);
        timeline.AddEvent(reviveAt);
        Card<int, string> longAfter = new(40, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        knockOutAt.Occurring += reached.OnTrigger;
        reviveAt.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;
        while (!ReferenceEquals(reached.MostRecentEvent, knockOutAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        health.ApplyInstantDamage(new Rational<long>(10000));
        while (!ReferenceEquals(reached.MostRecentEvent, reviveAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsKOd);
        Assert.True(combatant.HasStatus(StatusEffects.Silence));
        Assert.True(combatant.HasStatus(StatusEffects.Intangible));

        Assert.True(combatant.Revive(60));

        Assert.Empty(combatant.StatusEffects);
        Assert.True(silence.IsCancelled);
        Assert.True(intangible.IsCancelled);
        Assert.False(health.IsIntangible);
        Assert.False(health.IsPaused);
        Assert.Equal(60, combatant.Health);

        //It isn't immune to what was cleared: on from 6 to 26
        StatusCard<int> silenceAgain = new(combatant, StatusEffects.Silence, null, 1, 20, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(silenceAgain);
        Card<int, string> duringSecond = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(duringSecond);
        duringSecond.Occurring += reached.OnTrigger;
        while (!ReferenceEquals(reached.MostRecentEvent, duringSecond))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.HasStatus(StatusEffects.Silence));
        Assert.False(silenceAgain.IsCancelled);
        Assert.Same(silenceAgain, combatant.StatusEventFor(StatusEffects.Silence));

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Empty(combatant.StatusEffects);
        Assert.True(silence.IsFinished);
    }
}
