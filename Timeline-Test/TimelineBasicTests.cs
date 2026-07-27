using CoreMetrics;
using Timeline;
namespace Timeline_Test;

/// <summary>
/// Used to test everything in the Timeline class that's not related to TakeSteps, since TakeSteps is a complex function that has its own set of tests in TimelineTakeStepsTests.cs
/// </summary>
public class TimelineBasicTests
{
    [Fact(DisplayName = "Timeline constructor starts in Open phase and not termination reason")]
    public void Test0()
    {
        Timeline<int> timeline = new();
        Assert.Equal(TimelinePhase.Open, timeline.Phase);
        Assert.Equal(TerminationType.NotTerminated, timeline.TerminationReason);
    }
    #region Event adding/removing
    [Fact(DisplayName = "Adding an event adds it to Events if time is not Never")]
    public void Test1()
    {
        Timeline<int> timeline = new();
        BaseCard<int> card = new(1, timeline);
        timeline.AddEvent(card);
        Assert.Contains(card, timeline.Events);
        Assert.DoesNotContain(card, timeline.PossibleEvents);
    }
    [Fact(DisplayName = "Adding an event adds it to PossibleEvents if time is Never")]
    public void Test2()
    {
        Timeline<int> timeline = new();
        TimeOrNever<int> neverTime = new();
        { neverTime.IsNever = true; }
        PassiveDynamicEvent<int> nonOccuringEvent = new(neverTime, false, timeline); //Using the two argument constructor would automatically use AddPossibleEvent, so we use the three argument constructor to avoid that
        timeline.AddEvent(nonOccuringEvent);
        Assert.Contains(nonOccuringEvent, timeline.PossibleEvents);
        Assert.DoesNotContain(nonOccuringEvent, timeline.Events);
    }
    [Fact(DisplayName = "AddPossibleEvent always adds to PossibleEvents, even if the event has a time")]
    public void Test3()
    {
        Timeline<int> timeline = new();
        BaseCard<int> card = new(1, timeline);
        PassiveDynamicEvent<int> autoAddedNever = new(false, timeline); //Use the two argument constructor to automatically add it to PossibleEvents
        timeline.AddPossibleEvent(card);
        Assert.Contains(card, timeline.PossibleEvents);
        Assert.Contains(autoAddedNever, timeline.PossibleEvents);
        Assert.DoesNotContain(card, timeline.Events);
        Assert.DoesNotContain(autoAddedNever, timeline.Events);
    }
    [Fact(DisplayName = "Removing an event removes it from either Events or PossibleEvents")]
    public void Test4()
    {
        Timeline<int> timeline = new();
        BaseCard<int> card = new(1, timeline);
        timeline.AddEvent(card); //Prior tests have already verified that AddEvent works correctly, so we can use it here to set up the test
        Assert.True(timeline.RemoveEvent(card));
        Assert.DoesNotContain(card, timeline.Events);
        Assert.DoesNotContain(card, timeline.PossibleEvents);
        PassiveDynamicEvent<int> nonOccuringEvent = new(false, timeline);
        Assert.True(timeline.RemoveEvent(nonOccuringEvent));
        Assert.DoesNotContain(nonOccuringEvent, timeline.Events);
        Assert.DoesNotContain(nonOccuringEvent, timeline.PossibleEvents);
    }
    [Fact(DisplayName = "Removing an event that isn't in either Events or PossibleEvents returns false")]
    public void Test5() { 
        Timeline<int> timeline = new();
        BaseCard<int> card = new(1, timeline);
        Assert.False(timeline.RemoveEvent(card));
    }
    [Fact(DisplayName = "AddEvent throws an error if the TimelinePhase isn't Open")]
    public void Test6()
    {
        Timeline<int> timeline = new();
        BaseCard<int> card = new(1, timeline);
        for (TimelinePhase phase = TimelinePhase.Purged; phase <= TimelinePhase.Terminated; phase++) //Start after Open, since Open is the only phase that allows adding events
        {
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(phase)); //Should throw an error always. Make sure of that
            Assert.Throws<InvalidPhaseForActionException>(() => timeline.AddEvent(card));
            Assert.DoesNotContain(card, timeline.Events);
            Assert.DoesNotContain(card, timeline.PossibleEvents);
        }
    }
    [Fact(DisplayName = "AddPossibleEvent throws an error if the TimelinePhase isn't Open or InstantAction")]
    public void Test7()
    {
        Timeline<int> timeline = new();
        BaseCard<int> card = new(1, timeline);
        for (TimelinePhase phase = TimelinePhase.Purged; phase <= TimelinePhase.Terminated; phase++) //Start after Open
        { 
            if (phase == TimelinePhase.InstantAction) { continue; } //Skip InstantAction, since that phase allows adding possible events
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(phase)); //Should throw an error always. Make sure of that
            Assert.Throws<InvalidPhaseForActionException>(() => timeline.AddPossibleEvent(card));
            Assert.DoesNotContain(card, timeline.Events); //This should never happen, since AddPossibleEvent should never add to Events, but best to be safe and check
            Assert.DoesNotContain(card, timeline.PossibleEvents);
        }
    }
    [Fact(DisplayName = "AddPossibleEvent works as normal if the TimelinePhase is InstantAction")]
    public void Test8()
    {
        Timeline<int> timeline = new();
        Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.InstantAction));
        //The rest is from Test3, since we want to verify that AddPossibleEvent works as normal in InstantAction phase
        BaseCard<int> card = new(1, timeline);
        PassiveDynamicEvent<int> autoAddedNever = new(false, timeline); //Use the two argument constructor to automatically add it to PossibleEvents
        timeline.AddPossibleEvent(card);
        Assert.Contains(card, timeline.PossibleEvents);
        Assert.Contains(autoAddedNever, timeline.PossibleEvents);
        Assert.DoesNotContain(card, timeline.Events);
        Assert.DoesNotContain(autoAddedNever, timeline.Events);
    }
    [Fact(DisplayName = "Removing an event throws an error if the TimelinePhase isn't Open or InstantAction")]
    public void Test9()
    {
        Timeline<int> timeline = new();
        BaseCard<int> card = new(1, timeline);
        timeline.AddEvent(card);
        for (TimelinePhase phase = TimelinePhase.Purged; phase <= TimelinePhase.Terminated; phase++) //Start after Open
        {
            if (phase == TimelinePhase.InstantAction) { continue; } //Skip InstantAction, since that phase allows removing events
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(phase)); //Should throw an error always. Make sure of that
            Assert.Throws<InvalidPhaseForActionException>(() => timeline.RemoveEvent(card));
            Assert.Contains(card, timeline.Events); //The event should still be in Events, since the removal should have failed
            Assert.DoesNotContain(card, timeline.PossibleEvents);
        }
    }
    [Fact(DisplayName = "Removing an event only removes from PossibleEvents if the TimelinePhase is InstantAction")]
    public void Test10()
    {
        Timeline<int> timeline = new();
        BaseCard<int> card = new(1, timeline);
        PassiveDynamicEvent<int> autoAddedNever = new(false, timeline);
        timeline.AddEvent(card);
        Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.InstantAction));
        Assert.False(timeline.RemoveEvent(card));
        Assert.Contains(card, timeline.Events);
        Assert.DoesNotContain(card, timeline.PossibleEvents);
        Assert.True(timeline.RemoveEvent(autoAddedNever));
        Assert.DoesNotContain(autoAddedNever, timeline.Events);
        Assert.DoesNotContain(autoAddedNever, timeline.PossibleEvents);
    }
    #endregion
    #region Authorized phase change functions
    [Fact(DisplayName = "Terminate throws InvalidPhaseForActionException when the phase is Terminated.")]
    public void Test11()
    {
        Timeline<int> timeline = new();
        Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.Terminated));
        Assert.Throws<InvalidPhaseForActionException>(() => timeline.Terminate(TerminationType.UnknownEnding));
    }
    [Fact(DisplayName = "Terminate throws ArgumentException when the termination type is NotTerminated.")]
    public void Test12()
    {
        Timeline<int> timeline = new();
        Assert.Throws<ArgumentException>(() => timeline.Terminate(TerminationType.NotTerminated));
    }
    [Fact(DisplayName = "Terminate sets the phase to Terminated and sets the termination reason.")]
    public void Test13()
    {
        Timeline<int> timeline = new();
        for (TimelinePhase phase = TimelinePhase.Open; phase < TimelinePhase.Terminated; phase++) 
        {
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(phase)); //Should throw an error always. Make sure of that
            for (TerminationType terminationType = TerminationType.Escape; terminationType <= TerminationType.UnknownEnding; terminationType++) //Check all termination types except NotTerminated, since NotTerminated is already tested in Test12
            {
                timeline.Terminate(terminationType);
                Assert.Equal(TimelinePhase.Terminated, timeline.Phase);
                Assert.Equal(terminationType, timeline.TerminationReason);
                Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(phase)); //Reset after each termination
            }
        }
    }
    [Fact(DisplayName = "SetOpen throws InvalidOperationException if the phase is not Display.")]
    public void Test14()
    {
        Timeline<int> timeline = new();
        for (TimelinePhase phase = TimelinePhase.Open; phase <= TimelinePhase.Terminated; phase++)
        {
            if (phase == TimelinePhase.Display) { continue; } //Skip Display, since that phase allows SetOpen
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(phase)); //Should throw an error always. Make sure of that
            Assert.Throws<InvalidPhaseForActionException>(() => timeline.SetOpen());
        }
    }
    [Fact(DisplayName = "SetOpen sets the phase to Open if the phase is Display.")]
    public void Test15()
    {
        Timeline<int> timeline = new();
        Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.Display));
        timeline.SetOpen();
        Assert.Equal(TimelinePhase.Open, timeline.Phase);
    }
    #endregion
}
