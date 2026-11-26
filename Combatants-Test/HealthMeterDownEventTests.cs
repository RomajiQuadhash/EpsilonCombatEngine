using Combatants;
using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Combatants_Test;

/// <summary>
/// Tests for the events a HealthMeter makes for going down and coming back: the knock out event (for a KO or a death) and the revive card.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class HealthMeterDownEventTests
{
    [Fact(DisplayName = "A knock out event occurs when damage over time takes the health to its threshold, and nothing over time changes the health after it")]
    public void KnockOutEventStopsTheHealthWhenDamageReachesIt()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 30, 0, 1000, defense, new ModifierGroupCounter());
        //10 a tick from the damage and 2 a tick back from the healing, so 8 a tick: 22, 14, 6, and then 0 at 20
        health.AddDamage(new ModInstance<int, int>(100, 5));
        health.Add(new ModInstance<int, int>(2, 5));
        DiscreteReachThresholdEvent<int, int> knockOut = health.CreateKnockOutEvent(0, PriorityRank.PlayerKO);
        timeline.AddEvent(knockOut);
        MostRecentEventHolder<int> knockedOut = new();
        knockOut.Occurring += knockedOut.OnTrigger;
        Card<int, string> beforeKnockOut = new(19, timeline, PriorityRank.None);
        timeline.AddEvent(beforeKnockOut);
        Card<int, string> longAfter = new(60, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        beforeKnockOut.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, beforeKnockOut))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(6, health.CurrentValue);
        Assert.False(health.IsDown);
        Assert.Null(knockedOut.MostRecentEvent);

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Same(knockOut, knockedOut.MostRecentEvent);
        Assert.True(health.IsDown);
        Assert.True(health.IsPaused);
        Assert.Equal(0, health.CurrentValue);
    }
    [Fact(DisplayName = "A knock out event occurs when instant damage takes the health to its threshold, and the healing tick in progress never lands")]
    public void KnockOutEventOccursOnInstantDamage()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 30, 0, 1000, defense, new ModifierGroupCounter());
        health.Add(new ModInstance<int, int>(10, 5));
        DiscreteReachThresholdEvent<int, int> knockOut = health.CreateKnockOutEvent(0, PriorityRank.PlayerKO);
        timeline.AddEvent(knockOut);
        Card<int, string> hitAt = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(hitAt);
        Card<int, string> longAfter = new(20, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        hitAt.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, hitAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(30, health.ApplyInstantDamage(new Rational<long>(1000)));
        Assert.False(health.IsDown);

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(health.IsDown);
        Assert.Equal(0, health.CurrentValue);
    }
    [Fact(DisplayName = "A knock out event with a threshold below 0 is for a death while bleeding: the health passes 0 and goes down at the threshold")]
    public void KnockOutEventAtTheMinimumIsADeath()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 20, -20, 1000, defense, new ModifierGroupCounter());
        health.AddDamage(new ModInstance<int, int>(100, 5));
        DiscreteReachThresholdEvent<int, int> death = health.CreateKnockOutEvent(-20, PriorityRank.PlayerDeath);
        timeline.AddEvent(death);
        Card<int, string> pastZero = new(12, timeline, PriorityRank.None);
        timeline.AddEvent(pastZero);
        Card<int, string> longAfter = new(60, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        pastZero.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, pastZero))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(0, health.CurrentValue);
        Assert.False(health.IsDown);

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(health.IsDown);
        Assert.Equal(-20, health.CurrentValue);
    }
    [Fact(DisplayName = "A revive card brings the health back at the value on its face when it occurs, with nothing changing it")]
    public void ReviveCardRevivesWhenItOccurs()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 0, 0, 1000, defense, new ModifierGroupCounter());
        health.AddDamage(new ModInstance<int, int>(100, 5));
        health.KnockOut();
        Card<int, int> revive = health.CreateReviveCard(5, 50, PriorityRank.PlayerRes);
        timeline.AddEvent(revive);
        Card<int, string> beforeRevive = new(4, timeline, PriorityRank.None);
        timeline.AddEvent(beforeRevive);
        Card<int, string> longAfter = new(30, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        beforeRevive.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;

        Assert.Equal(50, revive.Face);
        while (!ReferenceEquals(reached.MostRecentEvent, beforeRevive))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(health.IsDown);
        Assert.Equal(0, health.CurrentValue);

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.False(health.IsDown);
        Assert.False(health.IsPaused);
        Assert.Equal(50, health.CurrentValue);
        Assert.Empty(health.DamageModifiers);
    }
    [Fact(DisplayName = "A revive card can't be made for a health outside the minimum and maximum")]
    public void ReviveCardOutsideTheRangeIsRefused()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 0, 0, 1000, defense, new ModifierGroupCounter());

        Assert.Throws<ArgumentOutOfRangeException>(() => health.CreateReviveCard(5, 1001, PriorityRank.PlayerRes));
        Assert.Throws<ArgumentOutOfRangeException>(() => health.CreateReviveCard(5, -1, PriorityRank.PlayerRes));
    }
    [Fact(DisplayName = "A health meter can be knocked out again after a revive, with a new knock out event")]
    public void KnockOutAfterAReviveNeedsANewEvent()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 10, 0, 1000, defense, new ModifierGroupCounter());
        timeline.AddEvent(health.CreateKnockOutEvent(0, PriorityRank.PlayerKO));
        Card<int, int> revive = health.CreateReviveCard(5, 10, PriorityRank.PlayerRes);
        timeline.AddEvent(revive);
        Card<int, string> afterRevive = new(6, timeline, PriorityRank.None);
        timeline.AddEvent(afterRevive);
        Card<int, string> longAfter = new(20, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        afterRevive.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;
        health.ApplyInstantDamage(new Rational<long>(1000));

        while (!ReferenceEquals(reached.MostRecentEvent, afterRevive))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.False(health.IsDown);
        Assert.Equal(10, health.CurrentValue);

        timeline.AddEvent(health.CreateKnockOutEvent(0, PriorityRank.PlayerKO));
        health.ApplyInstantDamage(new Rational<long>(1000));
        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(health.IsDown);
        Assert.Equal(0, health.CurrentValue);
    }
    [Fact(DisplayName = "A cloned knock out event knocks out the cloned health meter and leaves the original up, and a cloned revive card revives only the clone")]
    public void ClonedEventsActOnTheClone()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 10, 0, 1000, defense, new ModifierGroupCounter());
        health.AddDamage(new ModInstance<int, int>(100, 5));
        timeline.AddEvent(health.CreateKnockOutEvent(0, PriorityRank.PlayerKO));
        timeline.AddEvent(health.CreateReviveCard(8, 70, PriorityRank.PlayerRes));
        Card<int, string> whileDown = new(6, timeline, PriorityRank.None);
        timeline.AddEvent(whileDown);
        MostRecentEventHolder<int> reachedWhileDown = new();
        whileDown.Occurring += reachedWhileDown.OnTrigger;
        Card<int, string> afterRevive = new(9, timeline, PriorityRank.None);
        timeline.AddEvent(afterRevive);
        MostRecentEventHolder<int> reachedAfterRevive = new();
        afterRevive.Occurring += reachedAfterRevive.OnTrigger;

        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone(health);
        context.GetOrClone(reachedWhileDown);
        context.GetOrClone(reachedAfterRevive);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(timeline, out Timeline<int> timelineClone));
        Assert.True(context.TryGet(health, out HealthMeter<int> healthClone));
        Assert.True(context.TryGet(reachedWhileDown, out MostRecentEventHolder<int> reachedWhileDownClone));
        Assert.True(context.TryGet(reachedAfterRevive, out MostRecentEventHolder<int> reachedAfterReviveClone));

        while (reachedWhileDownClone.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timelineClone.TakeSteps()) { }
            timelineClone.SetOpen();
        }
        //The tick at 5 took the clone to 0
        Assert.True(healthClone.IsDown);
        Assert.Equal(0, healthClone.CurrentValue);
        Assert.False(health.IsDown);
        Assert.Equal(10, health.CurrentValue);

        while (reachedAfterReviveClone.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timelineClone.TakeSteps()) { }
            timelineClone.SetOpen();
        }
        Assert.False(healthClone.IsDown);
        Assert.Equal(70, healthClone.CurrentValue);
        Assert.Equal(10, health.CurrentValue);
        Assert.Single(health.DamageModifiers);
    }
}
