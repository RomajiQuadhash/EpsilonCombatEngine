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
    /// Useful for events that always will happen and storing some useful data.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="F">Type of the data stored on the card</typeparam>
    /// <param name="timeToEvent">How far in the future this card is</param>
    /// <param name="owningTimeline">What timeline this Card should be owned by</param>
    /// <param name="priority">The priority of this card</param>
    public class Card<T,F>(T timeToEvent, Timeline<T> owningTimeline, PrioityRank? priority) : BaseCard<T>(timeToEvent, owningTimeline), IOkUUID, IOkPriority, IOkTiebreaker<T> where T : INumber<T>
    {
        /// <summary>
        /// Priority of this card. If not present, treated as "None". Used as a tiebreaker for cards that occur at the same time. Lower numbers occur first. If this value is before "None", it will occur before any events without a priority. "None" and null are effectively the same, but prefer "None" to make it clear that the event was intentionally given no priority, rather than just forgetting to set a priority.
        /// </summary>
        public PrioityRank? Priority { get; set; } = priority;
        /// <summary>
        /// Data store to contain arbitrary information, that can be used for tiebreaking if it supports comparison.
        /// </summary>
        public F? Face { get; set; }

        public Card(T timeToEvent, Timeline<T> owningTimeline) : this(timeToEvent, owningTimeline, null) { }

        #region Overrides

        // Equals is inherited from BaseCard<T> and compares only type + UUID (identity).
        // For a "same kind of effect" / content comparison, compare the relevant properties
        // at the call site (or add a dedicated method), not via Equals.

        public override string ToString()
        {
            string faceData = $"face of type {nameof(F)}";
            if (Face is null)
            {
                faceData +=" that is null";
            } else
            {
                faceData += $" has data:{Face}";
            }
            return $"Card (UUID:{UUID}) with TimeToEvent: {TimeToEvent}, priority {Priority} and " + faceData;
        }
        #endregion
        /// <summary>
        /// Tries to tiebreak this card against another card after any standard comparisons (time, priority), but before last ditch comparisons (UUID)
        /// </summary>
        /// <param name="other">Another event. If not a Card with the same face type, the tiebreaker will not be applied.</param>
        /// <returns>negative if should come before other, 0 if inconclusive, positive if should come after other</returns>
        public int Tiebreaker(Okazo<T> other)
        {
            if (other is Card<T, F> otherCard)
            {
                if (Face is not null && otherCard.Face is null)
                {
                    return -1; // This card has a face and the other card doesn't, so this card goes first
                }
                if (Face is not null && Face is IComparable<F> compFace)
                {
                    return compFace.CompareTo(otherCard.Face);
                }

                if (Face is null && otherCard.Face is not null)
                {
                    return 1; // The other card has a face and this card doesn't, so the other card goes first
                }
            }
            return 0;
        }
        /// <summary>
        /// Creates a basic Card. In a subclass, this should be overridden to create a new instance of the subclass.
        /// </summary>
        /// <param name="timeToEvent"></param>
        /// <param name="owningTimeline"></param>
        /// <returns>A BaseCard (that is internally a Card)</returns>
        protected override BaseCard<T> CreateEmptyForClone(T timeToEvent, Timeline<T> owningTimeline)
        {
            return new Card<T, F>(timeToEvent, owningTimeline, Priority);
        }
        /// <summary>
        /// Copies all relevant properties from the current card instance to the specified clone, including the Face
        /// property if applicable.
        /// </summary>
        /// <param name="clone">The card instance to which properties are copied.</param>
        /// <param name="context">The context for the cloning operation.</param>
        /// <exception cref="UnrecoverableCloneException">Thrown when the cloned instance is not a Card somehow.</exception>
        protected override void CopyPropertiesForClone(BaseCard<T> clone, CloneContext context)
        {
            base.CopyPropertiesForClone(clone, context);
            if (clone is Card<T, F> cardClone)
            {
                if (Face is null)
                {
                    return;
                }
                if (Face is ICombatCloneable cloneableFace)
                {
                    cardClone.Face = (F)cloneableFace.Clone(context);
                }
                else 
                {
                    //If this face type doesn't implement ICombatCloneable and it isn't a value type, put specially handling here.
                    cardClone.Face = Face;
                }
                return; //Leave before we throw the exception, since the error didn't happen if we got here.
            }
            throw new UnrecoverableCloneException("Somehow, we cloned a card and got a different type back. This should never happen.");
        }
    }
}
