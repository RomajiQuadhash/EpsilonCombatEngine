using Combatants;
using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Combatants_Test;

/// <summary>
/// Tests for a HealthMeter with a Guard: the Guard adding to the Defense, sources of damage chipping it as they start,
/// Stagger and Break, the Guard healing back when a source ends, and a Break ending.
/// The amounts chipped and the time a heal takes are worked out from BattleTuning, so the tests don't depend on what those are set to
/// (as long as a rate of 200 is enough to take a Guard of 10 to 0 and a rate of 4 isn't, and a heal takes at least 1).
/// </summary>
/// <remarks>Claude written, Romaji </remarks>
public class HealthMeterGuardTests
{
    #region The Guard and the Defense
    [Fact(DisplayName = "A health meter without a Guard has a Guard stat of 0, is never Broken or Staggered, and has a break reward of 1")]
    public void NoGuardMeansNoGuardBonus()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());

        health.AddDamage(new ModInstance<int, int>(1000, 5));
        health.AddDamage(new ModInstance<int, int>(1000, 5));

        Assert.Null(health.Guard);
        Assert.Equal(Rational<long>.Zero, health.GuardStat);
        Assert.False(health.IsGuardBroken);
        Assert.False(health.IsGuardStaggered);
        Assert.Equal(Rational<long>.One, health.BreakReward);
        Assert.Equal(health.BaseDefense, health.EffectiveDefense);
    }
    [Fact(DisplayName = "A Guard meter whose minimum isn't 0 is refused")]
    public void GuardMinimumMustBeZero()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, -5, 10);

        Assert.Throws<ArgumentException>(() => new HealthMeter<int>(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter(), guard, guardStrength));
    }
    [Fact(DisplayName = "A Guard meter whose maximum isn't the GuardStrength's base stat is refused, since both are the Guard stat")]
    public void GuardMaximumMustMatchGuardStrength()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 12, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);

        Assert.Throws<ArgumentException>(() => new HealthMeter<int>(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter(), guard, guardStrength));
    }
    [Fact(DisplayName = "BuildGuard makes a full Guard meter and a GuardStrength stat from one Guard stat, which a health meter accepts")]
    public void BuildGuardMakesAMatchingPair()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));

        HealthMeter<int>.BuildGuard(timeline, 25, new Rational<long>(-4), new Rational<long>(3), out LinearContinuousMeter<int, double> guard, out StatModificationMeter<int> guardStrength);
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter(), guard, guardStrength);

        Assert.Equal(25, guard.CurrentValue);
        Assert.Equal(0, guard.MinValue);
        Assert.Equal(25, guard.MaxValue);
        Assert.Equal(25, guardStrength.BaseStat);
        Assert.Equal(new Rational<long>(-4), guardStrength.MinValue);
        Assert.Equal(new Rational<long>(3), guardStrength.MaxValue);
        Assert.Same(timeline, guard.OwningTimeline);
        Assert.Same(timeline, guardStrength.OwningTimeline);
        Assert.Equal(new Rational<long>(25), health.GuardStat);
        Assert.Equal(new Rational<long>(35), health.EffectiveDefense);
        Assert.Throws<ArgumentException>(() => HealthMeter<int>.BuildGuard(timeline, 0, new Rational<long>(-4), new Rational<long>(3), out _, out _));
    }
    [Fact(DisplayName = "While the Guard is up, damage over time is divided by the Defense plus the Guard, and a Defense buff counts on both")]
    public void GuardAddsToDefense()
    {
        double scale = (double)BattleTuning.GuardScale.Numerator / BattleTuning.GuardScale.Denominator;
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter(), guard, guardStrength);

        //A rate of 4 before Defense
        health.AddDamage(new ModInstance<int, int>(20, 5));

        Assert.Equal(new Rational<long>(10), health.GuardStat);
        Assert.Equal(new Rational<long>(2), health.BreakReward);
        Assert.Equal(new Rational<long>(20), health.EffectiveDefense);
        Assert.Equal(new Rational<long>(10), health.BaseDefense);
        Assert.Equal(new Rational<long>(-1, 5), health.TrueRate);
        Assert.Equal(10 - 4 * scale, guard.CurrentValue, 9);
        Assert.False(health.IsGuardStaggered);
        Assert.False(health.IsGuardBroken);

        defense.AddInstantChange(new Rational<long>(1));

        Assert.Equal(new Rational<long>(40), health.EffectiveDefense);
        Assert.Equal(new Rational<long>(20), health.BaseDefense);
        Assert.Equal(new Rational<long>(2), health.BreakReward);
        Assert.Equal(new Rational<long>(-1, 10), health.TrueRate);
    }
    [Fact(DisplayName = "Instant damage ignores the Guard and doesn't chip it")]
    public void InstantDamageIgnoresGuard()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter(), guard, guardStrength);

        Assert.Equal(10, health.ApplyInstantDamage(new Rational<long>(100)));

        Assert.Equal(10, guard.CurrentValue);
    }
    #endregion
    #region Chip, Stagger and Break
    [Fact(DisplayName = "A higher GuardStrength makes a source of damage chip less off the Guard, without changing what the Guard adds to the Defense")]
    public void GuardStrengthDividesTheChip()
    {
        double scale = (double)BattleTuning.GuardScale.Numerator / BattleTuning.GuardScale.Denominator;
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter(), guard, guardStrength);
        guardStrength.AddInstantChange(new Rational<long>(1));

        health.AddDamage(new ModInstance<int, int>(20, 5));

        Assert.Equal(10 - 2 * scale, guard.CurrentValue, 9);
        Assert.Equal(new Rational<long>(20), health.EffectiveDefense);
    }
    [Fact(DisplayName = "A source of damage that chips more than the Guard has left takes it to 0, which is Staggered, and is still divided by the Defense plus the Guard")]
    public void ChipStopsAtZeroAndStaggers()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter(), guard, guardStrength);

        //A rate of 200 before Defense
        health.AddDamage(new ModInstance<int, int>(1000, 5));

        Assert.Equal(0, guard.CurrentValue);
        Assert.True(health.IsGuardStaggered);
        Assert.False(health.IsGuardBroken);
        Assert.Equal(new Rational<long>(20), health.EffectiveDefense);
        Assert.Equal(new Rational<long>(-10), health.TrueRate);
    }
    [Fact(DisplayName = "A source of damage that starts while the Guard is Staggered Breaks it, and then all the damage is divided by the Defense alone")]
    public void DamageOnAStaggeredGuardBreaksIt()
    {
        double scale = (double)BattleTuning.GuardScale.Numerator / BattleTuning.GuardScale.Denominator;
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter(), guard, guardStrength);
        ModifierKey first = health.AddDamage(new ModInstance<int, int>(1000, 5));

        health.AddDamage(new ModInstance<int, int>(1000, 5));

        Assert.True(health.IsGuardBroken);
        Assert.False(health.IsGuardStaggered);
        Assert.Equal(-200 * scale, guard.CurrentValue, 9);
        Assert.Equal(new Rational<long>(10), health.EffectiveDefense);
        //Both sources, the first one included, are now 200 over a Defense of 10
        Assert.Equal(new Rational<long>(-40), health.TrueRate);
        Assert.Equal(new ModInstance<int, int>(1000, 5), health.DamageModifiers[first]);
        //Twice what the two would deal with the Guard up, which is the break reward
        Assert.Equal(new Rational<long>(-20) * health.BreakReward, health.TrueRate);
    }
    [Fact(DisplayName = "Replacing a source of damage with a stronger one chips the difference off the Guard at once, and the Guard heals all of it back when the source ends")]
    public void ReplaceWithStrongerChipsTheDifference()
    {
        double scale = (double)BattleTuning.GuardScale.Numerator / BattleTuning.GuardScale.Denominator;
        int healTime = (int)(BattleTuning.GuardHealDuration.Numerator / BattleTuning.GuardHealDuration.Denominator);
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 100000, 0, 100000, defense, new ModifierGroupCounter(), guard, guardStrength);
        ModifierKey key = health.AddDamage(new ModInstance<int, int>(20, 5));
        Card<int, string> afterHeal = new(healTime + 1, timeline, PriorityRank.None);
        timeline.AddEvent(afterHeal);
        MostRecentEventHolder<int> reached = new();
        afterHeal.Occurring += reached.OnTrigger;

        //From a rate of 4 to a rate of 8
        Assert.True(health.ReplaceDamage(key, new ModInstance<int, int>(40, 5)));

        Assert.Equal(10 - 8 * scale, guard.CurrentValue, 9);
        Assert.False(health.IsGuardStaggered);

        Assert.True(health.RemoveDamage(key));
        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(10, guard.CurrentValue, 9);
    }
    [Fact(DisplayName = "Replacing a source of damage with a weaker one gives the difference back to the Guard at once, and the Guard heals back only the rest when the source ends")]
    public void ReplaceWithWeakerGivesTheDifferenceBack()
    {
        double scale = (double)BattleTuning.GuardScale.Numerator / BattleTuning.GuardScale.Denominator;
        int healTime = (int)(BattleTuning.GuardHealDuration.Numerator / BattleTuning.GuardHealDuration.Denominator);
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 100000, 0, 100000, defense, new ModifierGroupCounter(), guard, guardStrength);
        ModifierKey key = health.AddDamage(new ModInstance<int, int>(40, 5));
        Card<int, string> afterHeal = new(healTime + 1, timeline, PriorityRank.None);
        timeline.AddEvent(afterHeal);
        MostRecentEventHolder<int> reached = new();
        afterHeal.Occurring += reached.OnTrigger;
        Assert.Equal(10 - 8 * scale, guard.CurrentValue, 9);

        //From a rate of 8 to a rate of 4
        Assert.True(health.ReplaceDamage(key, new ModInstance<int, int>(20, 5)));

        Assert.Equal(10 - 4 * scale, guard.CurrentValue, 9);

        Assert.True(health.RemoveDamage(key));
        Assert.Equal(10 - 4 * scale, guard.CurrentValue, 9);
        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //Not more than full, which it would be if the heal still gave back the whole first chip
        Assert.Equal(10, guard.CurrentValue, 9);
        Assert.Equal(0, guard.Derivative);
    }
    [Fact(DisplayName = "Replacing a source of damage with one strong enough to take the Guard to 0 Staggers it but doesn't Break it, and only what was taken is healed back")]
    public void ReplaceCanStaggerButNotBreak()
    {
        int healTime = (int)(BattleTuning.GuardHealDuration.Numerator / BattleTuning.GuardHealDuration.Denominator);
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 100000, 0, 100000, defense, new ModifierGroupCounter(), guard, guardStrength);
        ModifierKey key = health.AddDamage(new ModInstance<int, int>(20, 5));
        Card<int, string> afterHeal = new(healTime + 2, timeline, PriorityRank.None);
        timeline.AddEvent(afterHeal);
        MostRecentEventHolder<int> reached = new();
        afterHeal.Occurring += reached.OnTrigger;

        Assert.True(health.ReplaceDamage(key, new ModInstance<int, int>(1000, 5)));

        Assert.Equal(0, guard.CurrentValue);
        Assert.True(health.IsGuardStaggered);
        Assert.False(health.IsGuardBroken);

        //Replacing it again while Staggered still doesn't Break it
        Assert.True(health.ReplaceDamage(key, new ModInstance<int, int>(2000, 5)));
        Assert.False(health.IsGuardBroken);
        Assert.Equal(0, guard.CurrentValue);

        Assert.True(health.RemoveDamage(key));
        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(10, guard.CurrentValue, 9);
        Assert.False(health.IsGuardStaggered);
    }
    [Fact(DisplayName = "Replacing a source of damage while the Guard is Broken moves the Guard by the whole difference, both ways")]
    public void ReplaceWhileBrokenMovesTheWholeDifference()
    {
        double scale = (double)BattleTuning.GuardScale.Numerator / BattleTuning.GuardScale.Denominator;
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 100000, 0, 100000, defense, new ModifierGroupCounter(), guard, guardStrength);
        health.AddDamage(new ModInstance<int, int>(1000, 5));
        ModifierKey broke = health.AddDamage(new ModInstance<int, int>(1000, 5));
        Assert.Equal(-200 * scale, guard.CurrentValue, 9);

        //From a rate of 200 to a rate of 100
        Assert.True(health.ReplaceDamage(broke, new ModInstance<int, int>(500, 5)));
        Assert.Equal(-100 * scale, guard.CurrentValue, 9);
        Assert.True(health.IsGuardBroken);

        //And up to a rate of 300
        Assert.True(health.ReplaceDamage(broke, new ModInstance<int, int>(1500, 5)));
        Assert.Equal(-300 * scale, guard.CurrentValue, 9);
        Assert.True(health.IsGuardBroken);
    }
    #endregion
    #region Healing back and a Break ending
    [Fact(DisplayName = "When a source of damage ends, the Guard heals back what it chipped off over the heal time, not all at once")]
    public void GuardHealsBackWhenDamageEnds()
    {
        double scale = (double)BattleTuning.GuardScale.Numerator / BattleTuning.GuardScale.Denominator;
        int healTime = (int)(BattleTuning.GuardHealDuration.Numerator / BattleTuning.GuardHealDuration.Denominator);
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter(), guard, guardStrength);
        ModifierKey key = health.AddDamage(new ModInstance<int, int>(20, 5));
        Card<int, string> afterHeal = new(healTime + 1, timeline, PriorityRank.None);
        timeline.AddEvent(afterHeal);
        MostRecentEventHolder<int> reached = new();
        afterHeal.Occurring += reached.OnTrigger;

        Assert.True(health.RemoveDamage(key));

        Assert.Equal(10 - 4 * scale, guard.CurrentValue, 9);

        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(10, guard.CurrentValue, 9);
        Assert.Equal(0, guard.Derivative);
    }
    [Fact(DisplayName = "A Broken Guard that only heals back to 0 stays Broken, and it is restored once the Guard heals above 0")]
    public void BreakEndsWhenTheGuardHealsAboveZero()
    {
        double scale = (double)BattleTuning.GuardScale.Numerator / BattleTuning.GuardScale.Denominator;
        int healTime = (int)(BattleTuning.GuardHealDuration.Numerator / BattleTuning.GuardHealDuration.Denominator);
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 100000, 0, 100000, defense, new ModifierGroupCounter(), guard, guardStrength);
        //Chips a little, then one that Staggers, then one that Breaks
        health.AddDamage(new ModInstance<int, int>(20, 5));
        ModifierKey staggered = health.AddDamage(new ModInstance<int, int>(1000, 5));
        ModifierKey broke = health.AddDamage(new ModInstance<int, int>(1000, 5));
        Card<int, string> afterFirstHeal = new(healTime + 1, timeline, PriorityRank.None);
        timeline.AddEvent(afterFirstHeal);
        Card<int, string> afterSecondHeal = new(2 * healTime + 3, timeline, PriorityRank.None);
        timeline.AddEvent(afterSecondHeal);
        MostRecentEventHolder<int> reached = new();
        afterFirstHeal.Occurring += reached.OnTrigger;
        afterSecondHeal.Occurring += reached.OnTrigger;
        Assert.True(health.IsGuardBroken);

        //The one that broke it chipped the Guard from 0, so healing that back only gets to 0
        Assert.True(health.RemoveDamage(broke));
        while (!ReferenceEquals(reached.MostRecentEvent, afterFirstHeal))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(0, guard.CurrentValue, 9);
        Assert.True(health.IsGuardBroken);
        Assert.Equal(new Rational<long>(10), health.EffectiveDefense);

        //The one that Staggered it took what was left of the Guard, so healing that back goes above 0
        Assert.True(health.RemoveDamage(staggered));
        while (!ReferenceEquals(reached.MostRecentEvent, afterSecondHeal))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.False(health.IsGuardBroken);
        Assert.False(health.IsGuardStaggered);
        Assert.Equal(0, guard.MinValue);
        Assert.Equal(10 - 4 * scale, guard.CurrentValue, 9);
        //The first source is still running, and is divided by the Defense plus the Guard again
        Assert.Equal(new Rational<long>(20), health.EffectiveDefense);
        Assert.Equal(new Rational<long>(-1, 5), health.TrueRate);
        Assert.Equal(0, health.MidAdvanceRateChanges);
    }
    [Fact(DisplayName = "Reviving puts a Broken Guard back to full at once, with nothing owed to it, and it stays that way")]
    public void ReviveResetsTheGuard()
    {
        double scale = (double)BattleTuning.GuardScale.Numerator / BattleTuning.GuardScale.Denominator;
        int healTime = (int)(BattleTuning.GuardHealDuration.Numerator / BattleTuning.GuardHealDuration.Denominator);
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 100000, 0, 100000, defense, new ModifierGroupCounter(), guard, guardStrength);
        ModifierKey staggered = health.AddDamage(new ModInstance<int, int>(1000, 5));
        ModifierKey broke = health.AddDamage(new ModInstance<int, int>(1000, 5));
        Card<int, string> longAfter = new(2 * healTime + 3, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        longAfter.Occurring += reached.OnTrigger;
        Assert.True(health.IsGuardBroken);
        health.KnockOut();

        health.Revive(500);

        Assert.Equal(10, guard.CurrentValue);
        Assert.Equal(0, guard.MinValue);
        Assert.False(health.IsGuardBroken);
        Assert.False(health.IsGuardStaggered);
        Assert.Equal(new Rational<long>(20), health.EffectiveDefense);
        //The damage from before is gone, so ending it does nothing for the Guard either
        Assert.False(health.RemoveDamage(staggered));
        Assert.False(health.RemoveDamage(broke));

        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(10, guard.CurrentValue);
        Assert.Equal(0, guard.Derivative);
        Assert.False(health.IsGuardBroken);

        //And it is chipped like a fresh Guard afterwards
        health.AddDamage(new ModInstance<int, int>(20, 5));
        Assert.Equal(10 - 4 * scale, guard.CurrentValue, 9);
        Assert.False(health.IsGuardBroken);
    }
    [Fact(DisplayName = "Reviving while the Guard is healing back wipes that heal: the Guard is full and not healing at once, and nothing from before touches it later")]
    public void ReviveWipesAHealInProgress()
    {
        double scale = (double)BattleTuning.GuardScale.Numerator / BattleTuning.GuardScale.Denominator;
        int healTime = (int)(BattleTuning.GuardHealDuration.Numerator / BattleTuning.GuardHealDuration.Denominator);
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 100000, 0, 100000, defense, new ModifierGroupCounter(), guard, guardStrength);
        ModifierKey key = health.AddDamage(new ModInstance<int, int>(20, 5));
        Card<int, string> duringHeal = new(1, timeline, PriorityRank.None);
        timeline.AddEvent(duringHeal);
        Card<int, string> afterHeal = new(healTime + 2, timeline, PriorityRank.None);
        timeline.AddEvent(afterHeal);
        MostRecentEventHolder<int> reached = new();
        duringHeal.Occurring += reached.OnTrigger;
        afterHeal.Occurring += reached.OnTrigger;
        Assert.True(health.RemoveDamage(key));
        while (!ReferenceEquals(reached.MostRecentEvent, duringHeal))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(4 * scale / healTime, guard.Derivative, 9);

        health.Revive(500);

        Assert.Equal(10, guard.CurrentValue);
        Assert.Equal(0, guard.Derivative);

        //When the wiped heal's time is up, it doesn't take its rate back off a Guard that no longer has it
        while (!ReferenceEquals(reached.MostRecentEvent, afterHeal))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(10, guard.CurrentValue);
        Assert.Equal(0, guard.Derivative);
    }
    #endregion
    #region The Guard's state on the timeline
    [Fact(DisplayName = "Each change of the Guard's state occurs on the timeline as a card naming the change and the health meter, in the order the changes happened")]
    public void GuardStateChangesOccurOnTheTimeline()
    {
        int healTime = (int)(BattleTuning.GuardHealDuration.Numerator / BattleTuning.GuardHealDuration.Denominator);
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 100000, 0, 100000, defense, new ModifierGroupCounter(), guard, guardStrength);
        Card<int, string> afterStagger = new(1, timeline, PriorityRank.None);
        timeline.AddEvent(afterStagger);
        Card<int, string> afterBreak = new(2, timeline, PriorityRank.None);
        timeline.AddEvent(afterBreak);
        Card<int, string> afterHeals = new(healTime + 5, timeline, PriorityRank.None);
        timeline.AddEvent(afterHeals);
        MostRecentEventHolder<int> reached = new();
        afterStagger.Occurring += reached.OnTrigger;
        afterBreak.Occurring += reached.OnTrigger;
        afterHeals.Occurring += reached.OnTrigger;
        List<GuardTransition> shown = [];

        ModifierKey staggered = health.AddDamage(new ModInstance<int, int>(1000, 5));
        while (!ReferenceEquals(reached.MostRecentEvent, afterStagger))
        {
            foreach (StepReport<int> report in timeline.TakeSteps())
            {
                if (report.OccurredEvent is GuardStateCard<int> card)
                {
                    Assert.Same(health, card.Health);
                    shown.Add(card.Face);
                }
            }
            timeline.SetOpen();
        }
        Assert.Equal([GuardTransition.BecameStaggered], shown);
        Assert.Equal(GuardState.Staggered, health.CurrentGuardState);

        //Going from Staggered to Broken is two changes, made at the same moment
        ModifierKey broke = health.AddDamage(new ModInstance<int, int>(1000, 5));
        while (!ReferenceEquals(reached.MostRecentEvent, afterBreak))
        {
            foreach (StepReport<int> report in timeline.TakeSteps())
            {
                if (report.OccurredEvent is GuardStateCard<int> card)
                {
                    Assert.Same(health, card.Health);
                    shown.Add(card.Face);
                }
            }
            timeline.SetOpen();
        }
        Assert.Equal([GuardTransition.BecameStaggered, GuardTransition.LeftStaggered, GuardTransition.BecameBroken], shown);
        Assert.Equal(GuardState.Broken, health.CurrentGuardState);

        //Both end, so the Guard heals from below 0 to above it
        Assert.True(health.RemoveDamage(broke));
        Assert.True(health.RemoveDamage(staggered));
        while (!ReferenceEquals(reached.MostRecentEvent, afterHeals))
        {
            foreach (StepReport<int> report in timeline.TakeSteps())
            {
                if (report.OccurredEvent is GuardStateCard<int> card)
                {
                    Assert.Same(health, card.Health);
                    shown.Add(card.Face);
                }
            }
            timeline.SetOpen();
        }
        Assert.Equal([GuardTransition.BecameStaggered, GuardTransition.LeftStaggered, GuardTransition.BecameBroken, GuardTransition.LeftBroken], shown);
        Assert.Equal(GuardState.Up, health.CurrentGuardState);
    }
    [Fact(DisplayName = "A Staggered Guard that heals back above 0 without being Broken occurs on the timeline as leaving Staggered")]
    public void LeavingStaggeredByHealingOccursOnTheTimeline()
    {
        int healTime = (int)(BattleTuning.GuardHealDuration.Numerator / BattleTuning.GuardHealDuration.Denominator);
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 100000, 0, 100000, defense, new ModifierGroupCounter(), guard, guardStrength);
        Card<int, string> afterHeal = new(healTime + 3, timeline, PriorityRank.None);
        timeline.AddEvent(afterHeal);
        MostRecentEventHolder<int> reached = new();
        afterHeal.Occurring += reached.OnTrigger;
        List<GuardTransition> shown = [];
        ModifierKey staggered = health.AddDamage(new ModInstance<int, int>(1000, 5));

        Assert.True(health.RemoveDamage(staggered));
        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> report in timeline.TakeSteps())
            {
                if (report.OccurredEvent is GuardStateCard<int> card)
                {
                    shown.Add(card.Face);
                }
            }
            timeline.SetOpen();
        }
        Assert.Equal([GuardTransition.BecameStaggered, GuardTransition.LeftStaggered], shown);
        Assert.Equal(10, guard.CurrentValue, 9);
    }
    [Fact(DisplayName = "Reviving with a Broken Guard occurs on the timeline as leaving Broken")]
    public void ReviveShowsTheGuardLeavingBroken()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 100000, 0, 100000, defense, new ModifierGroupCounter(), guard, guardStrength);
        Card<int, string> end = new(1, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> reached = new();
        end.Occurring += reached.OnTrigger;
        List<GuardTransition> shown = [];
        health.AddDamage(new ModInstance<int, int>(1000, 5));
        health.AddDamage(new ModInstance<int, int>(1000, 5));

        health.Revive(500);

        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> report in timeline.TakeSteps())
            {
                if (report.OccurredEvent is GuardStateCard<int> card)
                {
                    shown.Add(card.Face);
                }
            }
            timeline.SetOpen();
        }
        Assert.Equal([GuardTransition.BecameStaggered, GuardTransition.LeftStaggered, GuardTransition.BecameBroken, GuardTransition.LeftBroken], shown);
    }
    [Fact(DisplayName = "A Guard state card that is cloned before it occurs names the cloned health meter when it occurs on the clone")]
    public void ClonedGuardStateCardNamesTheClone()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 100000, 0, 100000, defense, new ModifierGroupCounter(), guard, guardStrength);
        health.AddDamage(new ModInstance<int, int>(1000, 5));

        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone(health);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(timeline, out Timeline<int> timelineClone));
        Assert.True(context.TryGet(health, out HealthMeter<int> healthClone));

        List<GuardStateCard<int>> occurredOnClone = [];
        foreach (StepReport<int> report in timelineClone.TakeSteps())
        {
            if (report.OccurredEvent is GuardStateCard<int> card)
            {
                occurredOnClone.Add(card);
            }
        }
        GuardStateCard<int> shown = Assert.Single(occurredOnClone);
        Assert.Equal(GuardTransition.BecameStaggered, shown.Face);
        Assert.Same(healthClone, shown.Health);
    }
    #endregion
    #region Cloning
    [Fact(DisplayName = "A health meter cloned while its Guard is Broken has its own Broken Guard, and the clone's Guard being restored leaves the original's Broken")]
    public void CloneHasItsOwnGuard()
    {
        double scale = (double)BattleTuning.GuardScale.Numerator / BattleTuning.GuardScale.Denominator;
        int healTime = (int)(BattleTuning.GuardHealDuration.Numerator / BattleTuning.GuardHealDuration.Denominator);
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        StatModificationMeter<int> guardStrength = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        LinearContinuousMeter<int, double> guard = new(timeline, 10, 0, 10);
        HealthMeter<int> health = new(timeline, 100000, 0, 100000, defense, new ModifierGroupCounter(), guard, guardStrength);
        health.AddDamage(new ModInstance<int, int>(20, 5));
        ModifierKey staggered = health.AddDamage(new ModInstance<int, int>(1000, 5));
        ModifierKey broke = health.AddDamage(new ModInstance<int, int>(1000, 5));
        Card<int, string> afterHeals = new(healTime + 3, timeline, PriorityRank.None);
        timeline.AddEvent(afterHeals);
        MostRecentEventHolder<int> reached = new();
        afterHeals.Occurring += reached.OnTrigger;

        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone(health);
        context.GetOrClone(reached);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(timeline, out Timeline<int> timelineClone));
        Assert.True(context.TryGet(health, out HealthMeter<int> healthClone));
        Assert.True(context.TryGet(guard, out LinearContinuousMeter<int, double> guardClone));
        Assert.True(context.TryGet(guardStrength, out StatModificationMeter<int> guardStrengthClone));
        Assert.True(context.TryGet(reached, out MostRecentEventHolder<int> reachedClone));

        Assert.Same(guardClone, healthClone.Guard);
        Assert.Same(guardStrengthClone, healthClone.GuardStrength);
        Assert.True(healthClone.IsGuardBroken);
        Assert.Equal(guard.CurrentValue, guardClone.CurrentValue);

        //Both end on the clone at once, so its Guard heals from below 0 to above it
        Assert.True(healthClone.RemoveDamage(broke));
        Assert.True(healthClone.RemoveDamage(staggered));
        while (reachedClone.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timelineClone.TakeSteps()) { }
            timelineClone.SetOpen();
        }
        Assert.False(healthClone.IsGuardBroken);
        Assert.Equal(10 - 4 * scale, guardClone.CurrentValue, 9);
        Assert.Equal(new Rational<long>(20), healthClone.EffectiveDefense);

        Assert.True(health.IsGuardBroken);
        Assert.Equal(-200 * scale, guard.CurrentValue, 9);
        Assert.Equal(3, health.DamageModifiers.Count);
    }
    #endregion
}
