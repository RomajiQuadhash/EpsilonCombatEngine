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
    /// An event that can be never/not CouldOccur, but doesn't automatically update except for time advancing.
    /// Most dynamic events will be tied to Counters or other elements of the battle state, but this is useful for testing,
    /// and places where a Card isn't flexible enough.
    /// </summary>
    /// <remarks>If you're updating the TimeRemaining frequently, consider if a dedicated Dynamic event class should be made.</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public class PassiveDynamicEvent<T> : IOkazo<T>, IOkUUID where T : INumber<T>
    {
        /// <summary>
        /// The TimeOrNever representing how long until this event occurs.
        /// Needs to be set manually, but will automatically update when time advances.
        /// </summary>
        public TimeOrNever<T> TimeRemaining { get; set; }
        private bool _couldOccur;

        /// <summary>
        /// Only settable (to false) or matters if TimeRemaining is Never. 
        /// If true, this event could occur at some point in the future so it should be kept track of, but if false, this event will never occur and can be safely discarded.
        /// </summary>
        public bool CouldOccur {
            get { 
                if (TimeRemaining.IsNever)
                {
                    return _couldOccur;
                }
                return true;
            }
            set { 
                if (TimeRemaining.IsNever)
                {
                    _couldOccur=value;
                }
                if (value == true)
                {
                    _couldOccur=true;
                }
                throw new InvalidOperationException("An event with a time remaining will occur.");
            } }

        public event Occur<T>? Occurring;
        /// <summary>
        /// Guarentee that there's at least one difference between two events.
        /// UNLESS YOU'RE A UNIT TEST, NEVER SET THIS
        /// </summary>
        public int UUID { get; set; } = Guid.NewGuid().GetHashCode();
        /// <summary>
        /// Which timeline this event belongs to.
        /// Used for advancing this event.
        /// </summary>
        protected Timeline<T> OwningTimeline { get; }
        /// <summary>
        /// Main constructor. used when you have a specific time in mind for when this event should occur,
        /// or want to make very clear it isn't currently able to occur.
        /// </summary>
        /// <param name="timeRemaining">The time remaining until this event occurs.</param>
        /// <param name="couldOccur">If this event could happen. Ignored if timeRemaining is not Never</param>
        /// <param name="owningTimeline">The timeline used for this event.</param>
        public PassiveDynamicEvent(TimeOrNever<T> timeRemaining, bool couldOccur, Timeline<T> owningTimeline)
        {
            TimeRemaining = timeRemaining;
            _couldOccur = couldOccur;
            OwningTimeline = owningTimeline;
            OwningTimeline.Advance += OnAdvance;
            //Note that we won't automatically add the event, since sometimes we'll want to use AddPossibleEvent instead and AddEvent will error if we're not in the Open phase
        }
        /// <summary>
        /// Constructs an event, but assumes that the time is Never, and adds it to the timeline as a possible event.
        /// </summary>
        /// <param name="couldOccur"></param>
        /// <param name="owningTimeline"></param>
        public PassiveDynamicEvent(bool couldOccur, Timeline<T> owningTimeline):this(new TimeOrNever<T>() { IsNever = true }, couldOccur, owningTimeline)
        {
            //Since we're always going to be never, we can always use AddPossibleEvent, so we can add the event in the constructor.
            owningTimeline.AddPossibleEvent(this);
        }

        public void OnAdvance(object? sender, T e)
        {
            if (sender != OwningTimeline)
            {
                throw new InvalidOperationException("Don't subscribe an event to a timeline it doesn't belong to.");
            }
            if (TimeRemaining.IsNever)
            {
                return;
            }
            TimeRemaining.Time=TimeRemaining.Time - e;
        }
        /// <summary>
        /// If possible, invoke the Occuring event and remove this from the timeline's advance event.
        /// </summary>
        /// <exception cref="InvalidOperationException">If the time isn't zero, then can't trigger</exception>
        public void Trigger()
        {
            if(TimeRemaining.IsNever)
            {
                throw new InvalidOperationException("Can't trigger an event that will never occur.");
            }
            if (TimeRemaining.Time != T.Zero)
            {
                throw new InvalidOperationException("Events should only occur at time zero.");
            }
            Occurring?.Invoke(this, this);
            OwningTimeline.Advance -= OnAdvance;
        }
        
        public int CompareTo(IOkazo<T>? other)
        {
            return ((IOkazo<T>)this).CompareTo(other);
        }

        /// <summary>
        /// Simple UUID-based equality. Two events with the same UUID should be the same event, so if the other is a PassiveDynamicEvent<T> with the same UUID, we return true, otherwise false.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(IOkazo<T>? other)
        {
            if (other is PassiveDynamicEvent<T> otherEvent)
            {
                return UUID == otherEvent.UUID;
            }
            return false;
        }
        public override string ToString()
        {
            if (TimeRemaining.IsNever)
            {
                return $"PassiveDynamicEvent (UUID:{UUID}) without a time remaining. CouldOccur:{CouldOccur}";
            }
            return $"PassiveDynamicEvent (UUID:{UUID}) with {TimeRemaining} remaining";
        }
    }
}
