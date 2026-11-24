using CoreMetrics;
using Rationalz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Timeline;

namespace Meters
{
    /// <summary>
    /// A meter that changes in discrete steps over time.
    /// Use instead of LinearContinuousMeter if your meter is an integer type.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for this meter's value. Integer</typeparam>
    /// <param name="timeline">The timeline this meter is associated with</param>
    /// <param name="initialValue">The initial value of this meter</param>
    /// <param name="minValue">The minimum value of this meter</param>
    /// <param name="maxValue">The maximum value of this meter</param>
    public class LinearDiscreteMeter<T, M>(Timeline<T> timeline, M initialValue, M minValue, M maxValue) : InstantChangeMeter<T,M>(timeline, initialValue, minValue, maxValue) where T : INumber<T> where M : IBinaryInteger<M>
    {
        private M _derivative = M.Zero;
        private T _derivativeDenominator = T.One;
        /// <summary>
        /// Determines how long until the next time Derivative is added. 
        /// In range [0,DerivativeDenominator)
        /// </summary>
        public T TimeSinceLastIncrement { get; protected set; } = T.Zero;
        /// <summary>
        /// How much the meter changes when TimeSinceLastIncrement reaches DerivativeDenominator?
        /// </summary>
        public override M Derivative => _derivative;
        /// <summary>
        /// How much time it takes between updates?
        /// </summary>
        public override T DerivativeDenominator => _derivativeDenominator;
        #region Function Overrides
        /// <summary>
        /// Everything about a discrete meter that moving time forward can change.
        /// AdvanceTicks takes one of these and gives back another, so it can be asked "what would happen" without anything actually happening.
        /// </summary>
        /// <param name="Value">The value of the meter</param>
        /// <param name="TimeSinceLastIncrement">How far into the tick in progress it is. In range [0,DerivativeDenominator)</param>
        /// <param name="Derivative">How much each tick adds</param>
        /// <param name="DerivativeDenominator">How much time is between ticks</param>
        /// <param name="TicksLanded">How many ticks have landed since the state the advance started from. Negative if it rewound past some</param>
        protected readonly record struct TickState(M Value, T TimeSinceLastIncrement, M Derivative, T DerivativeDenominator, M TicksLanded);
        /// <summary>
        /// The meter as it is right now, in the form AdvanceTicks works with.
        /// </summary>
        protected TickState CurrentTickState => new(_currentValue, TimeSinceLastIncrement, _derivative, _derivativeDenominator, M.Zero);
        /// <summary>
        /// Advance the meter forward. Every time it hits DerivativeDenominator, it increases by derivative.
        /// </summary>
        /// <param name="sender">Must be the owning timeline</param>
        /// <param name="timeElapsed">How much time to move forward by.</param>
        public override void OnAdvance(object? sender, T timeElapsed)
        {
            base.OnAdvance(sender, timeElapsed); //Reset instant change and check sender.
            M oldValue = CurrentValue;
            TickState start = CurrentTickState;
            CommitTickState(timeElapsed, start, AdvanceTicks(timeElapsed, start));
            RaiseContinuousChangeEvt(timeElapsed, oldValue, CurrentValue); //Does nothing if the value didn't change
        }
        /// <summary>
        /// Works out what the meter would be after the time provided, starting from the state provided.
        /// Changes nothing. OnAdvance passes the result to CommitTickState, which is the only thing that makes it real.
        /// Subclasses that change the derivative partway through an advance override this and call the base once per piece,
        /// passing the state each piece returns into the next.
        /// </summary>
        /// <param name="timeElapsed">How much time to move forward by. Negative when rewinding</param>
        /// <param name="start">The state of the meter before this time passes</param>
        /// <returns>The state of the meter after this time passes</returns>
        protected virtual TickState AdvanceTicks(T timeElapsed, TickState start)
        {
            if (M.IsZero(start.Derivative))
            {
                return start;
            }
            T nextTimeSinceLastIncrement = start.TimeSinceLastIncrement + timeElapsed;

            // Floor division and positive modulo:
            // floor(n/d) and n mod d for n possibly negative.
            // This keeps TimeSinceLastIncrement in [0, Denominator) and applies
            // the correct number of increments (negative if rewinding).
            T remainder = ((nextTimeSinceLastIncrement % start.DerivativeDenominator) + start.DerivativeDenominator) % start.DerivativeDenominator;
            T floorQuotient = (nextTimeSinceLastIncrement - remainder) / start.DerivativeDenominator;
            M numberOfIncrements = M.CreateTruncating(floorQuotient);

            // If the time elapsed is exactly what TimeUntilTick said a tick was away, land exactly on that tick.
            // With a floating point T, the addition and modulo above can come out a hair to either side of it.
            // The neighbors are checked since that hair can also push the count off: just missing the tick loses one,
            // and the division then landing just under a whole number loses another when it is truncated.
            M oneFewer = numberOfIncrements - M.One;
            M oneMore = numberOfIncrements + M.One;
            M twoMore = oneMore + M.One;
            if (timeElapsed == TimeUntilTick(numberOfIncrements, start))
            {
                remainder = T.Zero;
            }
            else if (timeElapsed == TimeUntilTick(oneMore, start))
            {
                numberOfIncrements = oneMore;
                remainder = T.Zero;
            }
            else if (timeElapsed == TimeUntilTick(twoMore, start))
            {
                numberOfIncrements = twoMore;
                remainder = T.Zero;
            }
            else if (timeElapsed == TimeUntilTick(oneFewer, start)) //Technically off if M is unsigned but if M was unsigned, the meter couldn't decrease.
            {
                numberOfIncrements = oneFewer;
                remainder = T.Zero;
            }

            return start with
            {
                Value = M.Clamp(start.Value + start.Derivative * numberOfIncrements, MinValue, MaxValue),
                TimeSinceLastIncrement = remainder,
                TicksLanded = start.TicksLanded + numberOfIncrements
            };
        }
        /// <summary>
        /// Makes the result of AdvanceTicks real. Subclasses with more state that an advance changes override this and call the base.
        /// </summary>
        /// <param name="timeElapsed">How much time the advance moved forward by</param>
        /// <param name="start">The state before the advance</param>
        /// <param name="end">The state AdvanceTicks worked out for after the advance</param>
        protected virtual void CommitTickState(T timeElapsed, TickState start, TickState end)
        {
            _currentValue = end.Value;
            TimeSinceLastIncrement = end.TimeSinceLastIncrement;
            _derivative = end.Derivative;
            _derivativeDenominator = end.DerivativeDenominator;
        }
        /// <summary>
        /// How long from now until a tick lands, counting from the next one.
        /// Every prediction of a tick's time must come from here, since AdvanceTicks compares against this exact calculation.
        /// </summary>
        /// <param name="ticksFromNow">1 for the next tick, 2 for the one after. 0 is the tick that most recently landed and negatives are before that</param>
        /// <returns>The time until that tick. Negative or zero if it is in the past</returns>
        protected T TimeUntilTick(M ticksFromNow)
        {
            return TimeUntilTick(ticksFromNow, CurrentTickState);
        }
        /// <summary>
        /// The same as TimeUntilTick, but for a state that might not be the meter's real one.
        /// </summary>
        protected static T TimeUntilTick(M ticksFromNow, TickState state)
        {
            return T.CreateChecked(ticksFromNow) * state.DerivativeDenominator - state.TimeSinceLastIncrement;
        }
        /// <summary>
        /// Returns, given current properties, the time at which the meter will reach the specified threshold.
        /// "reach" is equaling, instantly jumping past, or being the first tick after passing.
        /// </summary>
        /// <param name="threshold">The value to see how long it will take to reach</param>
        /// <param name="allowNegative">Allow negative values. Will be the earliest value it stays equal to the threshold or just past it.
        /// If false, if moving away, then Never</param>
        /// <returns>The time at which the meter will reach the threshold or Never if it won't. 0 if already there (and not allowing negative)</returns>
        public override TimeOrNever<T> ThresholdReach(M threshold, bool allowNegative)
        {
            if (IsOutsideRange(threshold))
            {
                //The meter stops at its minimum and maximum, so it can never get there
                return new(true);
            }
            if (M.IsZero(Derivative))
            {
                return base.ThresholdReach(threshold, allowNegative);
            }
            
            M distanceToMake = threshold - CurrentValue;
            if (M.IsZero(distanceToMake))
            {
                if (allowNegative)
                {
                    //If we're allowing negative, the soonest was when we just ticked over.
                    return new(-TimeSinceLastIncrement);
                } else
                {
                    return new(false);
                }
            }
            if (M.Sign(distanceToMake)*M.Sign(Derivative)<0 && !allowNegative)
            {
                //Not allowed to use final sign negative.
                return new(true);
            }
            M testSteps = distanceToMake / Derivative;
            M afterSteps = distanceToMake - testSteps * Derivative;
            //The tick that reaches the threshold is the first one that makes up the whole distance, so the division rounded up.
            //testSteps is that division rounded toward zero instead, which is only different when the threshold is ahead and isn't landed on exactly.
            if (M.Sign(afterSteps) * M.Sign(Derivative) > 0)
            {
                //The threshold is ahead and testSteps ticks fall short of it, so it takes one more.
                return new(TimeUntilTick(testSteps + M.One));
            }
            //Either exactly on the money, or the threshold is behind us (only when allowing negative).
            //Behind us, rounding toward zero already is rounding up: with 97 -> 102 past 100, the tick that just landed (0) is the one that reached it.
            return new(TimeUntilTick(testSteps));
        }
        /// <summary>
        /// Almost the same as ThresholdReach, but with a few differences:
        /// 1. If the meter isn't moving, it will return Never instead of 0 if the meter is already at the threshold.
        /// 2. If a tick lands exactly on the threshold, it will be the next tick instead
        /// </summary>
        /// <param name="threshold">The value to see how long it will take to pass</param>
        /// <param name="allowNegative">Allow negative values. Will be the latest value it stays equal to the threshold or just past it.
        /// If false, if moving away, then Never</param>
        /// <returns>The time at which the meter will pass the threshold or Never if it won't. Next tick if already at the threshold and moving</returns>
        public override TimeOrNever<T> ThresholdPass(M threshold, bool allowNegative)
        {
            if (IsOutsideRange(threshold))
            {
                //The meter stops at its minimum and maximum, so it can never get there
                return new(true);
            }
            if (M.IsZero(Derivative))
            {
                return base.ThresholdPass(threshold, allowNegative);
            }
            TimeOrNever<T> reach = ThresholdReach(threshold, allowNegative);
            if (reach.IsNever) return reach;

            // Check if the reach tick landed exactly on the threshold.
            // If so, the pass is the next tick (one more increment in the direction of the derivative).
            M distanceToMake = threshold - CurrentValue;
            if (M.IsZero(distanceToMake))
            {
                // Already at the threshold. The "pass" is the next tick where it leaves,
                // if the meter is moving. If not moving, base handles it (returns Never).
                return new(TimeUntilTick(M.One));
            }
            // The reach tick landed exactly if distanceToMake is an exact multiple of Derivative.
            // (This is the same condition that ThresholdReach checks internally as afterSteps == 0.)
            M remainder = distanceToMake % Derivative;
            if (M.IsZero(remainder))
            {
                // Exact landing: pass is one tick later.
                return new(TimeUntilTick(distanceToMake / Derivative + M.One));
            }
            // Skipped over: pass == reach.
            return reach;
        }
        public override object Clone(CloneContext context)
        {
            if (context.TryGet(this, out var existingClone))
            {
                return existingClone;
            }
            if (!context.TryGet(OwningTimeline, out Timeline<T> owningTimelineClone))
            {
                throw new UnrecoverableCloneException("Owning timeline must be cloned before cloning any meters.");
            }
            LinearDiscreteMeter<T,M> clone = new(owningTimelineClone, CurrentValue-InstantChange,MinValue,MaxValue);
            context.Register(this, clone);

            //Now update this to match
            clone.AddInstantChange(InstantChange); //Set instant change and bring CurrentValue back to where it is here
            clone.UUID = UUID;
            clone.SetDerivative(Derivative, DerivativeDenominator, out _);
            clone.SetTimeSinceLastIncrement(TimeSinceLastIncrement);
            return clone;
        }
        public override string ToString()
        {
            return base.ToString()+ $" with derivative {Derivative}, progress of {TimeSinceLastIncrement}/{DerivativeDenominator}";
        }
        #endregion
        #region Mutators
        /// <summary>
        /// Sets both parts of the derivative
        /// </summary>
        /// <param name="newDerivative">Derivative (how much per tick)</param>
        /// <param name="newDDenominator">How often to change the value. >0</param>
        /// <param name="spillover">How much is leftover.</param>
        /// <returns>True. Subclasses might send false if it fails (or it is illegal)</returns>
        /// <exception cref="ArgumentException">What happens if you set a derivative less than 0</exception>
        public virtual bool SetDerivative(M newDerivative, T newDDenominator, out Rational<long> spillover)
        {
            if (newDDenominator <= T.Zero)
            {
                throw new ArgumentException($"Denominator must be positive, not {newDDenominator}");
            }
            //Save what would spill over. Make everything a rational, including the time in case it is an integer/low precision
            //Done in a wider type so the multiplication has room, then rounded back to a small denominator.
            spillover = RationalConversion.LimitDenominator(
                RationalConversion.Widen(RationalConversion.ToRational(Derivative)) *
                (RationalConversion.Widen(RationalConversion.ToRational(TimeSinceLastIncrement)) / RationalConversion.Widen(RationalConversion.ToRational(DerivativeDenominator))));

            //Now set everything
            _derivative=newDerivative;
            _derivativeDenominator=newDDenominator;
            TimeSinceLastIncrement = T.Zero;
            return true;
        }
        /// <summary>
        /// Changes how much the tick in progress (and every later one) will add, without restarting that tick.
        /// Nothing spills over, since the tick still lands when it was going to.
        /// </summary>
        /// <param name="newDerivative">Derivative (how much per tick)</param>
        /// <remarks>If the derivative is currently 0 there is no tick in progress to keep, so use SetDerivative instead</remarks>
        protected void SetDerivativeKeepingPhase(M newDerivative)
        {
            _derivative = newDerivative;
        }
        /// <summary>
        /// Sets TimeSinceLastIncrement in a safe way. It is protected since you shouldn't be doing this if you're not doing something like cloning
        /// </summary>
        /// <param name="newTimeSinceLastIncrement">What to set the time to. Must be in [0,DerivativeDenominator)</param>
        /// <returns>True if set (false if setting it to that value is illegal)</returns>
        protected bool SetTimeSinceLastIncrement(T newTimeSinceLastIncrement)
        {
            if (T.IsNegative(newTimeSinceLastIncrement))
            {
                return false;
            }
            if (newTimeSinceLastIncrement >= DerivativeDenominator)
            {
                return false;
            }
            TimeSinceLastIncrement = newTimeSinceLastIncrement;
            return true;
        }
        #endregion
    }
}
