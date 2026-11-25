using CoreMetrics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Timeline.OkazoOptionalProperties;

namespace Timeline
{
    /// <summary>
    /// Used as an event, but in Esperanto, since "event" is a reserved word.
    /// Comparison must sort by time remaining first, then by some other factor to ensure a deterministic order of events that occur at the same time. This is important to ensure that the timeline behaves predictably and that events are processed in a consistent order, even when they have the same time remaining.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public abstract class Okazo<T> : IComparable<Okazo<T>>, IEquatable<Okazo<T>>, IAdvanceable<T>, ICombatCloneable, ICombatRewireable where T : INumber<T>
    {
        /// <summary>
        /// Amount of time until this event occurs. If this event is dynamic, this value may change based on other factors besides time advancing.
        /// If negative, time will be rewound to bring this to zero before the event occurs, ensuring that all timeline dependent factors show correctly while the event occurs, and any new events created by this event will be scheduled correctly relative to this event.
        /// If this is Never, there is no time associated with the event currently and it should be saved separately or removed.
        /// </summary>
        public abstract TimeOrNever<T> TimeRemaining { get; set; }
        /// <summary>
        /// Only matters if TimeRemaining is Never. If true, this event could occur at some point in the future so it should be kept track of, but if false, this event will never occur and can be safely discarded.
        /// </summary>
        public abstract bool CouldOccur { get; set; }
        /// <summary>
        /// If true, if this event would be an Instant Action, it instead makes the timeline move on to cleanup/display.
        /// This is useful if the event would set who should choose actions next, since the impacts of choosing that action
        /// would likely add events to the timeline and that can only be fully done in Open phase.
        /// </summary>
        public abstract bool HaltsInstantAction { get; set; }
        /// <summary>
        /// Guarantee that there's at least one difference between two events
        /// </summary>
        public abstract int UUID { get; set; }

        /// <summary>
        /// Compares the current instance with another Okazo<T> object and returns an integer that indicates their
        /// relative order based on time remaining and a tiebreaker.
        /// </summary>
        /// <remarks>The comparison first considers the TimeRemaining property. If the values are equal, then checks if the events are the same. If not, goes to the comparator.
        /// </remarks>
        /// <param name="other">The Okazo<T> instance to compare with the current object. Cannot be null.</param>
        /// <returns>A value less than zero if the current instance should occur before the other; zero if they are equal;
        /// or greater than zero if the current instance should occur after.</returns>
        public int CompareTo(Okazo<T>? other)
        {
            ArgumentNullException.ThrowIfNull(other);
            int timeComparison = TimeRemaining.CompareTo(other.TimeRemaining);
            if (timeComparison != 0)
            {
                return timeComparison;
            }
            if (this.Equals(other))
            {
                return 0;
            }
           return OkazoComparator<T>.Compare(this, other);
        }

        /// <summary>
        /// Raised when this event occurs, allowing listeners to react to the event and update their state accordingly.
        /// </summary>
        public event Occur<T>? Occurring;
        /// <summary>
        /// does Occurring.Invoke. Needed to raise it up higher
        /// </summary>
        protected void InvokeOccurring()
        {
            Occurring?.Invoke(this, this);
        }

        /// <summary>
        /// Executes the event, and raises the Occurring event to notify listeners that the event has occurred and take other actions. 
        /// After this method is called, the event should be considered completed and should be removed from the timeline.
        /// Note that if this is a dynamic event, this might represent a state change, rather than an action to be taken so it might do nothing besides informing listeners.
        /// </summary>
        public abstract void Trigger();

        /// <summary>
        /// Advance handler required by the timeline. Implementations should update internal timing based on timeline advancement.
        /// </summary>
        public abstract void OnAdvance(object? sender, T timeElapsed);

        /// <summary>
        /// Note that this is intentionally only UUID and type to match with what is being hashed.
        /// </summary>
        public bool Equals(Okazo<T>? other) =>
            other is not null && other.GetType() == GetType() && other.UUID == UUID;

        public override bool Equals(object? obj) => Equals(obj as Okazo<T>);

        public override int GetHashCode() => HashCode.Combine(GetType(), UUID);

        /// <summary>
        /// Derived types must be able to clone themselves with CloneContext for reference
        /// </summary>
        /// <param name="context">What other objects should be cloned</param>
        /// <returns>The clone of this object</returns>
        public abstract object Clone(CloneContext context);
        /// <summary>
        /// Rewires the subscribers related to this Okazo.
        /// </summary>
        /// <param name="context">The filled clone context</param>
        /// <exception cref="UnrecoverableCloneException">Thrown if this doesn't have a clone or if internal rewiring fails</exception>
        /// <remarks>Don't change this in subclasses. Instead, modify RewireOthers</remarks>
        public void Rewire(CloneContext context)
        {
            if (!context.TryGet(this, out Okazo<T> clone))
            {
                throw new UnrecoverableCloneException(
                    $"Rewire called on {GetType().Name} ({this}) that has no clone registered.");
            }
            //Ensure we always relink Occurring
            RewireOccurring(context, clone);
            //Allow extensibility for subclasses.
            RewireOthers(context, clone);
        }
        
        private void RewireOccurring(CloneContext context, Okazo<T> clone)
        {
            if (Occurring is null) return;  // No extrinsic subscribers.

            foreach (var handler in Occurring.GetInvocationList())
            {
                if (handler.Target is null)
                {
                    throw new UnrecoverableCloneException(
                        $"{handler.Method} is a static method and should not be subscribed to Occurring");
                }

                if (!context.TryGet(handler.Target, out object subscriberClone))
                {
                    throw new UnrecoverableCloneException(
                        $"Subscriber to Occurring {handler.Target} has no clone registered. " +
                        "This suggests the cloning tree failed to reach all elements.");
                }

                var clonedHandler = (Occur<T>)Delegate.CreateDelegate(
                    typeof(Occur<T>), subscriberClone, handler.Method);
                clone.Occurring += clonedHandler;
            }
        }
        /// <summary>
        /// Establishes links for any additional events this Okazo has
        /// </summary>
        /// <param name="context">The fully populated clone dictionary</param>
        /// <param name="clone">The clone of this event</param>
        /// <exception cref="UnrecoverableCloneException">Thrown if there are any issues with finding clones of the subscribers</exception>
        /// <remarks>In the rare case you implement something here, any subclasses should call base or you should seal it and provide a new empty protected virtual method. But that's probably a sign things have gotten messy.</remarks>
        protected virtual void RewireOthers(CloneContext context, Okazo<T> clone)
        {

        }

        /// <summary>
        /// Gets all the objects looking at Occurring.
        /// If you're not a unit test or debug menu, you shouldn't be calling this.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Thrown if a static method is subscribed to Occurring (which would fail when cloned)</exception>
        public IReadOnlyList<object> GetOccurringSubscribers()
        {
            if (Occurring is null) return [];
            List<object> result = [];
            foreach (var d in Occurring.GetInvocationList())
            {
                if (d.Target is not null) result.Add(d.Target);
                else
                {
                    throw new InvalidOperationException($"Static method {d.Method} is subscribed to Occurring, which is not supported.");
                }
            }
            return result;
        }
    }
}
