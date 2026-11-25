using Meters;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Meters_Test;

/// <summary>
/// Tests for IMeterChangeEvent, what every event that changes a meter can be treated as.
/// </summary>
/// <remarks>Claude written, Romaji Approved</remarks>
public class MeterChangeEventTests
{
    [Fact(DisplayName = "An InstantChangeEvent, seen as a meter change event, gives its meter, timeline and priority, and is itself as an Okazo")]
    public void InstantChangeEventAsAMeterChangeEvent()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        InstantChangeEvent<int, int> damage = new(meter, -20, 3, PriorityRank.InstantDamage, timeline);

        IMeterChangeEvent<int, int> change = damage;

        Assert.Same(meter, change.MeterAppliedTo);
        Assert.Same(timeline, change.OwningTimeline);
        Assert.Equal(PriorityRank.InstantDamage, change.Priority);
        Assert.Same(damage, change.AsOkazo);
        Assert.False(change.IsFinished);
    }
    [Fact(DisplayName = "An InstantChangeEvent, seen as a meter change event, is finished once it has occurred")]
    public void InstantChangeEventFinishes()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        IMeterChangeEvent<int, int> change = new InstantChangeEvent<int, int>(meter, -20, 3, PriorityRank.InstantDamage, timeline);
        timeline.AddEvent(change.AsOkazo);

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }

        Assert.True(change.IsFinished);
        Assert.Equal(30, meter.CurrentValue);
    }
    [Fact(DisplayName = "A temporary change, seen as a meter change event, gives its meter, timeline and priority, and is itself as an Okazo")]
    public void TempChangeAsAMeterChangeEvent()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 10, 0, 100);
        TempInstantChange<int, int> buff = new(meter, 5, 3, 4, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);

        IMeterChangeEvent<int, int> change = buff;

        Assert.Same(meter, change.MeterAppliedTo);
        Assert.Same(timeline, change.OwningTimeline);
        Assert.Equal(PriorityRank.MeterChangePhaseStart, change.Priority);
        Assert.Same(buff, change.AsOkazo);
        Assert.False(change.IsFinished);
    }
    [Fact(DisplayName = "A temporary change, seen as a meter change event, isn't finished when it applies, only once it has been removed again")]
    public void TempChangeFinishesOnlyWhenRemoved()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 10, 0, 100);
        IMeterChangeEvent<int, int> change = new TempInstantChange<int, int>(meter, 5, 3, 4, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);
        timeline.AddEvent(change.AsOkazo);

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        Assert.False(change.IsFinished);
        Assert.Equal(PriorityRank.MeterChangePhaseEnd, change.Priority); //Now waiting to be removed

        timeline.SetOpen();
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        Assert.True(change.IsFinished);
    }
    [Fact(DisplayName = "The more specific temporary changes give the same meter when seen as a meter change event")]
    public void SpecificTempChangesGiveTheSameMeter()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> continuousMeter = new(timeline, 50, 0, 100);
        RateManagedMeter<int, int> rateManagedMeter = new(timeline, 0, -1000, 1000);
        TempContinuousChange<int, double> onContinuous = new(continuousMeter, 3, 2, 5, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);
        TempRMMChange<int, int> onRateManaged = new(rateManagedMeter, new ModInstance<int, int>(5, 1), 2, 6, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);

        IMeterChangeEvent<int, double> continuousChange = onContinuous;
        IMeterChangeEvent<int, int> rateManagedChange = onRateManaged;

        Assert.Same(continuousMeter, continuousChange.MeterAppliedTo);
        Assert.Same(rateManagedMeter, rateManagedChange.MeterAppliedTo);
    }
    [Fact(DisplayName = "Different kinds of meter change event for the same meter can be kept together and asked the same things")]
    public void DifferentKindsTogether()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 50, 0, 100);
        List<IMeterChangeEvent<int, int>> changes =
        [
            new InstantChangeEvent<int, int>(meter, -20, 1, PriorityRank.InstantDamage, timeline),
            new TempInstantChange<int, int>(meter, 5, 2, 4, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline),
        ];
        foreach (IMeterChangeEvent<int, int> change in changes)
        {
            timeline.AddEvent(change.AsOkazo);
        }

        while (changes.Any(change => !change.IsFinished))
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        Assert.All(changes, change => Assert.Same(meter, change.MeterAppliedTo));
        Assert.Equal(30, meter.CurrentValue); //Down 20 for good, and up 5 only for a while
    }
}
