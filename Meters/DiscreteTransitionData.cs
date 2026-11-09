using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Timeline.OkazoOptionalProperties;

namespace Meters
{
    /// <summary>
    /// An extension of TransitionData that is used for discrete meters, which have a tick rate rather than a continuous derivative.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for the associated meter. Integer</typeparam>
    public class DiscreteTransitionData<T,M> : TransitionData<M> where T : INumber<T> where M : INumber<M>
    {
        /// <summary>
        /// How often the meter having this transition ticks. (usually, DerivativeDenominator of the meter, but possibly not always)
        /// ProgressiveChange (derivative) isn't comparable if the tick rates are different, so we prioritize the faster tick rate first
        /// </summary>
        public T TickRate { get; protected init; }
        /// <summary>
        /// When this transition was created, did it just tick?
        /// If not, then the progressive change didn't actually change anything when this transition happened.
        /// </summary>
        /// <remarks>This is why InstantChange is meaningful here as something separate from After-Before (which is the same for a continuous meter)</remarks>
        public bool JustTicked { get; protected init; }
        /// <summary>
        /// What was the instant change at the time of crossing the threshold? 
        /// Almost always positive, but it can be negative if the instant change was in the opposite direction of the threshold crossing (only possible if JustTicked is true and the progressiveChange overpowered the instantChange)
        /// </summary>
        public M InstantChange { get; protected init; }
        public DiscreteTransitionData(M instantChange, M after, M progressiveChange, M range, M threshold, T tickRate, bool justTicked) : base(instantChange + (justTicked ? progressiveChange : M.Zero), after, progressiveChange, range, threshold)
        {
            if (tickRate <= T.Zero)
            {
                throw new ArgumentException("Tick rate must be greater than zero", nameof(tickRate));
            }
            TickRate = tickRate;
            JustTicked = justTicked;
            if (JustTicked && M.IsZero(instantChange+progressiveChange))
            {
                //This would mean that we somehow haven't moved at all, since we got instantly moved and progressively moved at once.
                throw new ArgumentException("No change occurred, shouldn't be a transition");
            }
            //we store this separately since Ticking "looks like" an instant change to lower down, but we want to keep it separate for comparison purposes.
            if (JustTicked && M.Sign(instantChange) != M.Sign(instantChange+progressiveChange))
            {
                //Now we're in the situation where the overall crossing is in the opposite direction of the instant change (only possible if we just ticked)
                InstantChange = -M.Abs(instantChange); //In this case, our instant change is in the opposite direction of the threshold crossing, so negative.
            }
            else
            {
                InstantChange = M.Abs(instantChange); //Assume the instant change is in the direction of the threshold crossing otherwise.
            }
        }
        public override int CompareTo(TransitionData<M>? other)
        {
            ArgumentNullException.ThrowIfNull(other);

            if (other is not DiscreteTransitionData<T,M> discreteOther)
            {
                //There is exactly one case where we're okay: If the other doesn't have a progressive change, since that's from an instant change meter, which must use a type that doesn't give us a tick rate.
                if (other.ProgressiveChange == M.Zero)
                {
                    //This isn't perfect because we lose the distinction between true instant changes and ticks
                    return base.CompareTo(other);
                }
                throw new InvalidOperationException($"{other.GetType().Name} is using an integer type, has a progressive change, but is not using {nameof(DiscreteTransitionData<T,M>)}. This is because you're using the wrong event type, likely because you're using the wrong meter type.");
            }

            // Tier 1: Bigger instant change wins (scaled by range)
            M myScaledInstantChange = InstantChange * other.Range;
            M otherScaledInstantChange = discreteOther.InstantChange * Range;
            int instantComparison = -myScaledInstantChange.CompareTo(otherScaledInstantChange);//Negate because bigger instant change should come first
            //Note that if there's an instant change that's in the opposite direction of the threshold crossing, it will be negative, and thus will lose to no instant change. This is intended behavior, since it's like we're warping away from the threshold crossing and so less "instant" than no warping at all.
            if (instantComparison != 0) return instantComparison;

            // Tier 2: Faster tick rate wins
            int tickComparison = TickRate.CompareTo(discreteOther.TickRate); //timeline units, no range conversion, smaller means faster tick rate
            if (tickComparison != 0) return tickComparison;

            // Tier 3: Base class comparison.
            return base.CompareTo(other);
        }
    }
}
