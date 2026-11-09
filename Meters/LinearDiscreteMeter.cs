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
        /// Advance the meter forward. Every time it hits DerivativeDenominator, it increases by derivative.
        /// </summary>
        /// <param name="sender">Must be the owning timeline</param>
        /// <param name="timeElapsed">How much time to move forward by.</param>
        public override void OnAdvance(object? sender, T timeElapsed)
        {
            base.OnAdvance(sender, timeElapsed); //Reset instant change and check sender.
            if (M.IsZero(Derivative))
            {
                return;
            }
            M oldValue = CurrentValue;
            T nextTimeSinceLastIncrement = TimeSinceLastIncrement + timeElapsed;

            // Floor division and positive modulo:
            // floor(n/d) and n mod d for n possibly negative.
            // This keeps TimeSinceLastIncrement in [0, Denominator) and applies
            // the correct number of increments (negative if rewinding).
            T remainder = ((nextTimeSinceLastIncrement % DerivativeDenominator) + DerivativeDenominator) % DerivativeDenominator;
            T floorQuotient = (nextTimeSinceLastIncrement - remainder) / DerivativeDenominator;

            TimeSinceLastIncrement = remainder;

            M numberOfIncrements = M.CreateTruncating(floorQuotient);
            _currentValue = M.Clamp(_currentValue + Derivative * numberOfIncrements, MinValue, MaxValue);
            RaiseContinuousChangeEvt(timeElapsed, oldValue, CurrentValue);
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
            M testSteps = distanceToMake /Derivative;
            M afterSteps = distanceToMake - testSteps * Derivative;
            if (M.Sign(afterSteps) * M.Sign(Derivative) < 0)
            {
                //If the sign product is negative, then we rounded towards 0, so the soonest before another change is one less
                return new(T.CreateChecked(testSteps) * DerivativeDenominator - DerivativeDenominator - TimeSinceLastIncrement);
            }
            if (M.IsZero(afterSteps))
            {
                //Exactly on the money
                return new(T.CreateChecked(testSteps) * DerivativeDenominator - TimeSinceLastIncrement);
            }
            //We rounded down, so we need to go one more
            return new(T.CreateChecked(testSteps) * DerivativeDenominator + 
                DerivativeDenominator - TimeSinceLastIncrement); //This part is to get to the next tick forward.
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
                return new(DerivativeDenominator - TimeSinceLastIncrement);
            }
            // The reach tick landed exactly if distanceToMake is an exact multiple of Derivative.
            // (This is the same condition that ThresholdReach checks internally as afterSteps == 0.)
            M remainder = distanceToMake % Derivative;
            if (M.IsZero(remainder))
            {
                // Exact landing: pass is one tick later.
                return new(reach.Time + DerivativeDenominator);
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
            spillover = Rational<long>.CreateChecked(Derivative)*
                (Rational<long>.CreateChecked(TimeSinceLastIncrement)/ Rational<long>.CreateChecked(DerivativeDenominator));

            //Now set everything
            _derivative=newDerivative;
            _derivativeDenominator=newDDenominator;
            TimeSinceLastIncrement = T.Zero;
            return true;
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
