using CoreMetrics;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Meters
{
    /// <summary>
    /// An event that makes one instant change to a meter at a set time, and is then done. Nothing undoes it.
    /// For a change that takes itself back off later, use TempInstantChange.
    /// </summary>
    /// <remarks>Like a card, it isn't put on the timeline automatically. Use AddEvent or AddPossibleEvent once it is made.</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for the affected meter</typeparam>
    /// <remarks>Made by Claude, approved by Romaji</remarks>
    public class InstantChangeEvent<T, M> : Okazo<T>, IOkPriority, IMeterChangeEvent<T, M> where T : INumber<T> where M : INumber<M>
    {
        /// <param name="meter">The meter to change</param>
        /// <param name="change">How much to add to the meter. Negative to take away</param>
        /// <param name="timeToEvent">How long from now until the change happens</param>
        /// <param name="priority">The priority of this event. Usually InstantDamage or InstantMeterChange</param>
        /// <param name="owningTimeline">The timeline this is for. Must be the meter's</param>
        public InstantChangeEvent(InstantChangeMeter<T, M> meter, M change, T timeToEvent, PriorityRank? priority, Timeline<T> owningTimeline)
        {
            MeterAppliedTo = meter;
            Change = change;
            TimeToEvent = timeToEvent;
            Priority = priority;
            OwningTimeline = owningTimeline;
            OwningTimeline.Advance += OnAdvance;
        }
        public Timeline<T> OwningTimeline { get; }
        /// <summary>
        /// The meter this changes.
        /// </summary>
        public InstantChangeMeter<T, M> MeterAppliedTo { get; }
        AbstractMeter<T, M> IMeterChangeEvent<T, M>.MeterAppliedTo => MeterAppliedTo;
        public Okazo<T> AsOkazo => this;
        /// <summary>
        /// How much this tries to add to the meter.
        /// </summary>
        public M Change { get; }
        /// <summary>
        /// How much it actually added, which is less than Change if the meter hit its minimum or maximum. 0 until it occurs.
        /// </summary>
        public M AppliedChange { get; private set; } = M.Zero;
        /// <summary>
        /// True once the change has been made. After that it is off the timeline and does nothing more.
        /// </summary>
        public bool IsFinished { get; private set; }
        /// <summary>
        /// The time until the change happens.
        /// </summary>
        public T TimeToEvent { get; set; }
        public PriorityRank? Priority { get; set; }
        /// <summary>
        /// Always a time, since the change is at a known time. It can't be set here. Set the time with TimeToEvent.
        /// </summary>
        public override TimeOrNever<T> TimeRemaining { get => new(TimeToEvent); set { throw new InvalidOperationException("Set the time with TimeToEvent"); } }
        /// <summary>
        /// Always true, since it is about a specific time in the future.
        /// </summary>
        public override bool CouldOccur { get => true; set { throw new InvalidOperationException("An InstantChangeEvent always can occur."); } }
        public override bool HaltsInstantAction { get; set; } = false;
        /// <summary>
        /// Guarantee that there's at least one difference between two events
        /// UNLESS YOU'RE A UNIT TEST, NEVER SET THIS
        /// </summary>
        public override int UUID { get; set; } = Guid.NewGuid().GetHashCode();

        public override void OnAdvance(object? sender, T timeElapsed)
        {
            if (sender != OwningTimeline)
            {
                throw new InvalidOperationException("Don't subscribe an event to a timeline it doesn't belong to.");
            }
            TimeToEvent -= timeElapsed;
        }
        /// <summary>
        /// Notifies others that this event is occurring, then makes the change.
        /// </summary>
        /// <exception cref="InvalidOperationException">If the time isn't zero, can't trigger</exception>
        public override void Trigger()
        {
            if (TimeToEvent != T.Zero)
            {
                throw new InvalidOperationException("Events should only occur at time zero.");
            }
            InvokeOccurring();
            OwningTimeline.Advance -= OnAdvance;
            M before = MeterAppliedTo.CurrentValue;
            MeterAppliedTo.AddInstantChange(Change);
            AppliedChange = MeterAppliedTo.CurrentValue - before;
            IsFinished = true;
        }
        /// <exception cref="UnrecoverableCloneException">Thrown when the owning timeline has not been cloned yet.</exception>
        public override object Clone(CloneContext context)
        {
            if (context.TryGet(this, out InstantChangeEvent<T, M> existingClone))
            {
                return existingClone;
            }
            if (!context.TryGet(OwningTimeline, out Timeline<T> owningTimelineClone))
            {
                throw new UnrecoverableCloneException("Owning timeline must be cloned before cloning any events.");
            }
            InstantChangeEvent<T, M> clone = new(context.GetOrClone(MeterAppliedTo), Change, TimeToEvent, Priority, owningTimelineClone);
            context.Register(this, clone);

            //Move other properties
            clone.UUID = UUID;
            clone.HaltsInstantAction = HaltsInstantAction;
            clone.AppliedChange = AppliedChange;
            clone.IsFinished = IsFinished;
            if (IsFinished)
            {
                //A finished one stopped listening to the timeline, and making the clone started it listening again.
                owningTimelineClone.Advance -= clone.OnAdvance;
            }
            return clone;
        }
        public override string ToString()
        {
            return $"InstantChangeEvent (UUID: {UUID}) of {Change} on {MeterAppliedTo}, " + (IsFinished ? "finished" : $"in {TimeToEvent}");
        }
    }
}
