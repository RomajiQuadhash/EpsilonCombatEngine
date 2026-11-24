using CoreMetrics;
using Meters;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Meters_Test;

/// <summary>
/// Tests for InstantChangeEvent, the event that makes one instant change to a meter and is then done.
/// </summary>
/// <remarks>Claude written but my style</remarks>
public class InstantChangeEventTests
{
    [Fact(DisplayName = "An InstantChangeEvent starts with its time, its priority, and the meter untouched")]
    public void StartsWithItsTimeAndPriority()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        InstantChangeEvent<int, int> damage = new(meter, -20, 3, PriorityRank.InstantDamage, timeline);

        Assert.Equal(new TimeOrNever<int>(3), damage.TimeRemaining);
        Assert.Equal(PriorityRank.InstantDamage, damage.Priority);
        Assert.True(damage.CouldOccur);
        Assert.False(damage.IsFinished);
        Assert.Equal(0, damage.AppliedChange);
        Assert.Equal(50, meter.CurrentValue);
    }
    [Fact(DisplayName = "An InstantChangeEvent makes its change when its time comes")]
    public void MakesItsChange()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        InstantChangeEvent<int, int> damage = new(meter, -20, 3, PriorityRank.InstantDamage, timeline);
        timeline.AddEvent(damage);
        int totalAdvance = 0;

        foreach (StepReport<int> report in timeline.TakeSteps())
        {
            totalAdvance += report.AdvanceAmount;
        }

        Assert.Equal(3, totalAdvance);
        Assert.Equal(30, meter.CurrentValue);
        Assert.Equal(-20, meter.InstantChange);
        Assert.Equal(-20, damage.AppliedChange);
    }
    [Fact(DisplayName = "After occurring, an InstantChangeEvent is finished and off the timeline")]
    public void IsDoneAfterOccurring()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        InstantChangeEvent<int, int> damage = new(meter, -20, 3, PriorityRank.InstantDamage, timeline);
        timeline.AddEvent(damage);

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }

        Assert.True(damage.IsFinished);
        Assert.DoesNotContain(damage, timeline.Events);
        Assert.DoesNotContain(damage, timeline.PossibleEvents);
        Assert.DoesNotContain(damage, timeline.GetAdvanceSubscribers());
    }
    [Fact(DisplayName = "An InstantChangeEvent notes how much actually fit if the meter hits its limit")]
    public void NotesWhatFit()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 15, 0, 100);
        InstantChangeEvent<int, int> damage = new(meter, -20, 3, PriorityRank.InstantDamage, timeline);
        timeline.AddEvent(damage);

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }

        Assert.Equal(0, meter.CurrentValue);
        Assert.Equal(-15, damage.AppliedChange);
    }
    [Fact(DisplayName = "An InstantChangeEvent tells its listeners before the meter has changed")]
    public void ListenersHearBeforeTheChange()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        InstantChangeEvent<int, int> damage = new(meter, -20, 3, PriorityRank.InstantDamage, timeline);
        timeline.AddEvent(damage);
        int valueWhenTold = -1;
        damage.Occurring += (_, _) => valueWhenTold = meter.CurrentValue;

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }

        Assert.Equal(50, valueWhenTold);
        Assert.Equal(30, meter.CurrentValue);
    }
    [Fact(DisplayName = "An InstantChangeEvent can't be triggered before its time")]
    public void CannotTriggerEarly()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        InstantChangeEvent<int, int> damage = new(meter, -20, 3, PriorityRank.InstantDamage, timeline);

        Assert.Throws<InvalidOperationException>(damage.Trigger);
        Assert.Equal(50, meter.CurrentValue);
    }
    [Fact(DisplayName = "An InstantChangeEvent's time can't be set through TimeRemaining, only through TimeToEvent")]
    public void TimeRemainingCannotBeSet()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        InstantChangeEvent<int, int> damage = new(meter, -20, 3, PriorityRank.InstantDamage, timeline);

        Assert.Throws<InvalidOperationException>(() => damage.TimeRemaining = new TimeOrNever<int>(7));
        Assert.Throws<InvalidOperationException>(() => damage.TimeRemaining = new TimeOrNever<int>(true));
        Assert.Equal(new TimeOrNever<int>(3), damage.TimeRemaining);

        damage.TimeToEvent = 7;
        Assert.Equal(new TimeOrNever<int>(7), damage.TimeRemaining);
    }
    [Fact(DisplayName = "A clone of an InstantChangeEvent changes the cloned meter and leaves the original alone")]
    public void CloneActsOnTheClonedMeter()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        InstantChangeEvent<int, int> damage = new(meter, -20, 3, PriorityRank.InstantDamage, timeline);
        timeline.AddEvent(damage);

        CloneContext context = new();
        Timeline<int> timelineClone = (Timeline<int>)timeline.Clone(context);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(meter, out InstantChangeMeter<int, int> meterClone)); //Cloning the event brought its meter along
        Assert.True(context.TryGet(damage, out InstantChangeEvent<int, int> damageClone));
        Assert.Same(meterClone, damageClone.MeterAppliedTo);
        Assert.Equal(damage.UUID, damageClone.UUID);
        Assert.Equal(damage.TimeRemaining, damageClone.TimeRemaining);
        Assert.Equal(damage.Priority, damageClone.Priority);

        foreach (StepReport<int> _ in timelineClone.TakeSteps()) { }

        Assert.Equal(30, meterClone.CurrentValue);
        Assert.True(damageClone.IsFinished);
        Assert.Equal(50, meter.CurrentValue);
        Assert.False(damage.IsFinished);
        Assert.Equal(new TimeOrNever<int>(3), damage.TimeRemaining);
    }
}
