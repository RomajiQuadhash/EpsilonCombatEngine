using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Timeline
{
    /// <summary>
    /// Represents a report for a single step in the timeline.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public class StepReport<T>(TimelinePhase initial) : object(), IAdvanceable<T> where T : INumber<T>
    {
        /// <summary>
        /// What phase the timeline was in at the start of the step.
        /// Important because the timeline will have changed phase by the end of the step, so this important context to understand what happened during the step and why.
        /// </summary>
        public TimelinePhase InitialPhase { get; private set; } = initial;
        /// <summary>
        /// What phase the timeline is in at the end of the step.
        /// Most phases should only have one next phase, but some phases may have multiple possible next phases depending on what events occur during the step, so this is important context to understand what happened during the step and why.
        /// </summary>
        public TimelinePhase FinalPhase { get; set; }
        /// <summary>
        /// The amount time was advanced during the step. Should be zero unless the InitialPhase is Sorted, but,
        /// could be non-zero during InstantAction if an event was triggered in the past (should be rounding error if anything).
        /// 
        /// This should be only updated by the Advance event, hence private set.
        /// </summary>
        public T AdvanceAmount { get; private set; } = T.Zero;
        /// <summary>
        /// Copy of the events on the timeline as they were at the end of the step, for reference.
        /// Note that this is shallow copy so don't rely on it persisting into the next step.
        /// </summary>
        public List<Okazo<T>>? EventBackup { get; set; }
        /// <summary>
        /// Shallow copy of the events that are not currently on the timeline but could occur in the future, for reference.
        /// Note that this is shallow copy so don't rely on it persisting into the next step.
        /// </summary>
        public ISet<Okazo<T>>? PossibleEventBackup { get; set; }
        /// <summary>
        /// If an event occurred during this step, saves the event.
        /// Note that this is shallow copy so don't rely on it persisting into the next step.
        /// </summary>
        public Okazo<T>? OccurredEvent { get; set; }
        /// <summary>
        /// If there is an event selected to be a reaction during this step, saves the event.
        /// Note that this is shallow copy so don't rely on it persisting into the next step.
        /// </summary>
        public Okazo<T>? ReactionEvent { get; set; }

        #region Event Handlers
        /// <summary>
        /// Handles the advancement event by processing the elapsed time since the last update.
        /// </summary>
        /// <param name="sender">The Timeline advancing time</param>
        /// <param name="timeElapsed">The amount of time to move the event by.</param>
        public void OnAdvance(object? sender, T timeElapsed)
        {
            AdvanceAmount += timeElapsed;
        }
        #endregion

        //TODO: Have a way to turn this into a string, with an optional parameter to specify how much detail to include, for debugging purposes.
    }
    //TODO: Choose what report levels should be.
}
