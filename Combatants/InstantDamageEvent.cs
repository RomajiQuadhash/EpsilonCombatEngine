using CoreMetrics;
using Meters;
using Rationalz;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Combatants
{
    /// <summary>
    /// An event that deals damage to a HealthMeter all at once at a set time, and is then done.
    /// The damage is what it was before Defense. When it occurs, the health meter divides it by the Defense as it is at that moment (never counting a Guard)
    /// and rounds up, so any damage at all takes at least 1.
    /// This is for the bonus of a Direct Hit and for attacks that are all instant. It doesn't chip the Guard.
    /// Like a card, it isn't put on the timeline automatically. Use AddEvent or AddPossibleEvent once it is made.
    /// </summary>
    /// <remarks>Claude written, Romaji approved</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public class InstantDamageEvent<T> : Okazo<T>, IOkPriority, IMeterChangeEvent<T, int> where T : INumber<T>
    {
        /// <param name="meter">The health the damage is dealt to</param>
        /// <param name="magnitude">The damage as it is before Defense. 0 or more</param>
        /// <param name="timeToEvent">How long from now until the damage is dealt. Negative for one that should have been dealt in the past, which makes the timeline rewind to then</param>
        /// <param name="priority">The priority of this event. InstantDamage unless there is a reason for another</param>
        /// <param name="owningTimeline">The timeline this is for. Must be the meter's</param>
        /// <exception cref="ArgumentOutOfRangeException">If magnitude is negative</exception>
        public InstantDamageEvent(HealthMeter<T> meter, Rational<long> magnitude, T timeToEvent, PriorityRank? priority, Timeline<T> owningTimeline)
        {
            if (magnitude.Numerator < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magnitude), magnitude, "Instant damage can't be negative");
            }
            MeterAppliedTo = meter;
            Magnitude = magnitude;
            TimeToEvent = timeToEvent;
            Priority = priority;
            OwningTimeline = owningTimeline;
            OwningTimeline.Advance += OnAdvance;
        }
        public Timeline<T> OwningTimeline { get; }
        /// <summary>
        /// The health the damage is dealt to.
        /// </summary>
        public HealthMeter<T> MeterAppliedTo { get; }
        AbstractMeter<T, int> IMeterChangeEvent<T, int>.MeterAppliedTo => MeterAppliedTo;
        public Okazo<T> AsOkazo => this;
        /// <summary>
        /// The damage as it is before Defense.
        /// </summary>
        public Rational<long> Magnitude { get; }
        /// <summary>
        /// How much health was actually lost, after Defense and rounding, and stopping at the health meter's minimum. 0 until it occurs.
        /// </summary>
        public int AppliedDamage { get; private set; }
        /// <summary>
        /// True once the damage has been dealt. After that it is off the timeline and does nothing more.
        /// </summary>
        public bool IsFinished { get; private set; }
        /// <summary>
        /// The time until the damage is dealt.
        /// </summary>
        public T TimeToEvent { get; set; }
        public PriorityRank? Priority { get; set; }
        /// <summary>
        /// Always a time, since the damage is at a known time. It can't be set here. Set the time with TimeToEvent.
        /// </summary>
        public override TimeOrNever<T> TimeRemaining { get => new(TimeToEvent); set { throw new InvalidOperationException("Set the time with TimeToEvent"); } }
        /// <summary>
        /// Always true, since it is about a specific time in the future.
        /// </summary>
        public override bool CouldOccur { get => true; set { throw new InvalidOperationException("An InstantDamageEvent always can occur."); } }
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
        /// Notifies others that this event is occurring, then deals the damage.
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
            AppliedDamage = MeterAppliedTo.ApplyInstantDamage(Magnitude);
            IsFinished = true;
        }
        #region Cloning
        /// <summary>
        /// Standard clone method for an InstantDamageEvent and subclasses. Subclasses override CreateEmptyForClone, and CopyPropertiesForClone if they have more to copy.
        /// </summary>
        /// <exception cref="UnrecoverableCloneException">Thrown when the owning timeline has not been cloned yet.</exception>
        public override object Clone(CloneContext context)
        {
            if (context.TryGet(this, out InstantDamageEvent<T> existingClone))
            {
                return existingClone;
            }
            if (!context.TryGet(OwningTimeline, out Timeline<T> owningTimelineClone))
            {
                throw new UnrecoverableCloneException("Owning timeline must be cloned before cloning any events.");
            }
            InstantDamageEvent<T> clone = CreateEmptyForClone(owningTimelineClone, context);
            context.Register(this, clone);
            CopyPropertiesForClone(clone, context);
            return clone;
        }
        /// <summary>
        /// Makes a new instance of this class for the cloned timeline, dealing the same damage to the clone of the health meter.
        /// A subclass overrides this to make one of itself.
        /// </summary>
        /// <param name="owningTimeline">The timeline that should own the clone</param>
        /// <param name="context">The clone context, for getting the clone of the health meter</param>
        protected virtual InstantDamageEvent<T> CreateEmptyForClone(Timeline<T> owningTimeline, CloneContext context)
        {
            return new InstantDamageEvent<T>(context.GetOrClone(MeterAppliedTo), Magnitude, TimeToEvent, Priority, owningTimeline);
        }
        /// <summary>
        /// Override this method (but always call base.CopyPropertiesForClone) in subclasses to copy any additional properties to the clone.
        /// </summary>
        /// <param name="clone">The new clone to modify. Already registered with the context</param>
        /// <param name="context">The clone context, in case you need to clone or reference clones of any other objects</param>
        protected virtual void CopyPropertiesForClone(InstantDamageEvent<T> clone, CloneContext context)
        {
            clone.UUID = UUID;
            clone.HaltsInstantAction = HaltsInstantAction;
            clone.AppliedDamage = AppliedDamage;
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
            return $"{GetType().Name} (UUID: {UUID}) of {Magnitude} before Defense on {MeterAppliedTo}, " + (IsFinished ? $"finished having taken {AppliedDamage}" : $"in {TimeToEvent}");
        }
    }
}
