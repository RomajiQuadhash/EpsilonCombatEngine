using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Timeline.OkazoOptionalProperties;

namespace Timeline
{
    public class Card<T,F>(T timeToEvent, Timeline<T> owningTimeline, PrioityRank? priority) : BaseCard<T>(timeToEvent, owningTimeline) where T : INumber<T>, IFormattable, IOkUUID,IOkPriority,IOkTiebreaker<T>
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

        /// <summary>
        /// Compares this card to another card for equality.
        /// Checks time, priority, face (if it supports equality), and UUID. If the other card is not a Card<T,F>, returns false.
        /// </summary>
        /// <param name="other">Other card.</param>
        /// <returns>True if the cards are equal, false otherwise.</returns>
        public override bool Equals(IOkazo<T>? other)
        {
            if (other is Card<T,F> otherCard)
            {
                if (TimeToEvent != otherCard.TimeToEvent)
                    return false;
                if (Priority != otherCard.Priority)
                    return false;
                if (Face is IEquatable<F> equatableFace && !equatableFace.Equals(otherCard.Face))
                    return false;
                return UUID == otherCard.UUID;
            }
            return false;
        }

        public override string ToString()
        {
            string faceData = $"face of type {nameof(F)}";
            if (Face is null)
            {
                faceData +=" that is null";
            } else if (Face is IFormattable or string)
            {
                faceData += $" has data:{Face}";
            }
            else
            {
                faceData += " that is not null but not printable";
            }
            return $"Card (UUID:{UUID}) with TimeToEvent: {TimeToEvent}, priority {Priority} and " + faceData;
        }
        #endregion
        /// <summary>
        /// Tries to tiebreak this card against another card after any standard comparisons (time, priority), but before last ditch comparisons (UUID)
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int Tiebreaker(IOkazo<T> other)
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
    }
}
