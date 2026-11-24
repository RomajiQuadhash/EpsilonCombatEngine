using CoreMetrics;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Meters
{
    /// <summary>
    /// A temporary change with two fixed lengths of time: how long it waits before applying, and how long it stays applied before it is removed.
    /// This holds the timing, the priorities and the cloning. A subclass only has to say what applying and removing do to its kind of meter.
    /// </summary>
    /// <remarks>
    /// Like a card, it isn't put on the timeline automatically. Use AddEvent or AddPossibleEvent once it is made.
    /// For changes that compute as they go, subclass AbstractTempChange.
    /// Made with Claude, some adjustments by hand.
    /// </remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for the affected meter</typeparam>
    public abstract class TimedTempChange<T, M> : AbstractTempChange<T, M> where T : INumber<T> where M : INumber<M>
    {
        private readonly AbstractMeter<T, M> _meter;
        /// <summary>
        /// Sets up the timing. It starts out waiting to apply.
        /// </summary>
        /// <param name="meter">The meter the change applies to</param>
        /// <param name="waitingTime">How long from now until the change applies. Negative values cause the meter to rewind</param>
        /// <param name="applyTime">How long the change stays applied before it is removed. 0 or more</param>
        /// <param name="applyPriority">The priority while it is waiting to apply</param>
        /// <param name="removePriority">The priority while it is waiting to be removed</param>
        /// <param name="owningTimeline">The timeline this is for. Must be the meter's</param>
        /// <exception cref="ArgumentOutOfRangeException">If apply time is negative.</exception>
        protected TimedTempChange(AbstractMeter<T, M> meter, T waitingTime, T applyTime, PriorityRank? applyPriority, PriorityRank? removePriority, Timeline<T> owningTimeline)
        {
            if (T.IsNegative(applyTime))
            {
                throw new ArgumentOutOfRangeException(nameof(applyTime), applyTime, "Apply time can't be negative");
            }
            _meter = meter;
            WaitingTime = waitingTime;
            ApplyTime = applyTime;
            ApplyPriority = applyPriority;
            RemovePriority = removePriority;
            OwningTimeline = owningTimeline;
            TimeToEvent = waitingTime;
            LifeCyclePhase = ApplyOrRemove.Apply;
            OwningTimeline.Advance += OnAdvance;
        }
        public override Timeline<T> OwningTimeline { get; }
        public override AbstractMeter<T, M> MeterAppliedTo => _meter;
        /// <summary>
        /// How long it waited (or is waiting) from when it was made until the change applies.
        /// </summary>
        public T WaitingTime { get; }
        /// <summary>
        /// How long the change stays applied before it is removed.
        /// </summary>
        public T ApplyTime { get; }
        /// <summary>
        /// The time until whichever comes next: applying if it hasn't applied yet, removing if it has.
        /// </summary>
        public T TimeToEvent { get; set; }
        /// <summary>
        /// Total amount of time until this event is gone for good
        /// </summary>
        public T TotalRemainingTime => LifeCyclePhase == ApplyOrRemove.Apply ? TimeToEvent + ApplyTime : TimeToEvent;
        /// <summary>
        /// The priority while it is waiting to apply.
        /// </summary>
        public PriorityRank? ApplyPriority { get; set; }
        /// <summary>
        /// The priority while it is waiting to be removed.
        /// </summary>
        public PriorityRank? RemovePriority { get; set; }
        /// <summary>
        /// Whichever of ApplyPriority and RemovePriority matches what it is waiting to do. Setting it sets that one.
        /// </summary>
        public override PriorityRank? Priority
        {
            get => LifeCyclePhase == ApplyOrRemove.Apply ? ApplyPriority : RemovePriority;
            set
            {
                if (LifeCyclePhase == ApplyOrRemove.Apply)
                {
                    ApplyPriority = value;
                }
                else
                {
                    RemovePriority = value;
                }
            }
        }
        /// <summary>
        /// Always a time, since both the apply and the remove are at known times. Setting it is invalid
        /// </summary>
        public override TimeOrNever<T> TimeRemaining { get => new(TimeToEvent); set { throw new InvalidOperationException("Set the time with TimeToEvent"); } }
        /// <summary>
        /// Always true, since it is about specific times in the future.
        /// </summary>
        public override bool CouldOccur { get => true; set { throw new InvalidOperationException("A TimedTempChange always can occur."); } }
        public override bool HaltsInstantAction { get; set; } = false;
        /// <summary>
        /// Guarantee that there's at least one difference between two temporary changes
        /// UNLESS YOU'RE A UNIT TEST, NEVER SET THIS
        /// </summary>
        public override int UUID { get; set; } = Guid.NewGuid().GetHashCode();
        /// <summary>
        /// True once the change has been removed again. After that it is off the timeline and does nothing more.
        /// </summary>
        public bool IsFinished { get; private set; }

        public override void OnAdvance(object? sender, T timeElapsed)
        {
            if (sender != OwningTimeline)
            {
                throw new InvalidOperationException("Don't subscribe a temporary change to a timeline it doesn't belong to.");
            }
            TimeToEvent -= timeElapsed;
        }
        /// <summary>
        /// Starts counting down to the remove, then makes the change.
        /// </summary>
        public sealed override void TriggerWhenApply()
        {
            TimeToEvent = ApplyTime;
            ApplyChange();
        }
        /// <summary>
        /// Takes the change back off and marks this as finished.
        /// </summary>
        public sealed override void TriggerWhenRemove()
        {
            IsFinished = true;
            RemoveChange();
        }
        /// <summary>
        /// Makes the change to the meter. Called once, when the waiting time is up.
        /// </summary>
        protected abstract void ApplyChange();
        /// <summary>
        /// Undoes what ApplyChange did. Called once, when the apply time is up.
        /// </summary>
        protected abstract void RemoveChange();

        #region Cloning
        /// <summary>
        /// Standard clone method for a TimedTempChange and subclasses. Subclasses override CreateEmptyForClone, and CopyPropertiesForClone if they have more to copy.
        /// </summary>
        /// <exception cref="UnrecoverableCloneException">Thrown when the owning timeline has not been cloned yet.</exception>
        public override object Clone(CloneContext context)
        {
            if (context.TryGet(this, out TimedTempChange<T, M> existingClone))
            {
                return existingClone;
            }
            if (!context.TryGet(OwningTimeline, out Timeline<T> owningTimelineClone))
            {
                throw new UnrecoverableCloneException("Owning timeline must be cloned before cloning any events.");
            }
            TimedTempChange<T, M> clone = CreateEmptyForClone(owningTimelineClone, context);
            context.Register(this, clone);
            CopyPropertiesForClone(clone, context);
            return clone;
        }
        /// <summary>
        /// Makes a new instance of the subclass for the cloned timeline, applying to the clone of the meter.
        /// </summary>
        /// <param name="owningTimeline">The timeline that should own the clone</param>
        /// <param name="context">The clone context, for getting the clone of the meter</param>
        protected abstract TimedTempChange<T, M> CreateEmptyForClone(Timeline<T> owningTimeline, CloneContext context);
        /// <summary>
        /// Override this method (but always call base.CopyPropertiesForClone) in subclasses to copy any additional properties to the clone.
        /// </summary>
        /// <param name="clone">The new clone to modify</param>
        /// <param name="context">The clone context, in case you need to clone or reference clones of any other objects</param>
        protected virtual void CopyPropertiesForClone(TimedTempChange<T, M> clone, CloneContext context)
        {
            clone.UUID = UUID;
            clone.HaltsInstantAction = HaltsInstantAction;
            clone.LifeCyclePhase = LifeCyclePhase;
            clone.TimeToEvent = TimeToEvent;
            clone.IsFinished = IsFinished;
            if (IsFinished)
            {
                //A finished one stopped listening to the timeline, and making the clone started it listening again.
                clone.OwningTimeline.Advance -= clone.OnAdvance;
            }
        }
        #endregion

        public override string ToString()
        {
            string state = IsFinished ? "finished" : LifeCyclePhase == ApplyOrRemove.Apply ? $"applies in {TimeToEvent}" : $"removed in {TimeToEvent}";
            return $"{GetType().Name} (UUID: {UUID}) on {MeterAppliedTo}, {state}";
        }
    }
}
