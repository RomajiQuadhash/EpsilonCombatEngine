using Combatants;
using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Combatants_Test;

/// <summary>
/// Tests for HealthMeter: damage over time divided by a Defense that can change while the damage is running,
/// healing that Defense has no say in, instant damage, pausing, and cloning.
/// These are also the first tests of a RateManagedMeter with a second group of modifiers, and of pausing one.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class HealthMeterTests
{
    #region Damage over time and Defense
    [Fact(DisplayName = "Damage over time is divided by the Defense, and the damage is kept as it was before Defense")]
    public void DamageIsDividedByDefense()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());

        ModifierKey key = health.AddDamage(new ModInstance<int, int>(100, 5));

        Assert.Equal(new Rational<long>(-2), health.TrueRate);
        Assert.Equal(-10, health.Derivative);
        Assert.Equal(5, health.DerivativeDenominator);
        Assert.Equal(new ModInstance<int, int>(100, 5), health.DamageModifiers[key]);
        Assert.Empty(health.Modifiers);
    }
    [Fact(DisplayName = "Twice the damage against twice the Defense is the same as neither")]
    public void EqualAttackAndDefenseBuffsCancel()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        defense.AddInstantChange(new Rational<long>(1));

        health.AddDamage(new ModInstance<int, int>(200, 5));

        Assert.Equal(new Rational<long>(-2), health.TrueRate);
        Assert.Equal(-10, health.Derivative);
        Assert.Equal(5, health.DerivativeDenominator);
    }
    [Fact(DisplayName = "A debuffed Defense never counts as less than the minimum Defense")]
    public void DefenseHasAFloor()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 1, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        //A Defense of 1 at a ratio of -5 is 1/6, which is under the minimum
        defense.AddInstantChange(new Rational<long>(-5));

        health.AddDamage(new ModInstance<int, int>(10, 1));

        Assert.Equal(HealthMeter<int>.MinDefense, health.EffectiveDefense);
        Assert.Equal(HealthMeter<int>.MinDefense, health.BaseDefense);
        Assert.Equal(new Rational<long>(-10) / HealthMeter<int>.MinDefense, health.TrueRate);
    }
    [Fact(DisplayName = "With two sources of damage, the stronger one sets how often the health ticks, and removing it hands that to the other")]
    public void StrongerDamageSetsThePace()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());

        ModifierKey stronger = health.AddDamage(new ModInstance<int, int>(60, 2));
        health.AddDamage(new ModInstance<int, int>(100, 5));

        //30 and 20 per time unit before Defense, so 5 after it
        Assert.Equal(new Rational<long>(-5), health.TrueRate);
        Assert.Equal(-10, health.Derivative);
        Assert.Equal(2, health.DerivativeDenominator);

        Assert.True(health.RemoveDamage(stronger));

        Assert.Equal(-10, health.Derivative);
        Assert.Equal(5, health.DerivativeDenominator);
    }
    [Fact(DisplayName = "Removing or replacing damage with a key the health doesn't have changes nothing and says so, including a key from Add")]
    public void UnknownDamageKeyChangesNothing()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 500, 0, 1000, defense, new ModifierGroupCounter());
        health.AddDamage(new ModInstance<int, int>(100, 5));
        health.AddDamage(new ModInstance<int, int>(100, 5));
        health.AddDamage(new ModInstance<int, int>(100, 5));
        //Add gives its keys separately from AddDamage, so the first key from Add is not the first key from AddDamage
        ModifierKey healKey = health.Add(new ModInstance<int, int>(1, 5));

        Assert.False(health.RemoveDamage(new ModifierKey(999)));
        Assert.False(health.ReplaceDamage(new ModifierKey(999), new ModInstance<int, int>(8, 2)));
        Assert.True(health.Remove(healKey));

        Assert.Equal(3, health.DamageModifiers.Count);
        Assert.Empty(health.Modifiers);
    }
    [Fact(DisplayName = "A key from Add never works on damage and a key from AddDamage never works on anything from Add, even when both are the first key given")]
    public void HealAndDamageKeysCannotBeMixedUp()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 500, 0, 1000, defense, new ModifierGroupCounter());

        ModifierKey healKey = health.Add(new ModInstance<int, int>(1, 5));
        ModifierKey damageKey = health.AddDamage(new ModInstance<int, int>(100, 5));

        Assert.NotEqual(healKey, damageKey);
        Assert.False(health.RemoveDamage(healKey));
        Assert.False(health.ReplaceDamage(healKey, new ModInstance<int, int>(8, 2)));
        Assert.False(health.Remove(damageKey));
        Assert.False(health.Replace(damageKey, new ModInstance<int, int>(8, 2)));
        Assert.Single(health.Modifiers);
        Assert.Single(health.DamageModifiers);

        Assert.True(health.Remove(healKey));
        Assert.True(health.RemoveDamage(damageKey));
    }
    #endregion
    #region Healing next to damage
    [Fact(DisplayName = "Healing through Add is not divided by the Defense, and goes together with the damage that is")]
    public void HealingIsNotDivided()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 500, 0, 1000, defense, new ModifierGroupCounter());

        health.Add(new ModInstance<int, int>(6, 3));
        health.AddDamage(new ModInstance<int, int>(50, 5));

        //Healing of 2 per time unit as it is, and damage of 10 per time unit that is 1 after Defense
        Assert.Equal(Rational<long>.One, health.TrueRate);
        Assert.Single(health.Modifiers);
        Assert.Single(health.DamageModifiers);
        //The healing is the stronger of the two once the damage is divided, so the health ticks when the healing does
        Assert.Equal(3, health.DerivativeDenominator);
        Assert.Equal(3, health.Derivative);
    }
    [Fact(DisplayName = "A Defense change that makes the damage stronger than the healing hands how often the health ticks to the damage")]
    public void DefenseChangeHandsThePaceOver()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 500, 0, 1000, defense, new ModifierGroupCounter());
        health.Add(new ModInstance<int, int>(6, 3));
        health.AddDamage(new ModInstance<int, int>(20, 2));
        //Damage of 10 per time unit is 1 after a Defense of 10, under the healing's 2
        Assert.Equal(3, health.DerivativeDenominator);
        Assert.Equal(3, health.Derivative);

        //A ratio of -4 makes the Defense 2, so the damage is 5 per time unit
        defense.AddInstantChange(new Rational<long>(-4));

        Assert.Equal(new Rational<long>(-3), health.TrueRate);
        Assert.Equal(2, health.DerivativeDenominator);
        Assert.Equal(-6, health.Derivative);
    }
    #endregion
    #region Defense changing while damage is running
    [Fact(DisplayName = "A Defense buff partway through a tick keeps the tick's place, lands it with the new amount, and touches no damage")]
    public void DefenseBuffMidTickKeepsTheTick()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        ModifierKey key = health.AddDamage(new ModInstance<int, int>(200, 5));
        Card<int, string> partway = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(partway);
        Card<int, string> end = new(5, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> reached = new();
        partway.Occurring += reached.OnTrigger;
        end.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, partway))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(-20, health.Derivative);
        defense.AddInstantChange(new Rational<long>(1));

        Assert.Equal(1000, health.CurrentValue);
        Assert.Equal(3, health.TimeSinceLastIncrement);
        Assert.Equal(-10, health.Derivative);
        Assert.Equal(new ModInstance<int, int>(200, 5), health.DamageModifiers[key]);

        while (!ReferenceEquals(reached.MostRecentEvent, end))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(990, health.CurrentValue);
        Assert.Equal(0, health.BlendPerTick);
        Assert.Equal(0, health.MidAdvanceRateChanges);
    }
    [Fact(DisplayName = "A Defense change with no damage running leaves healing exactly as it was")]
    public void DefenseChangeWithoutDamageDoesNothing()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 500, 0, 1000, defense, new ModifierGroupCounter());
        health.Add(new ModInstance<int, int>(6, 3));
        Card<int, string> partway = new(2, timeline, PriorityRank.None);
        timeline.AddEvent(partway);
        Card<int, string> end = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> reached = new();
        partway.Occurring += reached.OnTrigger;
        end.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, partway))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        defense.AddInstantChange(new Rational<long>(-4));

        Assert.Equal(6, health.Derivative);
        Assert.Equal(2, health.TimeSinceLastIncrement);
        Assert.False(health.IsRateChangePending);

        while (!ReferenceEquals(reached.MostRecentEvent, end))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(506, health.CurrentValue);
    }
    #endregion
    #region Instant damage
    [Fact(DisplayName = "Instant damage is divided by the Defense and rounded up, so any damage at all takes at least 1")]
    public void InstantDamageRoundsUp()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());

        Assert.Equal(2, health.ApplyInstantDamage(new Rational<long>(20)));
        Assert.Equal(998, health.CurrentValue);
        Assert.Equal(3, health.ApplyInstantDamage(new Rational<long>(25)));
        Assert.Equal(995, health.CurrentValue);
        Assert.Equal(1, health.ApplyInstantDamage(new Rational<long>(1, 3)));
        Assert.Equal(994, health.CurrentValue);
        Assert.Equal(0, health.ApplyInstantDamage(Rational<long>.Zero));
        Assert.Equal(994, health.CurrentValue);
    }
    [Fact(DisplayName = "Instant damage uses the Defense as it is right now, and says how much health was actually lost when the minimum gets in the way")]
    public void InstantDamageUsesCurrentDefenseAndStopsAtTheMinimum()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 12, 0, 1000, defense, new ModifierGroupCounter());
        defense.AddInstantChange(new Rational<long>(1));

        Assert.Equal(5, health.ApplyInstantDamage(new Rational<long>(100)));
        Assert.Equal(7, health.CurrentValue);
        Assert.Equal(7, health.ApplyInstantDamage(new Rational<long>(1000)));
        Assert.Equal(0, health.CurrentValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => health.ApplyInstantDamage(new Rational<long>(-1)));
    }
    #endregion
    #region Pausing
    [Fact(DisplayName = "A paused health meter doesn't move and no threshold on it occurs, and when resumed it carries on from the same point in its tick")]
    public void PauseHoldsTheTickAndThresholds()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        health.AddDamage(new ModInstance<int, int>(100, 5));
        DiscreteReachThresholdEvent<int, int> threshold = new(health, 990, PriorityRank.MeterTriggerProgressive, timeline) { CouldOccur = true };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> thresholdOccurred = new();
        threshold.Occurring += thresholdOccurred.OnTrigger;
        Card<int, string> pauseAt = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(pauseAt);
        Card<int, string> resumeAt = new(10, timeline, PriorityRank.None);
        timeline.AddEvent(resumeAt);
        Card<int, string> beforeTick = new(11, timeline, PriorityRank.None);
        timeline.AddEvent(beforeTick);
        Card<int, string> afterTick = new(13, timeline, PriorityRank.None);
        timeline.AddEvent(afterTick);
        MostRecentEventHolder<int> reached = new();
        pauseAt.Occurring += reached.OnTrigger;
        resumeAt.Occurring += reached.OnTrigger;
        beforeTick.Occurring += reached.OnTrigger;
        afterTick.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, pauseAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(new TimeOrNever<int>(2), threshold.TimeRemaining);
        health.StartIntangible();
        Assert.True(health.IsPaused);
        Assert.True(threshold.TimeRemaining.IsNever);

        while (!ReferenceEquals(reached.MostRecentEvent, resumeAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //The tick that was due at 5 never landed
        Assert.Equal(1000, health.CurrentValue);
        Assert.Equal(3, health.TimeSinceLastIncrement);
        Assert.Null(thresholdOccurred.MostRecentEvent);
        health.EndIntangible();
        Assert.False(health.IsPaused);
        Assert.Equal(new TimeOrNever<int>(2), threshold.TimeRemaining);

        while (!ReferenceEquals(reached.MostRecentEvent, beforeTick))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(1000, health.CurrentValue);
        Assert.Null(thresholdOccurred.MostRecentEvent);

        while (!ReferenceEquals(reached.MostRecentEvent, afterTick))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //3 of the tick before the pause and 2 after it, so it landed at 12
        Assert.Equal(990, health.CurrentValue);
        Assert.Same(threshold, thresholdOccurred.MostRecentEvent);
        Assert.Equal(Rational<long>.Zero, health.CurrentSpilloverFraction);
        Assert.Equal(0, health.MidAdvanceRateChanges);
    }
    [Fact(DisplayName = "What a paused health meter is owed for rounding doesn't build up while it is paused")]
    public void PauseHoldsTheSpillover()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 3, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        //7 every 5 against a Defense of 3 is 2 and 1/3 a tick, so each tick of 2 leaves 1/3 owed
        health.AddDamage(new ModInstance<int, int>(7, 5));
        Card<int, string> pauseAt = new(5, timeline, PriorityRank.None);
        timeline.AddEvent(pauseAt);
        Card<int, string> resumeAt = new(100, timeline, PriorityRank.None);
        timeline.AddEvent(resumeAt);
        MostRecentEventHolder<int> reached = new();
        pauseAt.Occurring += reached.OnTrigger;
        resumeAt.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, pauseAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(998, health.CurrentValue);
        Assert.Equal(new Rational<long>(-1, 3), health.CurrentSpilloverFraction);
        health.StartIntangible();

        while (!ReferenceEquals(reached.MostRecentEvent, resumeAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(998, health.CurrentValue);
        Assert.Equal(new Rational<long>(-1, 3), health.CurrentSpilloverFraction);
        Assert.Equal(LeapYearState.None, health.CurrentLeapYear);
        Assert.Equal(LeapYearState.None, health.UpcomingLeapYear);
    }
    [Fact(DisplayName = "An instant heal on a paused health meter still happens, and a threshold it crosses still occurs")]
    public void InstantChangeWhilePausedStillCrossesThresholds()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 500, 0, 1000, defense, new ModifierGroupCounter());
        health.AddDamage(new ModInstance<int, int>(100, 5));
        DiscreteReachThresholdEvent<int, int> threshold = new(health, 600, PriorityRank.MeterTriggerInstant, timeline) { CouldOccur = true, Direction = ThresholdDirection.Up };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> thresholdOccurred = new();
        threshold.Occurring += thresholdOccurred.OnTrigger;
        Card<int, string> end = new(5, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> reached = new();
        end.Occurring += reached.OnTrigger;
        health.StartIntangible();

        health.AddInstantChange(200);

        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Same(threshold, thresholdOccurred.MostRecentEvent);
        Assert.Equal(700, health.CurrentValue);
    }
    [Fact(DisplayName = "Damage added to a paused health meter does nothing until it is resumed, and then starts from that moment")]
    public void DamageAddedWhilePausedStartsOnResume()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        Card<int, string> resumeAt = new(10, timeline, PriorityRank.None);
        timeline.AddEvent(resumeAt);
        Card<int, string> beforeTick = new(14, timeline, PriorityRank.None);
        timeline.AddEvent(beforeTick);
        Card<int, string> atTick = new(15, timeline, PriorityRank.None);
        timeline.AddEvent(atTick);
        MostRecentEventHolder<int> reached = new();
        resumeAt.Occurring += reached.OnTrigger;
        beforeTick.Occurring += reached.OnTrigger;
        atTick.Occurring += reached.OnTrigger;
        health.StartIntangible();

        health.AddDamage(new ModInstance<int, int>(100, 5));

        Assert.Equal(new Rational<long>(-2), health.TrueRate);
        Assert.Equal(0, health.Derivative);

        while (!ReferenceEquals(reached.MostRecentEvent, resumeAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(1000, health.CurrentValue);
        health.EndIntangible();
        Assert.Equal(-10, health.Derivative);
        Assert.Equal(5, health.DerivativeDenominator);

        while (!ReferenceEquals(reached.MostRecentEvent, beforeTick))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(1000, health.CurrentValue);

        while (!ReferenceEquals(reached.MostRecentEvent, atTick))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(990, health.CurrentValue);
        Assert.Equal(0, health.MidAdvanceRateChanges);
    }
    [Fact(DisplayName = "A Defense change while paused changes nothing until the health meter is resumed, and then the tick in progress lands with the new amount")]
    public void DefenseChangeWhilePausedAppliesOnResume()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        health.AddDamage(new ModInstance<int, int>(200, 5));
        Card<int, string> pauseAt = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(pauseAt);
        Card<int, string> resumeAt = new(10, timeline, PriorityRank.None);
        timeline.AddEvent(resumeAt);
        Card<int, string> atTick = new(12, timeline, PriorityRank.None);
        timeline.AddEvent(atTick);
        MostRecentEventHolder<int> reached = new();
        pauseAt.Occurring += reached.OnTrigger;
        resumeAt.Occurring += reached.OnTrigger;
        atTick.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, pauseAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        health.StartIntangible();
        defense.AddInstantChange(new Rational<long>(1));
        Assert.Equal(-20, health.Derivative);

        while (!ReferenceEquals(reached.MostRecentEvent, resumeAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        health.EndIntangible();
        Assert.Equal(-10, health.Derivative);
        Assert.Equal(3, health.TimeSinceLastIncrement);

        while (!ReferenceEquals(reached.MostRecentEvent, atTick))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(990, health.CurrentValue);
        Assert.Equal(0, health.BlendPerTick);
        Assert.Equal(0, health.MidAdvanceRateChanges);
    }
    #endregion
    #region Going down and coming back
    [Fact(DisplayName = "After a knock out nothing over time changes the health, including the healing tick that was already in progress")]
    public void KnockOutStopsTheTickInProgress()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 1000, defense, new ModifierGroupCounter());
        health.Add(new ModInstance<int, int>(10, 5));
        Card<int, string> knockOutAt = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(knockOutAt);
        Card<int, string> longAfter = new(20, timeline, PriorityRank.None);
        timeline.AddEvent(longAfter);
        MostRecentEventHolder<int> reached = new();
        knockOutAt.Occurring += reached.OnTrigger;
        longAfter.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, knockOutAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(100, health.ApplyInstantDamage(new Rational<long>(1000)));
        health.KnockOut();
        Assert.True(health.IsDown);
        Assert.True(health.IsPaused);

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //The healing tick that was due at 5 never landed, and neither did any after it
        Assert.Equal(0, health.CurrentValue);
        Assert.Equal(3, health.TimeSinceLastIncrement);
    }
    [Fact(DisplayName = "A health meter that is both down and intangible stays paused until neither holds, whichever ends first")]
    public void DownAndIntangibleBothHoldThePause()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());

        health.StartIntangible();
        health.KnockOut();
        health.EndIntangible();
        Assert.True(health.IsPaused);
        health.Revive(50);
        Assert.False(health.IsPaused);

        health.KnockOut();
        health.StartIntangible();
        health.Revive(50);
        Assert.True(health.IsPaused);
        Assert.False(health.IsDown);
        health.EndIntangible();
        Assert.False(health.IsPaused);
    }
    [Fact(DisplayName = "Reviving puts the health at the given value with nothing changing it, and what was changing it before never comes back")]
    public void ReviveDropsEverything()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        ModifierKey healKey = health.Add(new ModInstance<int, int>(10, 4));
        //Runs from 1 to 21, so it is still going when the health comes back
        DamagePhaseEvent<int> damagePhase = new(health, new ModInstance<int, int>(100, 5), 1, 20, PriorityRank.DamagePhaseStart, PriorityRank.DamagePhaseEnd, timeline);
        timeline.AddEvent(damagePhase);
        Card<int, string> knockOutAt = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(knockOutAt);
        Card<int, string> reviveAt = new(8, timeline, PriorityRank.None);
        timeline.AddEvent(reviveAt);
        Card<int, string> longAfter = new(30, timeline, PriorityRank.None);
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
        Assert.NotEqual(0, health.TimeSinceLastIncrement);
        health.KnockOut();

        while (!ReferenceEquals(reached.MostRecentEvent, reviveAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        health.Revive(500);

        Assert.Equal(500, health.CurrentValue);
        Assert.False(health.IsDown);
        Assert.False(health.IsPaused);
        Assert.Empty(health.Modifiers);
        Assert.Empty(health.DamageModifiers);
        Assert.Equal(Rational<long>.Zero, health.TrueRate);
        Assert.Equal(0, health.Derivative);
        Assert.Equal(0, health.TimeSinceLastIncrement);
        Assert.Equal(Rational<long>.Zero, health.CurrentSpilloverFraction);
        Assert.False(health.Remove(healKey));

        while (!ReferenceEquals(reached.MostRecentEvent, longAfter))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        //The damage phase ended at 21 and found nothing of its own left to remove
        Assert.True(damagePhase.IsFinished);
        Assert.Equal(500, health.CurrentValue);
        Assert.Equal(0, health.MidAdvanceRateChanges);
    }
    [Fact(DisplayName = "Damage added after reviving starts its first tick from then, not from where the dropped tick had got to")]
    public void DamageAfterReviveStartsItsOwnTick()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        health.AddDamage(new ModInstance<int, int>(200, 5));
        Card<int, string> reviveAt = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(reviveAt);
        Card<int, string> beforeTick = new(7, timeline, PriorityRank.None);
        timeline.AddEvent(beforeTick);
        Card<int, string> atTick = new(8, timeline, PriorityRank.None);
        timeline.AddEvent(atTick);
        MostRecentEventHolder<int> reached = new();
        reviveAt.Occurring += reached.OnTrigger;
        beforeTick.Occurring += reached.OnTrigger;
        atTick.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, reviveAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        health.KnockOut();
        health.Revive(500);
        health.AddDamage(new ModInstance<int, int>(100, 5));

        while (!ReferenceEquals(reached.MostRecentEvent, beforeTick))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(500, health.CurrentValue);

        while (!ReferenceEquals(reached.MostRecentEvent, atTick))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(490, health.CurrentValue);
        Assert.Equal(0, health.BlendPerTick);
        Assert.Equal(0, health.MidAdvanceRateChanges);
    }
    [Fact(DisplayName = "Reviving partway through a leap year forgets the leap year and what was owed, so later damage is dealt as on a new meter")]
    public void ReviveForgetsTheLeapYear()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 3, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        //7 every 5 against a Defense of 3 is 2 and 1/3 a tick, which starts a leap year at the tick at 15
        health.AddDamage(new ModInstance<int, int>(7, 5));
        Card<int, string> reviveAt = new(20, timeline, PriorityRank.None);
        timeline.AddEvent(reviveAt);
        Card<int, string> end = new(30, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> reached = new();
        reviveAt.Occurring += reached.OnTrigger;
        end.Occurring += reached.OnTrigger;

        while (!ReferenceEquals(reached.MostRecentEvent, reviveAt))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(LeapYearState.Negative, health.CurrentLeapYear);
        Assert.NotEqual(Rational<long>.Zero, health.CurrentSpilloverFraction);
        health.KnockOut();
        health.Revive(500);

        Assert.Equal(LeapYearState.None, health.CurrentLeapYear);
        Assert.Equal(LeapYearState.None, health.UpcomingLeapYear);
        Assert.Equal(Rational<long>.Zero, health.CurrentSpilloverFraction);

        //30 every 5 against a Defense of 3 is exactly 10 a tick
        health.AddDamage(new ModInstance<int, int>(30, 5));
        while (!ReferenceEquals(reached.MostRecentEvent, end))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(480, health.CurrentValue);
        Assert.Equal(LeapYearState.None, health.CurrentLeapYear);
        Assert.Equal(LeapYearState.None, health.UpcomingLeapYear);
        Assert.Equal(Rational<long>.Zero, health.CurrentSpilloverFraction);
        Assert.Equal(0, health.MidAdvanceRateChanges);
    }
    [Fact(DisplayName = "Reviving is an instant change, so a threshold between the old health and the new one occurs")]
    public void ReviveCrossesThresholds()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 0, 0, 1000, defense, new ModifierGroupCounter());
        health.KnockOut();
        DiscreteReachThresholdEvent<int, int> threshold = new(health, 1, PriorityRank.MeterTriggerInstant, timeline) { CouldOccur = true, Direction = ThresholdDirection.Up };
        timeline.AddEvent(threshold);
        MostRecentEventHolder<int> thresholdOccurred = new();
        threshold.Occurring += thresholdOccurred.OnTrigger;
        Card<int, string> end = new(1, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> reached = new();
        end.Occurring += reached.OnTrigger;

        health.Revive(50);

        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Same(threshold, thresholdOccurred.MostRecentEvent);
        Assert.Equal(50, health.CurrentValue);
    }
    [Fact(DisplayName = "Reviving at a health outside the minimum and maximum is refused and changes nothing")]
    public void ReviveOutsideTheRangeIsRefused()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        health.AddDamage(new ModInstance<int, int>(100, 5));
        health.Add(new ModInstance<int, int>(1, 5));
        health.KnockOut();

        Assert.Throws<ArgumentOutOfRangeException>(() => health.Revive(1001));
        Assert.Throws<ArgumentOutOfRangeException>(() => health.Revive(-1));

        Assert.True(health.IsDown);
        Assert.True(health.IsPaused);
        Assert.Equal(1000, health.CurrentValue);
        Assert.Single(health.DamageModifiers);
        Assert.Single(health.Modifiers);
    }
    [Fact(DisplayName = "A health meter cloned while down is down too, and reviving the clone leaves the original down")]
    public void CloneOfADownMeterIsDown()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        health.AddDamage(new ModInstance<int, int>(100, 5));
        health.KnockOut();

        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone(health);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(health, out HealthMeter<int> healthClone));

        Assert.True(healthClone.IsDown);
        Assert.True(healthClone.IsPaused);
        healthClone.Revive(500);

        Assert.True(health.IsDown);
        Assert.Equal(1000, health.CurrentValue);
        Assert.Single(health.DamageModifiers);
        Assert.False(healthClone.IsDown);
        Assert.Equal(500, healthClone.CurrentValue);
        Assert.Empty(healthClone.DamageModifiers);
    }
    #endregion
    #region Cloning
    [Fact(DisplayName = "A cloned health meter is a health meter on the cloned Defense, carries on with the same damage, and a Defense change on the clone doesn't touch the original")]
    public void CloneCarriesOnSeparately()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        ModifierKey key = health.AddDamage(new ModInstance<int, int>(200, 5));
        ModifierKey healKey = health.Add(new ModInstance<int, int>(5, 5));
        Card<int, string> cloneAt = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(cloneAt);
        MostRecentEventHolder<int> reachedCloneAt = new();
        cloneAt.Occurring += reachedCloneAt.OnTrigger;
        Card<int, string> end = new(10, timeline, PriorityRank.None);
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
        Assert.True(context.TryGet(defense, out StatModificationMeter<int> defenseClone));
        Assert.True(context.TryGet(reachedEnd, out MostRecentEventHolder<int> reachedEndClone));

        Assert.NotSame(health, healthClone);
        Assert.Same(timelineClone, healthClone.OwningTimeline);
        Assert.Same(defenseClone, healthClone.Defense);
        Assert.Equal(health.UUID, healthClone.UUID);
        Assert.Equal(new ModInstance<int, int>(200, 5), healthClone.DamageModifiers[key]);
        Assert.Equal(new ModInstance<int, int>(5, 5), healthClone.Modifiers[healKey]);
        Assert.Equal(health.TrueRate, healthClone.TrueRate);
        Assert.Equal((health.Derivative, health.DerivativeDenominator, health.TimeSinceLastIncrement),
            (healthClone.Derivative, healthClone.DerivativeDenominator, healthClone.TimeSinceLastIncrement));

        //Twice the Defense on the clone only
        defenseClone.AddInstantChange(new Rational<long>(1));
        Assert.Equal(-5, healthClone.Derivative);
        Assert.Equal(-15, health.Derivative);

        while (reachedEnd.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        while (reachedEndClone.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timelineClone.TakeSteps()) { }
            timelineClone.SetOpen();
        }
        Assert.Equal(970, health.CurrentValue);
        Assert.Equal(990, healthClone.CurrentValue);
        Assert.Equal(0, health.MidAdvanceRateChanges);
        Assert.Equal(0, healthClone.MidAdvanceRateChanges);
    }
    [Fact(DisplayName = "A cloned health meter gives the same keys next as its original would, and making more groups on the clone's counter doesn't change the original's")]
    public void CloneKeepsItsGroupNumbersAndItsOwnCounter()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        ModifierGroupCounter counter = new();
        HealthMeter<int> health = new(timeline, 500, 0, 1000, defense, counter);
        ModifierKey healKey = health.Add(new ModInstance<int, int>(1, 5));
        ModifierKey damageKey = health.AddDamage(new ModInstance<int, int>(100, 5));

        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone(health);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(health, out HealthMeter<int> healthClone));
        Assert.True(context.TryGet(counter, out ModifierGroupCounter counterClone));

        Assert.Same(counterClone, healthClone.GroupCounter);
        Assert.NotSame(counter, counterClone);
        Assert.Equal(counter.GroupsMade, counterClone.GroupsMade);
        //The keys from before the clone work on it, each on its own group only
        Assert.False(healthClone.RemoveDamage(healKey));
        Assert.True(healthClone.RemoveDamage(damageKey));
        Assert.True(healthClone.Remove(healKey));
        //And the next key from each is the same on both
        Assert.Equal(health.Add(new ModInstance<int, int>(2, 5)), healthClone.Add(new ModInstance<int, int>(2, 5)));
        Assert.Equal(health.AddDamage(new ModInstance<int, int>(50, 5)), healthClone.AddDamage(new ModInstance<int, int>(50, 5)));

        StatModificationMeter<int> defenseClone = healthClone.Defense;
        HealthMeter<int> madeOnTheClone = new(healthClone.OwningTimeline, 500, 0, 1000, defenseClone, counterClone);

        Assert.Equal(2, counter.GroupsMade);
        Assert.Equal(4, counterClone.GroupsMade);
        Assert.NotEqual(healthClone.Add(new ModInstance<int, int>(3, 5)).Group, madeOnTheClone.Add(new ModInstance<int, int>(3, 5)).Group);
    }
    [Fact(DisplayName = "A health meter cloned while paused is paused too, and resuming the clone leaves the original paused")]
    public void CloneOfAPausedMeterIsPaused()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 1000, 0, 1000, defense, new ModifierGroupCounter());
        health.AddDamage(new ModInstance<int, int>(100, 5));
        Card<int, string> end = new(5, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> reachedEnd = new();
        end.Occurring += reachedEnd.OnTrigger;
        health.StartIntangible();

        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone(health);
        context.GetOrClone(reachedEnd);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(timeline, out Timeline<int> timelineClone));
        Assert.True(context.TryGet(health, out HealthMeter<int> healthClone));
        Assert.True(context.TryGet(reachedEnd, out MostRecentEventHolder<int> reachedEndClone));

        Assert.True(healthClone.IsPaused);
        healthClone.EndIntangible();
        Assert.True(health.IsPaused);

        while (reachedEnd.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        while (reachedEndClone.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timelineClone.TakeSteps()) { }
            timelineClone.SetOpen();
        }
        Assert.Equal(1000, health.CurrentValue);
        Assert.Equal(990, healthClone.CurrentValue);
    }
    #endregion
}
