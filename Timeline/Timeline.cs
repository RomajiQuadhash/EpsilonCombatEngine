using CoreMetrics;
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
        public List<Okazo<T>> Events { get; private set; }
        /// <summary>
        /// Any events that are at time Never but could occur at some point in the future, so they should be kept track of. This is separate from the main list of events since they don't have a time remaining that can be used to sort them, and they shouldn't be processed until they have a valid time remaining.
        /// </summary>
        public ISet<Okazo<T>> PossibleEvents { get; private set; }
        /// <summary>
        /// If an event should occur immediately, this is set to that event so it can be processed in the InstantAction phase. Otherwise, this is null.
        /// Note that only one event can be processed per InstantAction phase, so this isn't a list.
        /// This is because one ReactionEvent can cause another event to become instant, cause an event to no longer be instant, or change the order of other instant events.
        /// </summary>
        public Okazo<T>? ReactionEvent { get; private set; }

        /// <summary>
        /// An event that is raised when the timeline advances. All events on the timeline should subscribe to this event so they can update their time remaining when time advances.
        /// </summary>
        public event EventHandler<T>? Advance;
        /// <summary>
        /// Status of the timeline, which determines what happens when Update is called and what actions are allowed. The timeline starts in the Open phase, and must be in the Open phase for events to be added or adjusted. 
        /// Calling Update will move the timeline through the phases in the order they are defined, with some exceptions for InstantAction phases that can occur multiple times between PostEffect and Display. 
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
            PossibleEvents = new HashSet<Okazo<T>>();
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
        #endregion
        #region Event Management
        /// <summary>
        /// General method for adding an event to the timeline. If the event is at time Never, it will be added to PossibleEvents, and if it has a valid time remaining, it will be added to Events. 
        /// You should use this method for any event that is being added during the Open phase, since it will put the event in the correct list based on its time remaining. 
        /// If you're adding an event during the InstantAction phase, you must use AddPossibleEvent instead, since events added during the InstantAction phase since only PossibleEvents can be run or modified then.
        /// </summary>
        /// <remarks>Try not to add events with negative time remaining since this causes the timeline to back up. Not neccessarily always a bug, but should be avoided.</remarks>
        /// <param name="e"></param>
        public void AddEvent(Okazo<T> e)
        {
            PhaseValid([TimelinePhase.Open]);
            if (e.TimeRemaining.IsNever)
            {
                PossibleEvents.Add(e);
            }
            else
            {
                Events.Add(e);
            }
        }
        /// <summary>
        /// Always adds the event as a possible event. You should only use this for events added during another event, such as:
        /// *The Zeroed phase, during executing an event, where we want to set up an event caused by the event that just occurred, but we don't know if it will occur immediately or not, so we add it to PossibleEvents and let the InstantActionCheck handle it. (if we know it will occur immediately, we can just do the effects without the timeline)
        /// *A reaction event that is being added during the InstantAction phase. 
        /// If you're adding an event during the Open phase,you should probably use AddEvent instead, since it will put the event in the correct list based on its time remaining.
        /// </summary>
        /// <remarks>Try not to add events with negative time remaining since this causes the timeline to back up. Not neccessarily always a bug, but should be avoided.</remarks>
        /// <param name="e"></param>
        public void AddPossibleEvent(Okazo<T> e)
        {
            PhaseValid([TimelinePhase.Open,TimelinePhase.Zeroed,TimelinePhase.InstantAction]);
            PossibleEvents.Add(e);
        }
        /// <summary>
        /// Removes an event from the timeline. Will not unsubscribe the event from the Advance event, so unsubscribe manually if you're not just going to delete the event entirely.
        /// During InstantAction or Zeroed, only PossibleEvents is checked. This can lead to returning false even if the event is on the timeline
        /// </summary>
        /// <param name="e"></param>
        /// <returns>True if the event was successfully removed; otherwise, false.</returns>
        public bool RemoveEvent(Okazo<T> e)
        {
            PhaseValid([TimelinePhase.Open, TimelinePhase.InstantAction, TimelinePhase.Zeroed]);
            if(Phase != TimelinePhase.Open)
            {
                // During the InstantAction or Zeroed phase, only PossibleEvents can be modified, so only try PossibleEvents.
                return PossibleEvents.Remove(e);
            }
            if (!Events.Remove(e))
                return PossibleEvents.Remove(e);
            return true;
        }
        #endregion
        #region Step Processing
        /// <summary>
        /// Goes through the phases of the timeline in order, processing events as necessary, until it reaches the Display phase or Terminated phase. 
        /// Yields a report after each step, which can be used to update the display of the timeline as it changes. 
        /// If the timeline is terminated, the battle is over, so check the TerminationReason to see how it ended and display the timeline in its final state.
        /// </summary>
        /// <returns>A report for each stage.</returns>
        public IEnumerable<StepReport<T>> TakeSteps()
        {
            while (Phase != TimelinePhase.Terminated && Phase != TimelinePhase.Display)
            {
                StepReport<T> curReport = new(Phase);
                Advance += curReport.OnAdvance; // Subscribe the report to the Advance event so it can track how much time has advanced during this step.
                switch (Phase) {
                    case TimelinePhase.Open:
                        // If we're being told to advance, time to close and Purge.
                        if (Purge())
                        {
                            Phase = TimelinePhase.Purged;
                        }
                        else
                        {
                            // If Purge returns false, there are no events left to process, so the battle is over. Terminate with the NoEvents reason.
                            Terminate(TerminationType.NoEvents);
                        }
                        break;
                    case TimelinePhase.Purged:
                        // Now, sort the events and move to the Sorted phase.
                        Events.Sort(); // This should sort by time remaining since Okazo<T> implements IComparable and should be compared by time remaining. No events are Never, so there won't be a NeverIsNotATime exception here.
                        Phase = TimelinePhase.Sorted;
                        break;
                    case TimelinePhase.Sorted:
                        // Move time forward so the next event to occur is at time zero, and move to the Zeroed phase.
                        Advance.Invoke(this, Events[0].TimeRemaining.Time); //This can't be never, since we should have purged it.
                        Phase = TimelinePhase.Zeroed;
                        break;
                    case TimelinePhase.Zeroed:
                        // Do the first event now that time is at zero, and move to the PostEffect phase to check for any consequences of this event.
                        Events[0].Trigger();
                        curReport.OccurredEvent=Events[0];
                        Events.RemoveAt(0);
                        Phase = TimelinePhase.PostEffect;
                        break;
                    case TimelinePhase.PostEffect:
                        // We've done the event and removed it from the list, but we haven't checked for any things that should happen immediately as a result of this event.
                        if (InstantActionCheck())
                        {
                            Phase = TimelinePhase.InstantAction;
                            curReport.ReactionEvent = ReactionEvent;
                        }
                        else
                        {
                            Phase = TimelinePhase.Display;
                        }
                        break;
                    case TimelinePhase.InstantAction:
                        // Do the event that just was added...
                        if (ReactionEvent == null)
                        {
                            throw new InvalidOperationException("ReactionEvent should have been set in the InstantActionCheck if we returned true, but it was null. Clearly someone forgot to set it.");
                        }
                        if (ReactionEvent.TimeRemaining.Time != T.Zero)
                        {
                            Advance.Invoke(this, ReactionEvent.TimeRemaining.Time); // This should rewind time so the ReactionEvent is at time zero.
                        }
                        ReactionEvent.Trigger();
                        curReport.OccurredEvent = ReactionEvent;
                        ReactionEvent = null;
                        Phase = TimelinePhase.PostEffect; // Then go back to PostEffect to check for any more consequences of the original event or the new event, and repeat this process until there are no more instant actions to perform, at which point we can move to Display.
                        break;
                }
                Advance -= curReport.OnAdvance; // Unsubscribe the report from the Advance event so it doesn't track time advancements during the next step.
                curReport.FinalPhase = Phase;
                curReport.EventBackup = [.. Events];
                curReport.PossibleEventBackup = new HashSet<Okazo<T>>(PossibleEvents);
                yield return curReport; 
            }
            yield break;
        }
        /// <summary>
        /// Purges events from the timeline, moving promotable events to the main list and handling events that could never occur.
        /// </summary>
        /// <returns>True if there are events left to process after purging, false otherwise.</returns>
        private bool Purge() {
            //First, clean up possible events, and save any to promote to the main list.
            List<Okazo<T>> promotableEvents = [];
            foreach (Okazo<T> e in PossibleEvents)
            {
                if (e.CouldOccur && e.TimeRemaining.IsNever)
                {
                    // If the event could occur but is still at Never, keep it in PossibleEvents and move on.
                    continue;
                }
                if (!e.TimeRemaining.IsNever)
                {
                    promotableEvents.Add(e);
                }
                // Always remove events that couldn't occur or are no longer at never, since they shouldn't be in PossibleEvents.
                PossibleEvents.Remove(e);
            }
            // Now, add move any Never events from the main list to PossibleEvents, unless they could never occur, in which case we can just delete them.
            for (int i = Events.Count - 1; i >= 0; i--)
            {
                if (Events[i].TimeRemaining.IsNever)
                {
                    if (Events[i].CouldOccur)
                    {
                        PossibleEvents.Add(Events[i]);
                    }
                    Events.RemoveAt(i);
                }
            }
            // Finally, add any promotable events to the main list.
            // These will be sorted in the next phase, so we don't need to worry about sorting them now.
            Events.AddRange(promotableEvents);
            return Events.Count!=0;
        }
        /// <summary>
        /// Finds if any of the possible events should occur immediately, and if so, sets the ReactionEvent to the earliest of these events, removes it from PossibleEvents, and rewinds time so this event is at time zero.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if there is already a reaction event to process.</exception>
        /// <returns>True if an action was found and set as the ReactionEvent, false otherwise.</returns>
        private bool InstantActionCheck()
        {
            if (ReactionEvent != null)
            {
                throw new InvalidOperationException("There's already a reaction event to process. Don't look for another one.");
            }
            // Keep track of the earliest time remaining among the possible events
            TimeOrNever<T> timeToBeat = new();
            foreach (Okazo<T> e in PossibleEvents)
            {
                if (e.TimeRemaining.CompareTo(timeToBeat) > 0) //Note that if e.TimeRemaining is Never, then it will be greater than timeToBeat, so we don't need to check for that explicitly.
                {
                    // If e is later than the timeToBeat, then it is positive, less negative than the current ReactionEvent, or it is never.
                    continue;
                }
                if (ReactionEvent != null && ReactionEvent.CompareTo(e) < 0)
                {
                    // If we already have a ReactionEvent and it sorts earlier than e, then we should keep the current ReactionEvent
                    continue;
                }
                timeToBeat = e.TimeRemaining;
                ReactionEvent = e;
            }
            if (ReactionEvent == null)
            {
                return false;
            }
            // Remove the ReactionEvent from PossibleEvents, since it's now happening.
            PossibleEvents.Remove(ReactionEvent);
            return true;
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
        /// <summary>
        /// Changes the phase of the timeline for debugging purposes. This should only be used in unit tests.
        /// </summary>
        /// <param name="newPhase">The phase to set the timeline to. ALWAYS avoid using this if possible</param>
        /// <exception cref="InvalidOperationException">Always thrown. Catch in unit tests, but don't catch this ever in real code.</exception>
        public void DebugChangePhase(TimelinePhase newPhase)
        {
            Phase = newPhase;
            throw new InvalidOperationException("DebugChangePhase should only be used for testing purposes. Don't use this in production code.");
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
        PostEffect, // An event has just occurred, either from the main list or as an instant action, but we haven't checked for any consequences of this event yet. This is where we check for any events that should occur immediately as a result of this event, and if there are any, we move to the InstantAction phase to do them before moving to Display.
        InstantAction, // Only reached if a Possible Event becomes 0 or negative during the PostEffect phase. Time is rewound so the earliest of these events is at time zero, and all events that are at time zero are at the front of the list in some deterministic order. Returns to PostEffect after this.
        Display, // After PostEffect and any InstantAction phases are complete, the timeline is ready for display. A visual representation of the timeline should be generated at this point, and any events that are at time zero should be highlighted as occurring now. Set back to Open after this.
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
