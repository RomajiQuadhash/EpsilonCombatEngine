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
    public class LinearContinousMeter<T, M>(Timeline<T> timeline, M initialValue, M minValue, M maxValue) : InstantChangeMeter<T, M>(timeline, initialValue, minValue, maxValue) where T : INumber<T> where M : INumber<M>
    {
        protected M _derivative = M.Zero;
        /// <summary>
        /// The rate of change that takes place in one time unit
        /// </summary>
        public override M Derivative => _derivative;
        #region Function Overrides
        /// <summary>
        /// Moves the meter accoring to the derivative. Also zeros out instant change
        /// </summary>
        /// <param name="sender">Must be the owning timeline</param>
        /// <param name="timeElapsed">How much time to move forward by.</param>
        public override void OnAdvance(object? sender, T timeElapsed)
        {
            base.OnAdvance(sender, timeElapsed); //handles resetting instant change and checking sender
            if (!M.IsZero(Derivative)) //No point if the derivative is zero
            {
                M nextValue = CurrentValue + 
                    (M.CreateChecked<T>(timeElapsed)*Derivative); //This is why M needs to be high precision, at least as much as T.

                //Make sure we don't spill out of the range
                _currentValue = M.Clamp(nextValue,MinValue,MaxValue);
                //We won't automatically cut off the derivative at the clamp point,since it makes it easier to cancel LERPS and changes over time
            }
        }
        /// <summary>
        /// Returns, given current properties, the time at which the meter will touch the specified threshold.
        /// Note that this is a prediction based on current properties and is possibly rounded if T is less precise than M
        /// </summary>
        /// <param name="threshold">The value to see how long it will take to reach</param>
        /// <param name="allowNegative">If true, will return a value in the past if the derivative is in the other direction from the target</param>
        /// <returns>The time at which the meter will touch the threshold or Never if it won't. 0 if already there</returns>
        public override TimeOrNever<T> ThresholdTouch(M threshold, bool allowNegative)
        {
            if (M.IsZero(Derivative))
            {
                return base.ThresholdTouch(threshold, allowNegative);
            }
            M distanceToMake = threshold - CurrentValue;
            if (M.IsZero(distanceToMake))
            {
                return new TimeOrNever<T>(false);
            }
            M divisonResult = distanceToMake / Derivative;
            if (!M.IsFinite(divisonResult)) //Likely will be short circuited since Derivative isn't 0, but good to be sure
            {
                return new TimeOrNever<T>(true);
            }
            if (M.IsPositive(divisonResult) || allowNegative)
            {
                return new TimeOrNever<T>(T.CreateTruncating(divisonResult));
            }
            //Otherwise we're negative and negatives are not allowed
            return new TimeOrNever<T>(true);
        }
        /// <summary>
        /// The same as ThresholdTouch, but returns never if we are at the threshold and not moving.
        /// It also counts if we jumped over the thresholf instantly
        /// </summary>
        /// <param name="threshold">The value to see how long it will take to pass</param>
        /// <param name="allowNegative">If true, will return a value in the past if the derivative is in the other direction from the target</param>
        /// <returns>The time at which the meter will cross the threshold or Never if it won't.</returns>
        public override TimeOrNever<T> ThresholdCross(M threshold, bool allowNegative)
        {
            if (M.IsZero(Derivative))
                return base.ThresholdCross(threshold, allowNegative);
            return ThresholdTouch(threshold, allowNegative);
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
            LinearContinousMeter<T,M> clone = new(owningTimelineClone, CurrentValue-InstantChange,MinValue,MaxValue);
            context.Register(this, clone);

            //Now update this to match
            clone.AddInstantChange(InstantChange); //Set instant change and bring CurrentValue back to where it is here
            clone.UUID = UUID;
            clone.SetDerivative(Derivative);
            return clone;
        }
        #endregion
        #region Mutators
        /// <summary>
        /// Sets the derivative (rate of change over time. This will wipe any other changes, so be careful with it
        /// </summary>
        /// <param name="newDerivative">What to make the Derivative</param>
        /// <returns>True. Subclasses will return false if the change is invalid and ignored</returns>
        public bool SetDerivative(M newDerivative)
        {
            _derivative=newDerivative;
            return true;
        }
        /// <summary>
        /// Adds a change to the derivativative. Use this more than setting it.
        /// </summary>
        /// <param name="changeOfDerivative">Amount to add to the</param>
        /// <returns></returns>
        public bool AddToDerivative(M changeOfDerivative)
        {
            _derivative += changeOfDerivative;
            return true;
        }
        /// <summary>
        /// Makes the changes needed for the Current Value to increase by totalChange in deltaTime time units.
        /// </summary>
        /// <remarks>The meter will not keep track of when the Lerp is finished. Use CancelLerp as an event after</remarks>
        /// <param name="totalChange"></param>
        /// <param name="deltaTime"></param>
        /// <param name="actualTotalChange">How much of the change is actually performed</param>
        /// <returns>True if the entire LERP can be performed, false means that the LERP as expressed will hit a wall</returns>
        /// <exception cref="ArgumentException">If deltaTime is negative</exception>
        public bool AddLerp(M totalChange, T deltaTime, out M actualTotalChange)
        {
            actualTotalChange = M.Clamp(CurrentValue + totalChange, MinValue, MaxValue)- CurrentValue;
            if (T.IsZero(deltaTime))
            {
                return AddInstantChange(totalChange);
            }
            if (T.IsNegative(deltaTime))
            {
                throw new ArgumentException($"deltaTime needs to be >=0, but it was {deltaTime} instead");
            }
            _derivative += totalChange / M.CreateTruncating<T>(deltaTime);
            //Return if we can do the full Lerp
            return actualTotalChange == totalChange;
        }
        /// <summary>
        /// Run after AddLerp when the Lerp is finished.
        /// </summary>
        /// <param name="totalChange"></param>
        /// <param name="deltaTime"></param>
        public void CancelLerp(M totalChange, T deltaTime)
        {
            AddLerp(M.Zero - totalChange, deltaTime, out M _);
        }
        #endregion
    }
}
