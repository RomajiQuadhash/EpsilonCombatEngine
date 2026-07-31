using CoreMetrics;
using System.Numerics;
using Timeline;
using System.Threading.Tasks;

namespace Timeline_Test
{
    /// <summary>
    /// All tests related to the TakeSteps method of the Timeline class.
    /// This assumes that all tests in TimelineBasicTests have passed, since TakeSteps needs events and phases to work properly.
    /// </summary>
    public class TimelineTakeStepsTests
    {
        [Fact(DisplayName = "TakeSteps instantly returns if the phase is Terminated or Display")]
        public void InstantReturnOnTerminatedOrDisplay()
        {
            Timeline<int> timeline = new();
            int stepsTaken = 0;
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.Terminated));
            foreach (var item in timeline.TakeSteps())
            {
                stepsTaken++;
            }
            Assert.Equal(0, stepsTaken);
            //Now test Display phase
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.Display));
            foreach (var item in timeline.TakeSteps())
            {
                stepsTaken++;
            }
            Assert.Equal(0, stepsTaken);
        }

        #region Termination Checks
        [Fact(DisplayName = "If there are no events, Advancing from Open should terminate and set the termination reason to NoEvents")]
        public void TerminateOnNoEvents()
        {
            Timeline<int> timeline = new();
            int stepsTaken = 0;
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                //Check the StepReport properties. Since it will just run one step, we don't need to check which step it is, we can just check the properties directly.
                Assert.Equal(TimelinePhase.Open, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.Terminated, stepReport.FinalPhase);
                Assert.Equal(0, stepReport.AdvanceAmount);
                Assert.Equal([], stepReport.EventBackup);
                Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup);
                Assert.Null(stepReport.OccurredEvent);
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(1, stepsTaken);
            Assert.Equal(TimelinePhase.Terminated, timeline.Phase);
            Assert.Equal(TerminationType.NoEvents, timeline.TerminationReason);
        }
        [Fact(DisplayName = "If all events are never and can't occur, Advancing from Open will purge all events and terminate with NoEvents")]
        public void TerminateOnAllNeverPurgeable()
        {
            Timeline<int> timeline = new();
            TimeOrNever<int> neverTime = new() { IsNever = true };
            //Add a few events that are never and can't occur
            PassiveDynamicEvent<int> eventThatBecomesNever = new(new TimeOrNever<int>(), false, timeline);
            timeline.AddEvent(eventThatBecomesNever); //Add it this way so it is in Events instead of PossibleEvents
            eventThatBecomesNever.TimeRemaining = neverTime;
            _ = new PassiveDynamicEvent<int>(false, timeline); //This auto adds itself to PossibleEvents, so we don't need to add it manually

            //Now, the timeline has two events, one in Events and one in PossibleEvents, both of which are never and can't occur. Advancing should purge them and terminate with NoEvents.
            int stepsTaken = 0;
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                //Check the StepReport properties. Since it will just run one step, we don't need to check which step it is, we can just check the properties directly.
                Assert.Equal(TimelinePhase.Open, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.Terminated, stepReport.FinalPhase);
                Assert.Equal(0, stepReport.AdvanceAmount);
                Assert.Equal([], stepReport.EventBackup);
                Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup);
                Assert.Null(stepReport.OccurredEvent);
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(1, stepsTaken);
            Assert.Equal(TimelinePhase.Terminated, timeline.Phase);
            Assert.Equal(TerminationType.NoEvents, timeline.TerminationReason);
        }
        [Fact(DisplayName = "If all events are never and can occur, Advancing from Open will move/leave the events in PossibleEvents and terminate with NoEvents")]
        public void TerminateOnAllNeverUnpurgeable()
        {
            Timeline<int> timeline = new();
            TimeOrNever<int> neverTime = new() { IsNever = true };
            //Add a few events that are never and can occur
            PassiveDynamicEvent<int> eventThatBecomesNever = new(new TimeOrNever<int>(), true, timeline);
            timeline.AddEvent(eventThatBecomesNever); //Add it this way so it is in Events instead of PossibleEvents
            eventThatBecomesNever.TimeRemaining = neverTime;
            PassiveDynamicEvent<int> eventThatStartsAsNever = new(true, timeline); //This auto adds itself to PossibleEvents, so we don't need to add it manually
            //Now, the timeline has two events, one in Events and one in PossibleEvents, both of which are never and can occur. Advancing should move them to PossibleEvents and terminate with NoEvents.
            HashSet<Okazo<int>> expectedPossibleEvents = [eventThatBecomesNever, eventThatStartsAsNever];
            int stepsTaken = 0;
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                //Check the StepReport properties. Since it will just run one step, we don't need to check which step it is, we can just check the properties directly.
                Assert.Equal(TimelinePhase.Open, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.Terminated, stepReport.FinalPhase);
                Assert.Equal(0, stepReport.AdvanceAmount);
                Assert.Equal([], stepReport.EventBackup);
                Assert.Equal(expectedPossibleEvents, stepReport.PossibleEventBackup);
                Assert.Null(stepReport.OccurredEvent);
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(1, stepsTaken);
            Assert.Equal(TimelinePhase.Terminated, timeline.Phase);
            Assert.Equal(TerminationType.NoEvents, timeline.TerminationReason);
        }
        #endregion
        #region Open -> Purged
        [Fact(DisplayName ="If there's at least one valid event in Events, timeline phase is changed to Purged and keeps going")]
        public void OpenToPurgedOnValidEvent()
        {
            Timeline<int> timeline = new();
            //Add a valid event to the timeline
            BaseCard<int> validEvent = new(5, timeline);
            timeline.AddEvent(validEvent);
            int stepsTaken = 0;
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken==2)
                {
                    break; //We want to stop once we hit the second step, since we're only testing the first step here.
                }
                //Check the StepReport properties. Since it will just run one step, we don't need to check which step it is, we can just check the properties directly.
                Assert.Equal(TimelinePhase.Open, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.Purged, stepReport.FinalPhase);
                Assert.Equal(0, stepReport.AdvanceAmount);
                Assert.Equal([validEvent], stepReport.EventBackup);
                Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup);
                Assert.Null(stepReport.OccurredEvent);
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(2, stepsTaken);
            Assert.Equal(TimelinePhase.Sorted, timeline.Phase); //We have taken two steps, so the timeline should now be in the Sorted phase.
        }
        [Fact(DisplayName = "If a valid event is in PossibleEvents, it is promoted to Events and the timeline progresses to Purged")]
        public void OpenToPurgedOnValidPossibleEvent()
        {
            Timeline<int> timeline = new();
            //Add a valid event to the timeline's PossibleEvents
            BaseCard<int> validPossibleEvent = new(5, timeline);
            timeline.AddPossibleEvent(validPossibleEvent);
            int stepsTaken = 0;
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 2)
                {
                    break; //We want to stop once we hit the second step, since we're only testing the first step here.
                }
                //Check the StepReport properties. Since it will just run one step, we don't need to check which step it is, we can just check the properties directly.
                Assert.Equal(TimelinePhase.Open, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.Purged, stepReport.FinalPhase);
                Assert.Equal(0, stepReport.AdvanceAmount);
                Assert.Equal([validPossibleEvent], stepReport.EventBackup);
                Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup);
                Assert.Null(stepReport.OccurredEvent);
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(2, stepsTaken);
            Assert.Equal(TimelinePhase.Sorted, timeline.Phase);//We have taken two steps, so the timeline should now be in the Sorted phase.
        }

        [Fact(DisplayName = "If a valid event is in Events and a valid event is in PossibleEvents, both are kept and the timeline progresses to Purged")]
        public void OpenToPurgedOnValidEventAndPossibleEvent()
        {
            Timeline<int> timeline = new();
            //Add a valid event to the timeline's Events
            BaseCard<int> validEvent = new(5, timeline);
            timeline.AddEvent(validEvent);
            //Add a valid event to the timeline's PossibleEvents
            BaseCard<int> validPossibleEvent = new(1, timeline);
            timeline.AddPossibleEvent(validPossibleEvent);
            int stepsTaken = 0;
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 2)
                {
                    break; //We want to stop once we hit the second step, since we're only testing the first step here.
                }
                //Check the StepReport properties. Since it will just run one step, we don't need to check which step it is, we can just check the properties directly.
                Assert.Equal(TimelinePhase.Open, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.Purged, stepReport.FinalPhase);
                Assert.Equal(0, stepReport.AdvanceAmount);
                Assert.Equal([validEvent, validPossibleEvent], stepReport.EventBackup); //Both events will happen, and they won't be sorted yet, so possible events will be at the back of the list.
                Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup);
                Assert.Null(stepReport.OccurredEvent);
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(2, stepsTaken);
            Assert.Equal(TimelinePhase.Sorted, timeline.Phase);//We have taken two steps, so the timeline should now be in the Sorted phase.
        }
        [Fact(DisplayName = "If a mix of valid and invalid events are in PossibleEvents, only the valid ones are promoted to Events and the timeline progresses to Purged")]
        public void AddValidEventAndPossibleEvent()
        {
            Timeline<int> timeline = new();
            //Add a valid event to the timeline's PossibleEvents
            BaseCard<int> validPossibleEvent = new(1, timeline);
            timeline.AddPossibleEvent(validPossibleEvent);
            //Add an invalid event to the timeline's PossibleEvents
            _ = new PassiveDynamicEvent<int>(false, timeline); //This auto adds itself to PossibleEvents, so we don't need to add it manually
            int stepsTaken = 0;
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 2)
                {
                    break; //We want to stop once we hit the second step, since we're only testing the first step here.
                }
                //Check the StepReport properties. Since it will just run one step, we don't need to check which step it is, we can just check the properties directly.
                Assert.Equal(TimelinePhase.Open, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.Purged, stepReport.FinalPhase);
                Assert.Equal(0, stepReport.AdvanceAmount);
                Assert.Equal([validPossibleEvent], stepReport.EventBackup); //Only the valid event should be promoted to Events.
                Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup);
                Assert.Null(stepReport.OccurredEvent);
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(2, stepsTaken);
            Assert.Equal(TimelinePhase.Sorted, timeline.Phase);//We have taken two steps, so the timeline should now be in the Sorted phase.
        }
        [Fact(DisplayName = "If a mix of valid and invalid events are in Events, only the valid ones are kept and the timeline progresses to Purged")]
        public void AddValidEventAndInvalidEvents()
        {
            Timeline<int> timeline = new();
            //Add a valid event to the timeline's Events
            BaseCard<int> validEvent = new(5, timeline);
            timeline.AddEvent(validEvent);
            //Add an invalid event to the timeline's Events
            PassiveDynamicEvent<int> invalidEvent = new(new TimeOrNever<int>() { IsNever=true}, true, timeline); //This one can occur, but is never, so it should be in PossibleEvents after the purge.
            timeline.AddEvent(invalidEvent);
            _ = new PassiveDynamicEvent<int>(false, timeline); //This one can't occur, so it should be purged.
            int stepsTaken = 0;
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 2)
                {
                    break; //We want to stop once we hit the second step, since we're only testing the first step here.
                }
                //Check the StepReport properties. Since it will just run one step, we don't need to check which step it is, we can just check the properties directly.
                Assert.Equal(TimelinePhase.Open, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.Purged, stepReport.FinalPhase);
                Assert.Equal(0, stepReport.AdvanceAmount);
                Assert.Equal([validEvent], stepReport.EventBackup); //Only the valid event should remain in Events.
                Assert.Equal(new HashSet<Okazo<int>>() {invalidEvent}, stepReport.PossibleEventBackup);
                Assert.Null(stepReport.OccurredEvent);
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(2, stepsTaken);
            Assert.Equal(TimelinePhase.Sorted, timeline.Phase);//We have taken two steps, so the timeline should now be in the Sorted phase.
        }
        [Fact(DisplayName = "Multiple events that are valid remain, even after purging invalid ones")]
        public void MultipleValidEventsRemainAfterPurge()
        {
            Timeline<int> timeline = new();
            //Add multiple valid and invalid events to the timeline's Events
            BaseCard<int> validEvent1 = new(5, timeline);
            PassiveDynamicEvent<int> invalidEvent1 = new(new TimeOrNever<int>(3), true, timeline); //We'll make this one never after adding it to the timeline.
            BaseCard<int> validEvent2 = new(10, timeline);
            timeline.AddEvent(validEvent1);
            timeline.AddEvent(invalidEvent1);
            timeline.AddEvent(validEvent2);

            invalidEvent1.TimeRemaining = new TimeOrNever<int>() { IsNever = true }; //Make this event never, so it should be purged.
            int stepsTaken = 0;
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 2)
                {
                    break; //We want to stop once we hit the second step, since we're only testing the first step here.
                }
                //Check the StepReport properties. Since it will just run one step, we don't need to check which step it is, we can just check the properties directly.
                Assert.Equal(TimelinePhase.Open, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.Purged, stepReport.FinalPhase);
                Assert.Equal(0, stepReport.AdvanceAmount);
                Assert.Equal([validEvent1, validEvent2], stepReport.EventBackup); //Both valid events should remain in Events.
                Assert.Equal(new HashSet<Okazo<int>>() { invalidEvent1}, stepReport.PossibleEventBackup);
                Assert.Null(stepReport.OccurredEvent);
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(2, stepsTaken);
            Assert.Equal(TimelinePhase.Sorted, timeline.Phase);//We have taken two steps, so the timeline should now be in the Sorted phase.
        }
        #endregion
        #region Purged -> Sorted
        [Fact(DisplayName ="No matter the order they are added, the events are soonest to latest once the step completes")]
        public void PurgedToSortedEventsAreSorted()
        {
            Timeline<int> timeline = new();
            //Add multiple valid events to the timeline's Events in a random order
            BaseCard<int> event2 = new(10, timeline);
            BaseCard<int> event1 = new(5, timeline);
            BaseCard<int> event3 = new(15, timeline);
            timeline.AddEvent(event2);
            timeline.AddEvent(event1);
            timeline.AddEvent(event3);

            //Artificially set the timeline phase to Purged, so we can test the sorting step.
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.Purged));
            int stepsTaken = 1; //we skipped the first step, since we are testing the second step here.
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 3)
                {
                    break; //We want to stop once we hit the third step, since we're testing the second step here.
                }
                Assert.Equal(TimelinePhase.Purged, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.Sorted, stepReport.FinalPhase);
                Assert.Equal(0, stepReport.AdvanceAmount);
                Assert.Equal([event1, event2, event3], stepReport.EventBackup); //Events should be sorted from soonest to latest.
                Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup);
                Assert.Null(stepReport.OccurredEvent);
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(3, stepsTaken);
            Assert.Equal(TimelinePhase.Zeroed, timeline.Phase);//We have taken three steps, so the timeline should now be in the Zeroed phase.
        }
        [Fact(DisplayName ="Possible Events are not affected during this phase")]
        public void PurgedToSortedPossibleEventsUnaffected()
        {
            Timeline<int> timeline = new();
            //Add a valid event to the timeline's PossibleEvents
            BaseCard<int> validPossibleEvent = new(1, timeline);
            timeline.AddPossibleEvent(validPossibleEvent);
            //There needs to be at least one valid event in Events for the timeline to progress to Purged, so we'll add one
            BaseCard<int> validEvent = new(5, timeline);
            timeline.AddEvent(validEvent);
            //Artificially set the timeline phase to Purged, so we can test the sorting step.
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.Purged));
            int stepsTaken = 1; //we skipped the first step, since we are testing the second step here.
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 3)
                {
                    break; //We want to stop once we hit the third step, since we're testing the second step here.
                }
                Assert.Equal(TimelinePhase.Purged, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.Sorted, stepReport.FinalPhase);
                Assert.Equal(0, stepReport.AdvanceAmount);
                Assert.Equal([validEvent], stepReport.EventBackup);
                Assert.Equal(new HashSet<Okazo<int>>() { validPossibleEvent }, stepReport.PossibleEventBackup); //Possible events should remain unaffected.
                Assert.Null(stepReport.OccurredEvent);
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(3, stepsTaken);
            Assert.Equal(TimelinePhase.Zeroed, timeline.Phase);//We have taken three steps, so the timeline should now be in the Zeroed phase.
        }
        #endregion
        #region Sorted -> Zeroed
        [Fact(DisplayName ="If a timeline has only one event, that event is advanced to time 0, and that advancement is saved on the Step Report")]
        public void SortedToZeroedSingleEvent()
        {
            Timeline<int> timeline = new();
            //Add a valid event to the timeline's Events
            BaseCard<int> validEvent = new(5, timeline);
            timeline.AddEvent(validEvent);
            //Artificially set the timeline phase to Sorted, so we can test the zeroing step.
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.Sorted));
            int stepsTaken = 2; //we skipped the first two steps, since we are testing the third step here.
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 4)
                {
                    break; //We want to stop once we hit the fourth step, since we're testing the third step here.
                }
                Assert.Equal(TimelinePhase.Sorted, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.Zeroed, stepReport.FinalPhase);
                Assert.Equal(5, stepReport.AdvanceAmount); //The event should have been advanced by 5 to reach time 0.
                Assert.Equal(0, validEvent.TimeRemaining.Time); //The event should now be at time 0.
                Assert.Equal([validEvent], stepReport.EventBackup);
                Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup);
                Assert.Null(stepReport.OccurredEvent);
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(4, stepsTaken);
            Assert.Equal(TimelinePhase.PostEffect, timeline.Phase);//We have taken four steps, so the timeline should now be in the PostEffect phase.
        }
        [Fact(DisplayName = "All events are shifted by the amount needed to make the first event 0, even possible events. Never events don't have a time, so they don't change")]
        public void SortedToZeroedMultipleEvents()
        {
            Timeline<int> timeline = new();
            //Add multiple valid events to the timeline's Events
            BaseCard<int> event1 = new(5, timeline);
            BaseCard<int> event2 = new(12, timeline); //Make these times weird so we can be sure it isn't just swapping values
            BaseCard<int> event3 = new(15, timeline);
            timeline.AddEvent(event1);
            timeline.AddEvent(event2);
            timeline.AddEvent(event3);
            //Add a valid event to the timeline's PossibleEvents
            BaseCard<int> possibleEvent = new(26, timeline);
            timeline.AddPossibleEvent(possibleEvent);
            PassiveDynamicEvent<int> neverEvent = new( true, timeline); //This event is never, so it should not be affected by the zeroing step. Use Autoadd constructor to add it to PossibleEvents.
            //Artificially set the timeline phase to Sorted, so we can test the zeroing step.
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.Sorted));
            int stepsTaken = 2; //we skipped the first two steps, since we are testing the third step here.
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 4)
                {
                    break; //We want to stop once we hit the fourth step, since we're testing the third step here.
                }
                Assert.Equal(TimelinePhase.Sorted, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.Zeroed, stepReport.FinalPhase);
                Assert.Equal(5, stepReport.AdvanceAmount); //The events should have been advanced by 5 to make the first event 0.
                Assert.Equal(0, event1.TimeRemaining.Time); //The first event should now be at time 0.
                Assert.Equal(7, event2.TimeRemaining.Time); //The second event should now be at time 7.
                Assert.Equal(10, event3.TimeRemaining.Time); //The third event should now be at time 10.
                Assert.Equal(21, possibleEvent.TimeRemaining.Time); //The possible event should now be at time 21.
                Assert.True(neverEvent.TimeRemaining.IsNever); //The never event should still be never.
                Assert.Equal([event1, event2, event3], stepReport.EventBackup);
                Assert.Equal(new HashSet<Okazo<int>>() { possibleEvent, neverEvent }, stepReport.PossibleEventBackup);
                Assert.Null(stepReport.OccurredEvent);
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(4, stepsTaken);
            Assert.Equal(TimelinePhase.PostEffect, timeline.Phase);//We have taken four steps, so the timeline should now be in PostEffect
        }
        #endregion
        #region Zeroed -> PostEffect (without Instant Actions)
        [Fact(DisplayName ="If there is one event (which will be at time zero) it will be triggered and removed from the events list")]
        public void ZeroedToPostEffectSingleEvent()
        {
            Timeline<int> timeline = new();
            //Add a valid event to the timeline's Events
            BaseCard<int> validEvent = new(0, timeline); //This event is already at time 0, so it should be triggered immediately.
            timeline.AddEvent(validEvent);

            MostRecentEventHolder<int> mostRecentEventHolder = new();
            validEvent.Occurring += mostRecentEventHolder.OnTrigger; //Subscribe to the event's Occurring event so we can check if it was triggered.
            //Artificially set the timeline phase to Zeroed, so we can test the post effect step.
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.Zeroed));
            int stepsTaken = 3; //we skipped the first three steps, since we are testing the fourth step here.
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 5)
                {
                    break; //We want to stop once we hit the fifth step, since we're testing the fourth step here.
                }
                Assert.Equal(TimelinePhase.Zeroed, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.PostEffect, stepReport.FinalPhase);
                Assert.Equal(0, stepReport.AdvanceAmount); //No time should have been advanced during this step.
                Assert.Equal([], stepReport.EventBackup); //The event should have been removed from the events list after being triggered.
                Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup);
                Assert.Equal(validEvent, stepReport.OccurredEvent); //The event should have been reported as occurred.
                Assert.Equal(validEvent, mostRecentEventHolder.MostRecentEvent); //The event should have been triggered.
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(5, stepsTaken);
            Assert.Equal(TimelinePhase.Display, timeline.Phase);//We have taken five steps, so the timeline should now be in Display phase since there are no more events to process.
        }
        [Fact(DisplayName = "If there are multiple events in the Events list, they are preserved")]
        public void ZeroedToPostEffectMultipleEvents()
        {
            Timeline<int> timeline = new();
            //Add multiple valid events to the timeline's Events
            BaseCard<int> event1 = new(0, timeline); //This event is the first in the list
            BaseCard<int> event2 = new(0, timeline); //This event is second in the list, so it will NOT trigger, even though it is at time 0, since each iteration of the TakeSteps method only triggers one event at a time.
            timeline.AddEvent(event1);
            timeline.AddEvent(event2);
            MostRecentEventHolder<int> mostRecentEventHolder = new();
            event1.Occurring += mostRecentEventHolder.OnTrigger; //Subscribe to the event's Occurring event so we can check if it was triggered.
            event2.Occurring += mostRecentEventHolder.OnTrigger; //Subscribe to the event's Occurring event so we can check if it was triggered.
            //Artificially set the timeline phase to Zeroed, so we can test the post effect step.
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.Zeroed));
            int stepsTaken = 3; //we skipped the first three steps, since we are testing the fourth step here.
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 5)
                {
                    break; //We want to stop once we hit the fifth step, since we're testing the fourth step here.
                }
                Assert.Equal(TimelinePhase.Zeroed, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.PostEffect, stepReport.FinalPhase);
                Assert.Equal(0, stepReport.AdvanceAmount); //No time should have been advanced during this step.
                Assert.Equal([event2], stepReport.EventBackup); //The second event should still be in the events list, since only one event is triggered per step.
                Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup);
                Assert.Equal(event1, stepReport.OccurredEvent);
                Assert.Equal(event1, mostRecentEventHolder.MostRecentEvent); 
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(5, stepsTaken);
            Assert.Equal(TimelinePhase.Display, timeline.Phase);//We have taken five steps, so the timeline
        }
        [Fact(DisplayName = "If there are events in PossibleEvents (that occur never/after 0) they are kept and sent to Display phase")]
        public void ZeroedToPostEffectPossibleEventsKept()
        {
            Timeline<int> timeline = new();
            BaseCard<int> occurredEvent = new(0, timeline); //This event is already at time 0, so it should be triggered immediately.
            timeline.AddEvent(occurredEvent);
            //Add a valid event to the timeline's PossibleEvents
            BaseCard<int> possibleEvent = new(5, timeline);
            timeline.AddPossibleEvent(possibleEvent);

            MostRecentEventHolder<int> mostRecentEventHolder = new();
            occurredEvent.Occurring += mostRecentEventHolder.OnTrigger;
            possibleEvent.Occurring += mostRecentEventHolder.OnTrigger; 
            //Artificially set the timeline phase to Zeroed, so we can test the post effect step.
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.Zeroed));
            int stepsTaken = 3; //we skipped the first three steps, since we are testing the fourth step here.
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 5)
                {
                    break; //We want to stop once we hit the fifth step, since we're testing the fourth step here.
                }
                Assert.Equal(TimelinePhase.Zeroed, stepReport.InitialPhase);
                Assert.Equal(TimelinePhase.PostEffect, stepReport.FinalPhase);
                Assert.Equal(0, stepReport.AdvanceAmount); //No time should have been advanced during this step.
                Assert.Equal([], stepReport.EventBackup); //There are no events in Events list, so it should be empty.
                Assert.Equal(new HashSet<Okazo<int>>() { possibleEvent }, stepReport.PossibleEventBackup); //The possible event should still be in the PossibleEvents list.
                Assert.Equal(occurredEvent, stepReport.OccurredEvent);
                Assert.Equal(occurredEvent, mostRecentEventHolder.MostRecentEvent);
                Assert.Null(stepReport.ReactionEvent);
            }
            Assert.Equal(5, stepsTaken);
            Assert.Equal(TimelinePhase.Display, timeline.Phase);//We have taken five steps, so the timeline should now be in Display phase since there are no more events to process.
        }
        #endregion
        #region InstantAction "loop" tests
        //This region will be filled with making sure Instant Actions occur appropriately, in an appropriate sequence, and don't trigger anything else
        //This will test both that the events are triggered in the correct order and that multiple events are only all triggered if they occur concurrently.
        [Fact(DisplayName ="If there's one PossibleEvent and it has time remaining 0, then PostEffect -> InstantAction (event is triggered) -> PostEffect -> Display")]
        public void InstantActionSingleEvent()
        {
            Timeline<int> timeline = new();
            BaseCard<int> possibleEvent = new(0, timeline);
            timeline.AddPossibleEvent(possibleEvent);
            MostRecentEventHolder<int> mostRecentEventHolder = new();
            possibleEvent.Occurring += mostRecentEventHolder.OnTrigger;

            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.PostEffect));
            int stepsTaken = 3;
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 4) //This is the PostEffect -> InstantAction step
                {
                    Assert.Equal(TimelinePhase.PostEffect, stepReport.InitialPhase);
                    Assert.Equal(TimelinePhase.InstantAction, stepReport.FinalPhase);
                    Assert.Equal(0, stepReport.AdvanceAmount);
                    Assert.Equal([], stepReport.EventBackup);
                    Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup); //The possible event should be removed since it is queued to be triggered in the InstantAction phase.
                    Assert.Null(stepReport.OccurredEvent); //The event hasn't been triggered yet, so it should be null.
                    Assert.Null(mostRecentEventHolder.MostRecentEvent); //The event hasn't been triggered yet, so it should be null.
                    Assert.Equal(possibleEvent, stepReport.ReactionEvent); //But it should be queued up to be triggered in InstantAction phase
                }
                else if (stepsTaken == 5) //This is the InstantAction -> PostEffect step
                {
                    Assert.Equal(TimelinePhase.InstantAction, stepReport.InitialPhase);
                    Assert.Equal(TimelinePhase.PostEffect, stepReport.FinalPhase);
                    Assert.Equal(0, stepReport.AdvanceAmount);
                    Assert.Equal([], stepReport.EventBackup);
                    Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup);
                    Assert.Equal(possibleEvent, stepReport.OccurredEvent); //The event should have been triggered now.
                    Assert.Equal(possibleEvent, mostRecentEventHolder.MostRecentEvent); //The event should have been triggered now.
                    Assert.Null(stepReport.ReactionEvent);
                }
            }
            Assert.Equal(6, stepsTaken); //Should have taken 6 steps to reach Display and should break once it does
            Assert.Equal(TimelinePhase.Display, timeline.Phase);//We have taken six steps, so the timeline should now be in Display phase since there are no more events to process.
        }
        [Fact(DisplayName = "If the PossibleEvent in InstantActionSingleEvent is in the past, time is rewound in InstantAction")]
        public void InstantActionSingleEventInPast()
        {
            Timeline<int> timeline = new();
            BaseCard<int> possibleEvent = new(-3, timeline);
            timeline.AddPossibleEvent(possibleEvent);
            MostRecentEventHolder<int> mostRecentEventHolder = new();
            possibleEvent.Occurring += mostRecentEventHolder.OnTrigger;

            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.PostEffect));
            int stepsTaken = 3;
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 4) //This is the PostEffect -> InstantAction step
                {
                    Assert.Equal(TimelinePhase.PostEffect, stepReport.InitialPhase);
                    Assert.Equal(TimelinePhase.InstantAction, stepReport.FinalPhase);
                    Assert.Equal(0, stepReport.AdvanceAmount);
                    Assert.Equal([], stepReport.EventBackup);
                    Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup); //The possible event should be removed since it is queued to be triggered in the InstantAction phase.
                    Assert.Null(stepReport.OccurredEvent); //The event hasn't been triggered yet, so it should be null.
                    Assert.Null(mostRecentEventHolder.MostRecentEvent); //The event hasn't been triggered yet, so it should be null.
                    Assert.Equal(possibleEvent, stepReport.ReactionEvent); //But it should be queued up to be triggered in InstantAction phase
                }
                else if (stepsTaken == 5) //This is the InstantAction -> PostEffect step
                {
                    Assert.Equal(TimelinePhase.InstantAction, stepReport.InitialPhase);
                    Assert.Equal(TimelinePhase.PostEffect, stepReport.FinalPhase);
                    Assert.Equal(-3, stepReport.AdvanceAmount);
                    Assert.Equal([], stepReport.EventBackup);
                    Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup);
                    Assert.Equal(possibleEvent, stepReport.OccurredEvent); //The event should have been triggered now.
                    Assert.Equal(possibleEvent, mostRecentEventHolder.MostRecentEvent); //The event should have been triggered now.
                    Assert.Null(stepReport.ReactionEvent);
                }
            }
            Assert.Equal(6, stepsTaken);
            Assert.Equal(TimelinePhase.Display, timeline.Phase);//We have taken six steps, so the timeline should now be in Display phase since there are no more events to process.
        }
        [Fact(DisplayName ="If there are two possible events and one is more negative than the other, only the most negative occurs as an InstantAction")]
        public void InstantActionTwoEventsOneTrigger()
        {
            Timeline<int> timeline = new();
            BaseCard<int> earliestEvent = new(-4, timeline);
            BaseCard<int> latestEvent = new(-2, timeline);

            //Make sure it isn't the most recently added event
            timeline.AddPossibleEvent(latestEvent);
            timeline.AddPossibleEvent(earliestEvent);

            MostRecentEventHolder<int> mostRecentEventHolder = new();
            earliestEvent.Occurring += mostRecentEventHolder.OnTrigger;
            latestEvent.Occurring += mostRecentEventHolder.OnTrigger;

            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.PostEffect));
            int stepsTaken = 3;
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 4) //This is the PostEffect -> InstantAction step
                {
                    Assert.Equal(TimelinePhase.PostEffect, stepReport.InitialPhase);
                    Assert.Equal(TimelinePhase.InstantAction, stepReport.FinalPhase);
                    Assert.Equal(0, stepReport.AdvanceAmount);
                    Assert.Equal([], stepReport.EventBackup);
                    Assert.Equal(new HashSet<Okazo<int>>() { latestEvent }, stepReport.PossibleEventBackup); //The possible event should be removed since it is queued to be triggered in the InstantAction phase.
                    Assert.Null(stepReport.OccurredEvent); //The event hasn't been triggered yet, so it should be null.
                    Assert.Null(mostRecentEventHolder.MostRecentEvent); //The event hasn't been triggered yet, so it should be null.
                    Assert.Equal(earliestEvent, stepReport.ReactionEvent); //But it should be queued up to be triggered in InstantAction phase
                }
                else if (stepsTaken == 5) //This is the InstantAction -> PostEffect step
                {
                    Assert.Equal(TimelinePhase.InstantAction, stepReport.InitialPhase);
                    Assert.Equal(TimelinePhase.PostEffect, stepReport.FinalPhase);
                    Assert.Equal(-4, stepReport.AdvanceAmount);
                    Assert.Equal([], stepReport.EventBackup);
                    Assert.Equal(new HashSet<Okazo<int>>() { latestEvent }, stepReport.PossibleEventBackup);
                    Assert.Equal(earliestEvent, stepReport.OccurredEvent); //The event should have been triggered now.
                    Assert.Equal(earliestEvent, mostRecentEventHolder.MostRecentEvent); //The event should have been triggered now.
                    Assert.Null(stepReport.ReactionEvent);
                }
            }
            Assert.Equal(6, stepsTaken); //If this fails, then we did both events instead of just one
            Assert.Equal(TimelinePhase.Display, timeline.Phase);
        }
        [Fact(DisplayName ="If there are two possible events that share a time, PostEffect -> InstantAction -> PostEffect loops, doing them in sort order")]
        public void InstantActionMultipleTrigger()
        {
            Timeline<int> timeline = new();
            BaseCard<int> card1 = new(-2, timeline);
            BaseCard<int> card2 = new(-2, timeline);

            //Set UUIDs to guarentee that card1 sorts before card2
            card1.UUID = 1;
            card2.UUID = 2;

            //Just in case HashSet has some preference for earliest added, add them "backwards"
            timeline.AddPossibleEvent(card2);
            timeline.AddPossibleEvent(card1);

            MostRecentEventHolder<int> mostRecentEventHolder = new();
            card1.Occurring += mostRecentEventHolder.OnTrigger;
            card2.Occurring += mostRecentEventHolder.OnTrigger;

            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.PostEffect));
            int stepsTaken = 3;
            foreach (var stepReport in timeline.TakeSteps())
            {
                stepsTaken++;
                if (stepsTaken == 4) //First event found
                {
                    Assert.Equal(TimelinePhase.PostEffect, stepReport.InitialPhase);
                    Assert.Equal(TimelinePhase.InstantAction, stepReport.FinalPhase);
                    Assert.Equal(0, stepReport.AdvanceAmount);
                    Assert.Equal([], stepReport.EventBackup);
                    Assert.Equal(new HashSet<Okazo<int>>() { card2 }, stepReport.PossibleEventBackup); 
                    Assert.Null(stepReport.OccurredEvent); 
                    Assert.Null(mostRecentEventHolder.MostRecentEvent);
                    Assert.Equal(card1, stepReport.ReactionEvent);
                } else if (stepsTaken == 5) //First event triggered
                {
                    Assert.Equal(TimelinePhase.InstantAction, stepReport.InitialPhase);
                    Assert.Equal(TimelinePhase.PostEffect, stepReport.FinalPhase);
                    Assert.Equal(-2, stepReport.AdvanceAmount);
                    Assert.Equal([], stepReport.EventBackup);
                    Assert.Equal(new HashSet<Okazo<int>>() { card2 }, stepReport.PossibleEventBackup);
                    Assert.Equal(card1, stepReport.OccurredEvent); 
                    Assert.Equal(card1, mostRecentEventHolder.MostRecentEvent);
                    Assert.Null(stepReport.ReactionEvent);
                } else if (stepsTaken == 6) //Second event found
                {
                    Assert.Equal(TimelinePhase.PostEffect, stepReport.InitialPhase);
                    Assert.Equal(TimelinePhase.InstantAction, stepReport.FinalPhase);
                    Assert.Equal(0, stepReport.AdvanceAmount);
                    Assert.Equal([], stepReport.EventBackup);
                    Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup);
                    Assert.Null(stepReport.OccurredEvent);
                    Assert.Equal(card1, mostRecentEventHolder.MostRecentEvent); //Note that event though card1 has already been triggered, nothing cleared it out
                    Assert.Equal(card2, stepReport.ReactionEvent);
                } else if (stepsTaken == 7) //Second event triggered
                {
                    Assert.Equal(TimelinePhase.InstantAction, stepReport.InitialPhase);
                    Assert.Equal(TimelinePhase.PostEffect, stepReport.FinalPhase);
                    Assert.Equal(0, stepReport.AdvanceAmount); //since they're at the same time, the timeline already rewound
                    Assert.Equal([], stepReport.EventBackup);
                    Assert.Equal(new HashSet<Okazo<int>>(), stepReport.PossibleEventBackup);
                    Assert.Equal(card2, stepReport.OccurredEvent);
                    Assert.Equal(card2, mostRecentEventHolder.MostRecentEvent);
                    Assert.Null(stepReport.ReactionEvent);
                }
            }
            Assert.Equal(8, stepsTaken); //If this fails, then we didn't do both events
            Assert.Equal(TimelinePhase.Display, timeline.Phase);
        }
        
        //Nothing should be different if an event is added during an InstantAction vs already there
        //This is because once it gets back to PostEffect, the state is the same as after any other action, with the one exception that
        //events could be pushed forward if an instant action triggered in the past
        #endregion
    }
}
