using Combatants;
using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Combatants_Test;

/// <summary>
/// Tests for a Combatant that is bleeding: going below 0 health without being knocked out, being knocked out when the bleeding ends,
/// dying at the bleed minimum, bleeding while already knocked out, and what a combatant that can't be killed does instead.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class CombatantBleedTests
{
    [Fact(DisplayName = "The health at which a bleeding combatant dies is below 0 by the bleed limit of its maximum health, rounded up")]
    public void BleedMinimumRoundsUp()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        Combatant<int> even = new(new HealthMeter<int>(timeline, 100, 0, 100, defense, new ModifierGroupCounter()), speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        Combatant<int> uneven = new(new HealthMeter<int>(timeline, 95, 0, 95, defense, new ModifierGroupCounter()), speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        Rational<long> limit = BattleTuning.BleedLimit;

        Assert.Equal(-(int)((100 * limit.Numerator + limit.Denominator - 1) / limit.Denominator), even.BleedMinimum);
        Assert.Equal(-(int)((95 * limit.Numerator + limit.Denominator - 1) / limit.Denominator), uneven.BleedMinimum);
        Assert.True(uneven.BleedMinimum < 0);
    }
    [Fact(DisplayName = "A bleeding combatant can go below 0 health without being knocked out, and is knocked out when the bleeding ends with it at 0 or below")]
    public void BleedingHoldsBackTheKnockOut()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        //A maximum of 1000, so that the bleed minimum is far enough below 0 not to be reached here
        HealthMeter<int> health = new(timeline, 15, 0, 1000, defense, new ModifierGroupCounter());
        Combatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath) { CanBeKilled = true };
        //From 1 to 13 at 10 a tick: 5 at 6 and -5 at 11
        BleedEvent<int> bleed = new(combatant, null, new ModInstance<int, int>(100, 5), 1, 12, PriorityRank.PlayerBleedStart, PriorityRank.PlayerBleedEnd);
        timeline.AddEvent(bleed);
        Card<int, string> whileBleeding = new(12, timeline, PriorityRank.None);
        timeline.AddEvent(whileBleeding);
        Card<int, string> afterBleeding = new(30, timeline, PriorityRank.None);
        timeline.AddEvent(afterBleeding);
        MostRecentEventHolder<int> reached = new();
        whileBleeding.Occurring += reached.OnTrigger;
        afterBleeding.Occurring += reached.OnTrigger;
        Assert.False(combatant.IsBleeding);

        while (!ReferenceEquals(reached.MostRecentEvent, whileBleeding))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsBleeding);
        Assert.Equal(combatant.BleedMinimum, health.MinValue);
        Assert.Equal(-5, combatant.Health);
        Assert.True(combatant.IsActive);
        Assert.Null(combatant.PendingKnockOut);

        while (!ReferenceEquals(reached.MostRecentEvent, afterBleeding))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.False(combatant.IsBleeding);
        Assert.True(combatant.IsKOd);
        Assert.Equal(0, combatant.Health);
        Assert.Equal(0, health.MinValue);
        Assert.True(health.IsDown);
        //The tick that was in progress when the bleed ended never landed
        Assert.Equal(0, health.CurrentValue);
    }
    [Fact(DisplayName = "A combatant whose bleeding ends with health above 0 stays up, can't go below 0 any more, and can be knocked out as usual")]
    public void BleedingEndsAboveZero()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        Combatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath) { CanBeKilled = true };
        //From 1 to 7
        timeline.AddEvent(new BleedEvent<int>(combatant, null, new ModInstance<int, int>(100, 5), 1, 6, PriorityRank.PlayerBleedStart, PriorityRank.PlayerBleedEnd));
        Card<int, string> afterBleeding = new(20, timeline, PriorityRank.None);
        timeline.AddEvent(afterBleeding);
        Card<int, string> afterHit = new(21, timeline, PriorityRank.None);
        timeline.AddEvent(afterHit);
        MostRecentEventHolder<int> reached = new();
        afterBleeding.Occurring += reached.OnTrigger;
        afterHit.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, afterBleeding))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.False(combatant.IsBleeding);
        Assert.True(combatant.IsActive);
        Assert.Equal(0, health.MinValue);
        Assert.NotNull(combatant.PendingKnockOut);
        Assert.True(combatant.Health > 0);

        health.ApplyInstantDamage(new Rational<long>(10000));
        while (!ReferenceEquals(reached.MostRecentEvent, afterHit))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsKOd);
        Assert.Equal(0, health.CurrentValue);
    }
    [Fact(DisplayName = "A bleeding combatant whose health falls to the bleed minimum dies")]
    public void BleedingToTheMinimumKills()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 5, 0, 100, defense, new ModifierGroupCounter());
        Combatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath) { CanBeKilled = true };
        //Far more than it takes to get from 5 to the minimum
        timeline.AddEvent(new BleedEvent<int>(combatant, null, new ModInstance<int, int>(1000, 5), 1, 50, PriorityRank.PlayerBleedStart, PriorityRank.PlayerBleedEnd));
        Card<int, string> longAfter = new(60, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        longAfter.Occurring += reached.OnTrigger;

        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsDead);
        Assert.False(combatant.IsBleeding);
        Assert.Equal(0, combatant.Health);
        Assert.Empty(health.DamageModifiers);
        Assert.False(combatant.Revive(50));
    }
    [Fact(DisplayName = "For a combatant that can't be killed a bleed is only damage: it never counts as bleeding, can't go below 0, and is knocked out at 0 as usual")]
    public void CannotBeKilledMeansNoBleeding()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 20, 0, 100, defense, new ModifierGroupCounter());
        Combatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath);
        //10 a tick: 10 at 6 and 0 at 11
        timeline.AddEvent(new BleedEvent<int>(combatant, null, new ModInstance<int, int>(100, 5), 1, 50, PriorityRank.PlayerBleedStart, PriorityRank.PlayerBleedEnd));
        Card<int, string> during = new(8, timeline, PriorityRank.None);
        timeline.AddEvent(during);
        Card<int, string> afterZero = new(12, timeline, PriorityRank.None);
        timeline.AddEvent(afterZero);
        MostRecentEventHolder<int> reached = new();
        during.Occurring += reached.OnTrigger;
        afterZero.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, during))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.False(combatant.IsBleeding);
        Assert.Equal(0, health.MinValue);
        Assert.Equal(10, combatant.Health);
        Assert.NotNull(combatant.PendingKnockOut);

        while (!ReferenceEquals(reached.MostRecentEvent, afterZero))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsKOd);
        Assert.False(combatant.IsDead);
    }
    [Fact(DisplayName = "A combatant keeps bleeding until the last of its bleeds has ended")]
    public void BleedsUntilTheLastOneEnds()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        Combatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath) { CanBeKilled = true };
        //One from 1 to 6 and one from 2 to 22, each 1 a tick
        timeline.AddEvent(new BleedEvent<int>(combatant, null, new ModInstance<int, int>(10, 5), 1, 5, PriorityRank.PlayerBleedStart, PriorityRank.PlayerBleedEnd));
        timeline.AddEvent(new BleedEvent<int>(combatant, null, new ModInstance<int, int>(10, 5), 2, 20, PriorityRank.PlayerBleedStart, PriorityRank.PlayerBleedEnd));
        Card<int, string> afterFirstEnds = new(10, timeline, PriorityRank.None);
        timeline.AddEvent(afterFirstEnds);
        Card<int, string> afterBothEnd = new(30, timeline, PriorityRank.None);
        timeline.AddEvent(afterBothEnd);
        MostRecentEventHolder<int> reached = new();
        afterFirstEnds.Occurring += reached.OnTrigger;
        afterBothEnd.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, afterFirstEnds))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsBleeding);
        Assert.Equal(combatant.BleedMinimum, health.MinValue);

        while (!ReferenceEquals(reached.MostRecentEvent, afterBothEnd))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.False(combatant.IsBleeding);
        Assert.Equal(0, health.MinValue);
        Assert.True(combatant.IsActive);
    }
    [Fact(DisplayName = "A knocked out combatant that starts bleeding stays knocked out and shows 0 health, while its health falls from 0 and can be healed out of sight")]
    public void KnockedOutCombatantBleedsFromZero()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 20, 0, 1000, defense, new ModifierGroupCounter());
        Combatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath) { CanBeKilled = true };
        //A heal that is running when it is knocked out, which should be gone once the bleed starts
        health.Add(new ModInstance<int, int>(50, 5));
        //From 2 to 32, at 2 a tick: -2 at 7 and -4 at 12
        timeline.AddEvent(new BleedEvent<int>(combatant, null, new ModInstance<int, int>(20, 5), 2, 30, PriorityRank.PlayerBleedStart, PriorityRank.PlayerBleedEnd));
        Card<int, string> afterKnockOut = new(1, timeline, PriorityRank.None);
        timeline.AddEvent(afterKnockOut);
        Card<int, string> whileBleeding = new(13, timeline, PriorityRank.None);
        timeline.AddEvent(whileBleeding);
        Card<int, string> afterBleeding = new(40, timeline, PriorityRank.None);
        timeline.AddEvent(afterBleeding);
        MostRecentEventHolder<int> reached = new();
        afterKnockOut.Occurring += reached.OnTrigger;
        whileBleeding.Occurring += reached.OnTrigger;
        afterBleeding.Occurring += reached.OnTrigger;
        health.ApplyInstantDamage(new Rational<long>(10000));
        while (!ReferenceEquals(reached.MostRecentEvent, afterKnockOut))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsKOd);
        Assert.True(health.IsPaused);

        while (!ReferenceEquals(reached.MostRecentEvent, whileBleeding))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsKOd);
        Assert.True(combatant.IsBleeding);
        Assert.False(health.IsPaused);
        Assert.Empty(health.Modifiers);
        Assert.Equal(-4, health.CurrentValue);
        Assert.Equal(0, combatant.Health);

        //Healing goes onto its health, but the combatant still shows 0 and stays knocked out
        health.AddInstantChange(50);
        Assert.Equal(46, health.CurrentValue);
        Assert.Equal(0, combatant.Health);
        Assert.True(combatant.IsKOd);

        while (!ReferenceEquals(reached.MostRecentEvent, afterBleeding))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.False(combatant.IsBleeding);
        Assert.True(combatant.IsKOd);
        Assert.Equal(0, combatant.Health);
        Assert.Equal(0, health.MinValue);
        Assert.True(health.IsPaused);
    }
    [Fact(DisplayName = "Reviving a combatant that is knocked out and bleeding ends its bleeds, so it comes back not bleeding with nothing changing its health")]
    public void ReviveEndsBleeds()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 20, 0, 1000, defense, new ModifierGroupCounter());
        Combatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO, PriorityRank.PlayerDeath) { CanBeKilled = true };
        //From 2 to 32
        BleedEvent<int> bleed = new(combatant, null, new ModInstance<int, int>(20, 5), 2, 30, PriorityRank.PlayerBleedStart, PriorityRank.PlayerBleedEnd);
        timeline.AddEvent(bleed);
        Card<int, string> whileBleeding = new(13, timeline, PriorityRank.None);
        timeline.AddEvent(whileBleeding);
        Card<int, string> longAfter = new(40, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        whileBleeding.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;
        health.ApplyInstantDamage(new Rational<long>(10000));
        while (!ReferenceEquals(reached.MostRecentEvent, whileBleeding))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsKOd);
        Assert.True(combatant.IsBleeding);

        Assert.True(combatant.Revive(50));

        Assert.True(combatant.IsActive);
        Assert.False(combatant.IsBleeding);
        Assert.True(bleed.HasEndedEarly);
        Assert.Equal(0, health.MinValue);
        Assert.Equal(50, combatant.Health);
        Assert.Empty(health.DamageModifiers);
        Assert.NotNull(combatant.PendingKnockOut);

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.True(combatant.IsActive);
        Assert.False(combatant.IsBleeding);
        Assert.Equal(50, combatant.Health);
    }
}
