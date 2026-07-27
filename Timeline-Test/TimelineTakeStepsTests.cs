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

    }
}
