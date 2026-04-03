using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Timeline
{
    public class Timeline<T> where T : INumber<T>
    {
        /// <summary>
        /// The current list of events on the timeline, sorted by time remaining.
        /// Re-sorted after every time advancement since one or more events may have changed their time remaining, and new events may have been added.
        /// </summary>
        public List<IOkazo<T>> Events { get; private set; }
        /// <summary>
        /// Any events that are at time Never but could occur at some point in the future, so they should be kept track of. This is separate from the main list of events since they don't have a time remaining that can be used to sort them, and they shouldn't be processed until they have a valid time remaining.
        /// </summary>
        public ISet<IOkazo<T>> PossibleEvents { get; private set; }
        /// <summary>
        /// Status of the timeline, which determines what happens when Update is called and what actions are allowed. The timeline starts in the Open phase, and must be in the Open phase for events to be added or adjusted. 
        /// Calling Update will move the timeline through the phases in the order they are defined, with some exceptions for InstantAction phases that can occur multiple times between EffectApplied and Display. 
        /// After Display, use "SetOpen" to set the timeline back to the Open phase to prepare for the next update.
        /// If the timeline is Terminated, the battle is over, so check the TerminationReason to see how it ended and display the timeline in its final state.
        /// </summary>
        public TimelinePhase Phase { get; private set; }
        /// <summary>
        /// Should be NotTerminated until the timeline is Terminated, at which point it indicates how the battle ended.
        /// You should always check this before deleting the timeline!
        /// </summary>
        public TerminationType TerminationReason { get; private set; }
        public Timeline()
        {
            Events = [];
            PossibleEvents = new HashSet<IOkazo<T>>();
        }

        #region Phase Handlers
        /// <summary>
        /// Use to return the timeline to the Open phase after Display.
        /// Only use after Display, since otherwise we're in the middle of processing events. (also it would throw an error)
        /// </summary>
        public void SetOpen()
        {
            PhaseValid([TimelinePhase.Display]);
            Phase = TimelinePhase.Open;
        }
        /// <summary>
        /// Terminates the timeline.
        /// </summary>
        /// <param name="reason">Why the timeline was terminated. Obviously, can't be "not terminated"</param>
        /// <exception cref="InvalidPhaseForActionException">Can't re-terminate execution.</exception>
        /// <exception cref="ArgumentException">If you try to haver NotTerminated as a termination reason, enjoy your prize</exception>
        public void Terminate(TerminationType reason)
        {
            if (Phase == TimelinePhase.Terminated)
            {
                throw new InvalidPhaseForActionException("Timeline is already terminated.");
            }
            if (reason == TerminationType.NotTerminated)
            {
                throw new ArgumentException("Termination reason cannot be NotTerminated.");
            }
            Phase = TimelinePhase.Terminated;
            TerminationReason = reason;
        }
        /// <summary>
        /// Goes through the phases of the timeline in order, processing events as necessary, until it reaches the Display phase or Terminated phase. 
        /// Yields a report after each step, which can be used to update the display of the timeline as it changes. 
        /// If the timeline is terminated, the battle is over, so check the TerminationReason to see how it ended and display the timeline in its final state.
        /// 
        /// TODO: Actually have a report to return here, and actually implement the logic for each phase. For now, this is just a skeleton to show how the phases will work and to make sure the structure of the timeline is sound.
        /// </summary>
        /// <returns>A report for each stage.</returns>
        internal IEnumerable<StepReport<T>> TakeSteps()
        {
            while (Phase != TimelinePhase.Terminated)
            {
                switch (Phase) {
                    case TimelinePhase.Open:
                        // If we're being told to advance, time to close and purge.
                        if (purge())
                        {
                            Phase = TimelinePhase.Purged;
                        }
                        else
                        {
                            // If purge returns false, there are no events left to process, so the battle is over. Terminate with the NoEvents reason.
                            Terminate(TerminationType.NoEvents);
                        }
                        break;
                    case TimelinePhase.Purged:
                        // Now, sort the events and move to the Sorted phase.
                        Events.Sort();
                        Phase = TimelinePhase.Sorted;
                        break;
                    case TimelinePhase.Sorted:
                        // Move time forward so the next event to occur is at time zero, and move to the Zeroed phase.
                        //TODO: add an event to actually move all the IOkazo<T> in Events forward by the time of the next event
                        Phase = TimelinePhase.Zeroed;
                        break;
                    case TimelinePhase.Zeroed:
                        //TODO: Apply the effects...
                        Phase = TimelinePhase.EffectApplied;
                        break;
                    case TimelinePhase.EffectApplied:
                        // We've done the event, now delete it then check if there are any consequences of the event that need to be added to the timeline, and add them if so.
                        Events.RemoveAt(0);
                        if (instantActionCheck())
                        {
                            Phase = TimelinePhase.InstantAction;
                        }
                        else
                        {
                            Phase = TimelinePhase.Display;
                        }
                        break;
                    case TimelinePhase.InstantAction:
                        // Do the event that just was added...
                        //TODO: Actually do that
                        Phase = TimelinePhase.EffectApplied; // Then go back to EffectApplied to check for any more consequences of the original event or the new event, and repeat this process until there are no more instant actions to perform, at which point we can move to Display.
                        break;
                    case TimelinePhase.Display:
                        // We shouldn't actually do anything in this phase, we're waiting for the caller to call SetOpen to move back to the Open phase and prepare for the next update.
                        break;
                }
                yield return new StepReport<T>(); //TODO: actually return something useful here
            }
            yield break;
        }
        private bool purge() {
            //TODO: implement purge logic, which moves any events that are not Never among Events and PossibleEvents to Events, and moves any events that are Never among Events to PossibleEvents. Returns false if there are no events left to process after purging, which would indicate that the battle is over due to no events left.
            return Events.Count!=0;
        }
        private bool instantActionCheck()
        {
            //TODO: implement instant action check, which checks if any events in PossibleEvents have become 0 or negative, and if so, moves the earliest of these events to the front of the list in some deterministic order, rewinds time to that new event and returns true. Otherwise, returns false.
            return false;
        }
        #endregion

        #region helpers
        /// <summary>
        /// Used with any action that can only be performed during certain phases of the timeline to error check.
        /// </summary>
        /// <param name="validPhases">Any collection that can hold phases. Sets are probably fastest.</param>
        /// <exception cref="InvalidPhaseForActionException"></exception>
        protected void PhaseValid(ICollection<TimelinePhase> validPhases)
        {
            if (!validPhases.Contains(Phase))
            {
                throw new InvalidPhaseForActionException($"Phase must be one of {validPhases} to perform this action. Current phase: {Phase}");
            }
        }
        #endregion

    }
    /// <summary>
    /// What state the timeline is in, which determines what happens when Update is called and what actions are allowed.
    /// </summary>
    public enum TimelinePhase
    {
        Open, // The timeline is open for events to be added and adjusted. Must be here before advancing time.
        Purged, // Any event that is not Never among Events and PossibleEvents is in Events, and all events in PossibleEvents are Never.
        Sorted, // All events in Events are sorted by time remaining.
        Zeroed, // The next event to occur is at time zero, and all events that are at time zero are at the front of the list in some deterministic order.
        EffectApplied, // The effect of the event at the front of the list has been applied and deleted, but any consequences of the event have not yet been checked.
        InstantAction, // Only reached if a Possible Event becomes 0 or negative during the EffectApplied phase. Time is rewound so the earliest of these events is at time zero, and all events that are at time zero are at the front of the list in some deterministic order. Returns to EffectApplied after this.
        Display, // After EffectApplied and any InstantAction phases are complete, the timeline is ready for display. A visual representation of the timeline should be generated at this point, and any events that are at time zero should be highlighted as occurring now. Set back to Open after this.
        Terminated = 255 // The battle is over, so the timeline is terminated. No events should be added or processed at this point, and the timeline should be displayed in its final state.
    }
    /// <summary>
    /// Final results of the battle.
    /// </summary>
    public enum TerminationType
    {
        NotTerminated,
        Escape,
        Victory,
        DefeatedKO,//If all players are knocked out
        Killed, //If at least one player is killed, the battle is over, since, they're kids, they don't have the resilience to keep fighting after that.
        NoEvents, // If there are no events left to process, the battle is over. Very likely indicates a bug, since on a player turn, they should add at least one event to the timeline (a "give control back to player" event at minimum).
        UnknownEnding // Debug option. Should never be used in a real battle, but can be used for testing purposes when the battle ends without any of the other conditions being met, such as if the timeline is terminated manually or if there is a bug that causes the battle to end prematurely.
    }
}
