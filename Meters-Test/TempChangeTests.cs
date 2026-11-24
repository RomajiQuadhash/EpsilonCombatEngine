using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Meters_Test;

/// <summary>
/// Tests for the temporary changes: TempInstantChange, TempContinuousChange and TempRMMChange, and the timing they share from TimedTempChange.
/// Each run of TakeSteps goes until one event has occurred and the timeline is ready to display, so a test with only a temporary change on the timeline
/// gets the apply from its first run and the remove from its second.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class TempChangeTests
{
    #region Timing shared by all of them
    [Fact(DisplayName = "A temporary change starts out waiting to apply, with the waiting time as its time remaining")]
    public void StartsWaitingToApply()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 10, 0, 100);
        TempInstantChange<int, int> change = new(meter, 5, 3, 4, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);

        Assert.Equal(ApplyOrRemove.Apply, change.LifeCyclePhase);
        Assert.Equal(new TimeOrNever<int>(3), change.TimeRemaining);
        Assert.Equal(PriorityRank.MeterChangePhaseStart, change.Priority);
        Assert.True(change.CouldOccur);
        Assert.False(change.IsFinished);
        Assert.Equal(10, meter.CurrentValue); //Nothing has happened to the meter yet
    }
    [Fact(DisplayName = "After applying, a temporary change waits the apply time to be removed and uses its remove priority")]
    public void AfterApplyingWaitsToBeRemoved()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 10, 0, 100);
        TempInstantChange<int, int> change = new(meter, 5, 3, 4, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);
        timeline.AddEvent(change);

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }

        Assert.Equal(ApplyOrRemove.Remove, change.LifeCyclePhase);
        Assert.Equal(new TimeOrNever<int>(4), change.TimeRemaining);
        Assert.Equal(PriorityRank.MeterChangePhaseEnd, change.Priority);
        Assert.False(change.IsFinished);
        Assert.Contains(change, timeline.Events); //It put itself back on the timeline for the remove
    }
    [Fact(DisplayName = "After being removed, a temporary change is finished and off the timeline")]
    public void AfterRemovingIsFinished()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 10, 0, 100);
        TempInstantChange<int, int> change = new(meter, 5, 3, 4, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);
        timeline.AddEvent(change);

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        timeline.SetOpen();
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }

        Assert.True(change.IsFinished);
        Assert.DoesNotContain(change, timeline.Events);
        Assert.DoesNotContain(change, timeline.PossibleEvents);
        Assert.DoesNotContain(change, timeline.GetAdvanceSubscribers());
    }
    [Fact(DisplayName = "Setting the priority sets whichever of the apply and remove priorities is in use")]
    public void SettingPrioritySetsTheOneInUse()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 10, 0, 100);
        TempInstantChange<int, int> change = new(meter, 5, 3, 4, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);
        timeline.AddEvent(change);

        change.Priority = PriorityRank.PlayerBleedStart;
        Assert.Equal(PriorityRank.PlayerBleedStart, change.ApplyPriority);
        Assert.Equal(PriorityRank.MeterChangePhaseEnd, change.RemovePriority);

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        change.Priority = PriorityRank.PlayerBleedEnd;
        Assert.Equal(PriorityRank.PlayerBleedStart, change.ApplyPriority);
        Assert.Equal(PriorityRank.PlayerBleedEnd, change.RemovePriority);
    }
    [Fact(DisplayName = "A temporary change can't be made with a negative apply time")]
    public void NegativeApplyTimeIsRefused()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 10, 0, 100);

        Assert.Throws<ArgumentOutOfRangeException>(() => new TempInstantChange<int, int>(meter, 5, 3, -1, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline));
    }
    [Fact(DisplayName = "A temporary change with a negative waiting time rewinds the timeline to when it should have applied")]
    public void NegativeWaitingTimeRewinds()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 10, 0, 100);
        TempInstantChange<int, int> change = new(meter, 5, -2, 4, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);
        timeline.AddEvent(change);
        Assert.Equal(new TimeOrNever<int>(-2), change.TimeRemaining);
        int totalAdvance = 0;

        foreach (StepReport<int> report in timeline.TakeSteps())
        {
            totalAdvance += report.AdvanceAmount;
        }

        Assert.Equal(-2, totalAdvance);
        Assert.Equal(15, meter.CurrentValue);
        Assert.Equal(ApplyOrRemove.Remove, change.LifeCyclePhase);
        Assert.Equal(new TimeOrNever<int>(4), change.TimeRemaining); //The apply time counts from when it applied, not from when it was made
    }
    [Fact(DisplayName = "A temporary change's time can't be set through TimeRemaining, only through TimeToEvent")]
    public void TimeRemainingCannotBeSet()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 10, 0, 100);
        TempInstantChange<int, int> change = new(meter, 5, 3, 4, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);

        Assert.Throws<InvalidOperationException>(() => change.TimeRemaining = new TimeOrNever<int>(7));
        Assert.Throws<InvalidOperationException>(() => change.TimeRemaining = new TimeOrNever<int>(true));
        Assert.Equal(new TimeOrNever<int>(3), change.TimeRemaining);

        change.TimeToEvent = 7;
        Assert.Equal(new TimeOrNever<int>(7), change.TimeRemaining);
    }
    [Fact(DisplayName = "A temporary change with no apply time is applied and removed without the timeline moving in between")]
    public void ZeroApplyTimeAppliesAndRemovesTogether()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 10, 0, 100);
        TempInstantChange<int, int> change = new(meter, 5, 3, 0, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);
        timeline.AddEvent(change);
        int totalAdvance = 0;

        foreach (StepReport<int> report in timeline.TakeSteps())
        {
            totalAdvance += report.AdvanceAmount;
        }

        //The remove is an instant action right after the apply, so one run does both
        Assert.Equal(3, totalAdvance);
        Assert.Equal(5, change.AppliedChange);
        Assert.True(change.IsFinished);
        Assert.Equal(10, meter.CurrentValue);
    }
    #endregion
    #region TempInstantChange
    [Fact(DisplayName = "TempInstantChange adds its change to the meter when it applies and takes it back off when it is removed")]
    public void InstantChangeAppliesAndIsUndone()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 10, 0, 100);
        TempInstantChange<int, int> change = new(meter, 5, 3, 4, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);
        timeline.AddEvent(change);

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        Assert.Equal(15, meter.CurrentValue);
        Assert.Equal(5, meter.InstantChange);
        Assert.Equal(5, change.AppliedChange);

        timeline.SetOpen();
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        Assert.Equal(10, meter.CurrentValue);
        Assert.Equal(-5, meter.InstantChange);
    }
    [Fact(DisplayName = "TempInstantChange only takes back off what actually fit when it applied")]
    public void InstantChangeUndoesOnlyWhatFit()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 98, 0, 100);
        TempInstantChange<int, int> change = new(meter, 5, 3, 4, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);
        timeline.AddEvent(change);

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        Assert.Equal(100, meter.CurrentValue); //Only 2 of the 5 fit under the maximum
        Assert.Equal(2, change.AppliedChange);

        timeline.SetOpen();
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        Assert.Equal(98, meter.CurrentValue); //Back where it started, not 95
    }
    [Fact(DisplayName = "TempInstantChange works as a buff on a StatModificationMeter")]
    public void InstantChangeBuffsAStat()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> meter = new(timeline, 10, new Rational<long>(-1), new Rational<long>(1));
        TempInstantChange<int, Rational<long>> buff = new(meter, new Rational<long>(1, 2), 3, 4, PriorityRank.StatusEffectStart, PriorityRank.StatusEffectEnd, timeline);
        timeline.AddEvent(buff);

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        Assert.Equal(new Rational<long>(15), meter.GetModifiedStat());
        Assert.Equal(StatModificationType.Buff, meter.ModificationType());

        timeline.SetOpen();
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        Assert.Equal(new Rational<long>(10), meter.GetModifiedStat());
        Assert.Equal(StatModificationType.None, meter.ModificationType());
    }
    [Fact(DisplayName = "A clone of an applied TempInstantChange is removed from the cloned meter and leaves the original alone")]
    public void InstantChangeCloneActsOnTheClonedMeter()
    {
        Timeline<int> timeline = new();
        InstantChangeMeter<int, int> meter = new(timeline, 10, 0, 100);
        TempInstantChange<int, int> change = new(meter, 5, 3, 4, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);
        timeline.AddEvent(change);
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        timeline.SetOpen();

        CloneContext context = new();
        Timeline<int> timelineClone = (Timeline<int>)timeline.Clone(context);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(meter, out InstantChangeMeter<int, int> meterClone)); //Cloning the change brought its meter along
        Assert.True(context.TryGet(change, out TempInstantChange<int, int> changeClone));
        Assert.Same(meterClone, changeClone.MeterAppliedTo);
        Assert.Equal(change.UUID, changeClone.UUID);
        Assert.Equal(ApplyOrRemove.Remove, changeClone.LifeCyclePhase);
        Assert.Equal(new TimeOrNever<int>(4), changeClone.TimeRemaining);
        Assert.Equal(5, changeClone.AppliedChange);

        foreach (StepReport<int> _ in timelineClone.TakeSteps()) { }

        Assert.Equal(10, meterClone.CurrentValue);
        Assert.True(changeClone.IsFinished);
        Assert.Equal(15, meter.CurrentValue);
        Assert.False(change.IsFinished);
    }
    #endregion
    #region TempDerivativeChange
    [Fact(DisplayName = "TempContinuousChange adds to the meter's derivative only between applying and being removed")]
    public void DerivativeChangeAppliesAndIsUndone()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 0, -1000, 1000);
        TempContinuousChange<int, double> change = new(meter, 3, 2, 5, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);
        timeline.AddEvent(change);
        Assert.Equal(0, meter.Derivative);

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        Assert.Equal(3, meter.Derivative);
        Assert.Equal(0, meter.CurrentValue); //It has only just started

        timeline.SetOpen();
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        Assert.Equal(0, meter.Derivative);
        Assert.Equal(15, meter.CurrentValue); //3 a time unit for 5
    }
    [Fact(DisplayName = "Two TempDerivativeChanges that overlap add together while both are applied")]
    public void DerivativeChangesStack()
    {
        Timeline<int> timeline = new();
        LinearContinuousMeter<int, double> meter = new(timeline, 0, -1000, 1000);
        TempContinuousChange<int, double> longChange = new(meter, 3, 2, 5, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline); //From 2 to 7
        TempContinuousChange<int, double> shortChange = new(meter, 2, 4, 2, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline); //From 4 to 6
        timeline.AddEvent(longChange);
        timeline.AddEvent(shortChange);
        MostRecentEventHolder<int> lastRemoved = new();
        longChange.Occurring += lastRemoved.OnTrigger;

        //longChange occurs twice. The first time is its apply, so keep going until it has been removed.
        while (!longChange.IsFinished)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        Assert.Same(longChange, lastRemoved.MostRecentEvent);
        Assert.True(shortChange.IsFinished);
        Assert.Equal(0, meter.Derivative);
        Assert.Equal(19, meter.CurrentValue); //3 for 5, and 2 more for 2 of those
    }
    #endregion
    #region TempModInstance
    [Fact(DisplayName = "TempRMMChange adds its modifier to the meter when it applies and removes that same one when it is removed")]
    public void ModInstanceAppliesAndIsRemoved()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        ModInstance<int, int> modifier = new(5, 1);
        TempRMMChange<int, int> change = new(meter, modifier, 2, 6, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);
        timeline.AddEvent(change);
        Assert.Null(change.Key);
        Assert.Empty(meter.Modifiers);

        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        timeline.SetOpen();
        Assert.NotNull(change.Key);
        Assert.Same(modifier, meter.Modifiers[change.Key.Value]);
        Assert.Equal(5, meter.Derivative);

        //The meter's rate is exact, so nothing of its own occurs and the next thing is the remove
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        Assert.True(change.IsFinished);
        Assert.Empty(meter.Modifiers);
        Assert.Equal(0, meter.Derivative);
        Assert.Equal(30, meter.CurrentValue); //6 ticks of 5, the last landing as the modifier is removed
    }
    [Fact(DisplayName = "A TempRMMChange leaves the meter's other modifiers alone when it is removed")]
    public void ModInstanceRemovesOnlyItsOwn()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        ModifierKey permanent = meter.Add(new ModInstance<int, int>(5, 1));
        //The same modifier again, so the only thing telling them apart is the key
        TempRMMChange<int, int> change = new(meter, new ModInstance<int, int>(5, 1), 2, 6, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);
        timeline.AddEvent(change);

        while (!change.IsFinished)
        {
            foreach (StepReport<int> _ in timeline.TakeSteps()) { }
            timeline.SetOpen();
        }

        Assert.Single(meter.Modifiers);
        Assert.True(meter.Modifiers.ContainsKey(permanent));
        Assert.Equal(5, meter.Derivative);
        Assert.Equal(70, meter.CurrentValue); //8 ticks of 5 from the permanent one, and 6 more of 5 while the temporary one was there
    }
    [Fact(DisplayName = "A clone of an applied TempRMMChange removes the modifier from the cloned meter and leaves the original alone")]
    public void ModInstanceCloneActsOnTheClonedMeter()
    {
        Timeline<int> timeline = new();
        RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
        TempRMMChange<int, int> change = new(meter, new ModInstance<int, int>(5, 1), 2, 6, PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, timeline);
        timeline.AddEvent(change);
        foreach (StepReport<int> _ in timeline.TakeSteps()) { }
        timeline.SetOpen();

        CloneContext context = new();
        Timeline<int> timelineClone = (Timeline<int>)timeline.Clone(context);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet(meter, out RateManagedMeter<int, int> meterClone)); //Cloning the change brought its meter along
        Assert.True(context.TryGet(change, out TempRMMChange<int, int> changeClone));
        Assert.Same(meterClone, changeClone.MeterAppliedTo);
        Assert.Equal(change.Key, changeClone.Key);

        foreach (StepReport<int> _ in timelineClone.TakeSteps()) { }

        Assert.True(changeClone.IsFinished);
        Assert.Empty(meterClone.Modifiers);
        Assert.Equal(30, meterClone.CurrentValue);
        Assert.False(change.IsFinished);
        Assert.Single(meter.Modifiers);
        Assert.Equal(0, meter.CurrentValue);
    }
    #endregion
}
