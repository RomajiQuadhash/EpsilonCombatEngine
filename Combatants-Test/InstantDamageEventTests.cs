using Combatants;
using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Combatants_Test;

/// <summary>
/// Tests for InstantDamageEvent, damage dealt to a HealthMeter all at once at a set time.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class InstantDamageEventTests
{
    [Fact(DisplayName = "An instant damage event does nothing until its time, then deals its damage divided by the Defense and rounded up, and says how much health was lost")]
    public void DealsItsDamageAtItsTime()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        //25 over a Defense of 10 is 2 and a half, so 3
        InstantDamageEvent<int> damage = new(health, new Rational<long>(25), 4, PriorityRank.InstantDamage, timeline);
        timeline.AddEvent(damage);
        Card<int, string> before = new(3, timeline, PriorityRank.None);
        timeline.AddEvent(before);
        Card<int, string> after = new(5, timeline, PriorityRank.None);
        timeline.AddEvent(after);
        MostRecentEventHolder<int> reached = new();
        before.Occurring += reached.OnTrigger;
        after.Occurring += reached.OnTrigger;

        Assert.Same(health, damage.MeterAppliedTo);
        Assert.Same(damage, damage.AsOkazo);
        Assert.Equal(new Rational<long>(25), damage.Magnitude);

        while (!ReferenceEquals(reached.MostRecentEvent, before))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(100, health.CurrentValue);
        Assert.False(damage.IsFinished);
        Assert.Equal(0, damage.AppliedDamage);

        while (!ReferenceEquals(reached.MostRecentEvent, after))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(97, health.CurrentValue);
        Assert.True(damage.IsFinished);
        Assert.Equal(3, damage.AppliedDamage);
    }
    [Fact(DisplayName = "An instant damage event uses the Defense as it is when it occurs, ignores a Guard, and only counts the health that was there to lose")]
    public void UsesDefenseAtTheTimeAndStopsAtTheMinimum()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int>.BuildGuard(timeline, 10, new Rational<long>(-6), new Rational<long>(6), out LinearContinuousMeter<int, double> guard, out StatModificationMeter<int> guardStrength);
        HealthMeter<int> health = new(timeline, 12, 0, 100, defense, new ModifierGroupCounter(), guard, guardStrength);
        InstantDamageEvent<int> first = new(health, new Rational<long>(100), 2, PriorityRank.InstantDamage, timeline);
        timeline.AddEvent(first);
        InstantDamageEvent<int> second = new(health, new Rational<long>(1000), 4, PriorityRank.InstantDamage, timeline);
        timeline.AddEvent(second);
        Card<int, string> end = new(5, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> reached = new();
        end.Occurring += reached.OnTrigger;
        //Made after the events, and still counts: 100 over a Defense of 20 is 5, not 10
        defense.AddInstantChange(new Rational<long>(1));

        while (reached.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }
        Assert.Equal(5, first.AppliedDamage);
        Assert.Equal(7, second.AppliedDamage);
        Assert.Equal(0, health.CurrentValue);
        Assert.Equal(10, guard.CurrentValue);
    }
    [Fact(DisplayName = "An instant damage event can't be made with negative damage")]
    public void NegativeDamageIsRefused()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());

        Assert.Throws<ArgumentOutOfRangeException>(() => new InstantDamageEvent<int>(health, new Rational<long>(-1), 4, PriorityRank.InstantDamage, timeline));
    }
    [Fact(DisplayName = "A cloned instant damage event deals its damage to the cloned health meter, and the original health is untouched")]
    public void CloneDamagesTheClone()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        InstantDamageEvent<int> damage = new(health, new Rational<long>(200), 4, PriorityRank.InstantDamage, timeline);
        timeline.AddEvent(damage);
        Card<int, string> end = new(5, timeline, PriorityRank.None);
        timeline.AddEvent(end);
        MostRecentEventHolder<int> reached = new();
        end.Occurring += reached.OnTrigger;

        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone(health);
        context.GetOrClone(reached);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(timeline, out Timeline<int> timelineClone));
        Assert.True(context.TryGet(health, out HealthMeter<int> healthClone));
        Assert.True(context.TryGet(damage, out InstantDamageEvent<int> damageClone));
        Assert.True(context.TryGet(reached, out MostRecentEventHolder<int> reachedClone));
        Assert.Same(healthClone, damageClone.MeterAppliedTo);
        Assert.Equal(new Rational<long>(200), damageClone.Magnitude);

        while (reachedClone.MostRecentEvent is null)
        {
            foreach (StepReport<int> _ in timelineClone.TakeSteps()) { }
            timelineClone.SetOpen();
        }
        Assert.Equal(80, healthClone.CurrentValue);
        Assert.True(damageClone.IsFinished);
        Assert.Equal(20, damageClone.AppliedDamage);
        Assert.Equal(100, health.CurrentValue);
        Assert.False(damage.IsFinished);
    }
}
