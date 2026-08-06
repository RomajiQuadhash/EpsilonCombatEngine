using CoreMetrics;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Meters
{
    /// <summary>
    /// An abstract placeholder for events that are triggered by crossing a threshold.
    /// Examples might be hitting the top or bottom of the range (like a KO, death, or max health/MP)
    /// or crossing a specific threshold (like getting a vibe meter to sufficently rage/focus to perform an action).
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for the metric</typeparam>
    public abstract class AbstractThresholdEvent<T,M> : Okazo<T>, IOkUUID, IOkPriority,IOkTransition<M> where T : INumber<T> where M : INumber<M>
    {
        /// <summary>
        /// Guarentee that there's at least one difference between two events
        /// UNLESS YOU'RE A UNIT TEST, NEVER SET THIS
        /// </summary>
        public int UUID { get; set; } = Guid.NewGuid().GetHashCode();
        /// <summary>
        /// Used to get the transition data and compute the time remaining until the threshold is crossed.
        /// </summary>
        public abstract AbstractMeter<T,M> OwningMeter { get; }
        //Almost any priority could happen when a threshold is crossed, not just the ones that should always be from a threshold crossing.
        //So, no need to restrict the priority to a specific set of values, just use the general PrioityRank enum.
        public abstract PrioityRank? Priority { get; }
        /// <summary>
        /// Note that if this event is never, this will throw an InvalidOperationException,
        /// since we'll construct a TransitionData object with invalid data.
        /// This is OK because a never event will never end up in OkazoComparitor.Compare
        /// </summary>
        /// <remarks>TODO: consider if we can just, implement this</remarks>
        public abstract TransitionData<M> TransitionData { get; }
        /// <summary>
        /// Explicitly settable, since we might want to disable an event without removing it immediately
        /// (it will be removed when the timline is cleaned (Open and PostEffect when TakeSteps is called)
        /// </summary>
        public override bool CouldOccur { get; set; }
        /// <summary>
        /// Gets the remaining time until the threshold event occurs, or a value indicating it will "never" occur.
        /// "Never" really means "not predicted to occur based on current properties", 
        /// since the meter might change in the future and make it possible for the threshold to be crossed.
        /// </summary>
        /// <remarks>The value is calculated by the ComputeTimeRemaining method. 
        /// Setting this property is illogical and will throw an InvalidOperationException.</remarks>
        public override TimeOrNever<T> TimeRemaining { get {return ComputeTimeRemaining(); } set { throw new InvalidOperationException(); } }
        /// <summary>
        /// Used to compute the remaining time until the threshold event occurs.
        /// </summary>
        /// <returns>The remaining time until the threshold event occurs, or Never if it isn't predicted to occur</returns>
        /// <remarks>Why not just call OwningMeter.ThresholdTouch or OwningMeter.ThresholdCross?
        /// Well, which one is correct might depend of what the purpose of the threshold is, but there's another difficulty.
        /// We might want to call them with allowNegative=true sometimes, but usually with it false.
        /// </remarks>
        internal abstract TimeOrNever<T> ComputeTimeRemaining();
    }
}
