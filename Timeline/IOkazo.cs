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
    /// Used as an event, but in Esperato, since "event" is a reserved word.
    /// Comparison must sort by time remaining first, then by some other factor to ensure a deterministic order of events that occur at the same time. This is important to ensure that the timeline behaves predictably and that events are processed in a consistent order, even when they have the same time remaining.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public interface IOkazo<T> : IComparable<IOkazo<T>>, IEquatable<IOkazo<T>>, IAdvanceable<T> where T : INumber<T>, IFormattable
    {
        /// <summary>
        /// Amount of time until this event occurs. If this event is dynamic, this value may change based on other factors besides time advancing.
        /// If negative, time will be rewound to bring this to zero before the event occurs, ensuring that all timeline dependent factors show correctly while the event occurs, and any new events created by this event will be scheduled correctly relative to this event.
        /// If this is Never, there is no time associated with the event currently and it should be saved separately or removed.
        /// </summary>
        public TimeOrNever<T> TimeRemaining { get; }
        /// <summary>
        /// Only matters if TimeRemaining is Never. If true, this event could occur at some point in the future so it should be kept track of, but if false, this event will never occur and can be safely discarded.
        /// </summary>
        public bool CouldOccur { get; }
        /// <summary>
        /// If two events have the same time remaining, this method is used to determine which one should occur first.
        /// </summary>
        /// <param name="other">The other event</param>
        /// <returns>-1 if this event should take place before the other, +1 if after. Only zero if the other is this</returns>
        internal int Tiebreaker(IOkazo<T> other);

        /// <summary>
        /// Compares the current instance with another IOkazo<T> object and returns an integer that indicates their
        /// relative order based on time remaining and a tiebreaker.
        /// </summary>
        /// <remarks>The comparison first considers the TimeRemaining property. If the values are equal, a
        /// tiebreaker is used to determine the order. This is useful to sort what happens on the timeline.
        /// </remarks>
        /// <param name="other">The IOkazo<T> instance to compare with the current object. Cannot be null.</param>
        /// <returns>A value less than zero if the current instance should occur before the other; zero if they are equal;
        /// or greater than zero if the current instance should occur after.</returns>
        public new int CompareTo(IOkazo<T>? other)
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
            int genericComparison=OkazoComparitor<T>.Compare(this, other);
            if (genericComparison != 0)
            {
                return genericComparison;
            }
            return Tiebreaker(other);
        }

        /// <summary>
        /// Raised when this event occurs, allowing listeners to react to the event and update their state accordingly.
        /// </summary>
        public event Occur<T>? Occurring;

        /// <summary>
        /// Executes the event, rewinding time if necessary, and raises the Occurring event to notify listeners that the event has occurred and take other actions. 
        /// After this method is called, the event should be considered completed and should be removed from the timeline.
        /// Note that if this is a dynamic event, this might represent a state change, rather than an action to be taken so it might do nothing besides informing listeners.
        /// </summary>
        public void Trigger();
    }
}
