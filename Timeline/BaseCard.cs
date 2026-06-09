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
    /// A basic "card" style event (as in, it holds how long in the future it is rather than computes it).
    /// Useful for unit tests, but you should probably use Card or a subclass, since a priority is useful.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public class BaseCard<T> : IOkazo<T>, IOkUUID where T : INumber<T>
    {
        /// <summary>
        /// A card can always occur, since it is about a specific time in the future.
        /// </summary>
        public bool CouldOccur { get => true; }
        /// <summary>
        /// The actual time until this event occurs. 
        /// Used to calculate the TimeRemaining, should be used instead of TimeRemaining whenever possible.
        /// </summary>
        public T TimeToEvent { get; set; }
        /// <summary>
        /// Always generates a time that is not Never, since this card is about a specific time in the future. Required by IOkazo<T>
        /// </summary>
        public TimeOrNever<T> TimeRemaining { get => new(TimeToEvent); }

        public event Occur<T>? Occurring;

        protected Timeline<T> OwningTimeline { get; }

        /// <summary>
        /// Guarentee that there's at least one difference between two cards
        /// </summary>
#if unitTest
        public int UUID {get; set;} = Guid.NewGuid().GetHashCode();
#else
        public int UUID { get; } = Guid.NewGuid().GetHashCode();
#endif

        public BaseCard(T timeToEvent, Timeline<T> owningTimeline)
        {
            TimeToEvent = timeToEvent;
            OwningTimeline = owningTimeline;
            OwningTimeline.Advance += OnAdvance;
            //Note that we won't automatically add the event, since sometimes we'll want to use AddPossibleEvent instead and AddEvent will error if we're not in the Open phase
        }

        public void OnAdvance(object? sender, T e)
        {
            if (sender != OwningTimeline)
            {
                throw new InvalidOperationException("Don't subscribe a card to a timeline it doesn't belong to.");
            }
            TimeToEvent -= e;
        }
        public int CompareTo(IOkazo<T>? other)
        {
            return ((IOkazo<T>)this).CompareTo(other);
        }
        /// <summary>
        /// Used to mark that this event is occuring. Should only be called by the timeline, 
        /// and only when TimeToEvent is zero, since otherwise the event shouldn't be occuring yet.
        /// </summary>
        /// <remarks>Note that this method is NOT virtual and doesn't call a virtual function.
        /// This is by design, since Cards should be informing other objects and not doing anything themselves.
        /// </remarks>
        /// <exception cref="InvalidOperationException">If the time isn't zero when triggering the event, thrown</exception>
        public void Trigger()
        {
            if (TimeToEvent != T.Zero)
            {
                throw new InvalidOperationException("Events should only occur at time zero.");
            }
            Occurring?.Invoke(this, this);
            OwningTimeline.Advance -= OnAdvance;
        }
        /// <summary>
        /// Any Subclass should override this method to provide a more specific equality check, since here we assume the other has to be BaseCard<T> to be equal
        /// </summary>
        /// <param name="other">The other IOkazo<T> to compare with. Won't be equal if it isn't a BaseCard<T></param>
        /// <returns></returns>
        public virtual bool Equals(IOkazo<T>? other)
        {
            if (other is BaseCard<T> otherCard)
            {
                return UUID == otherCard.UUID;
            }
            return false;
        }

        public override string ToString()
        {
            return $"BaseCard (UUID: {UUID}) with TimeToEvent: {TimeToEvent}";
        }
    }
}
