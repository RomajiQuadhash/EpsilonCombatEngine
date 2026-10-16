using CoreMetrics;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Timeline;

namespace Meters
{
    public class InstantChangeMeter<T, M> : AbstractMeter<T,M> where T : INumber<T> where M : INumber<M>
    {

        protected M _currentValue;
        private M _minValue;
        private M _maxValue;
        private M _instantChange;
        public override Timeline<T> OwningTimeline { get; init; }
        public override M CurrentValue => _currentValue;
        /// <summary>
        /// Always 0 since no contininous change will ever apply
        /// </summary>
        public override M Derivative => M.Zero;
        public override T DerivativeDenominator => T.One;
        public override M InstantChange => _instantChange;
        public override M MinValue => _minValue;
        public override M MaxValue => _maxValue;

        public InstantChangeMeter(Timeline<T> timeline, M initialValue,  M minValue, M maxValue)
        {
            OwningTimeline=timeline;
            _minValue=minValue;
            if (maxValue<=minValue)
            {
                throw new ArgumentException($"The max value ({maxValue})must be strictly larger than the min value ({minValue})");
            }
            _maxValue =maxValue;
            if (maxValue<initialValue || minValue>initialValue)
            {
                throw new ArgumentException($"Inital value ({initialValue}) is not in [{minValue},{maxValue}]. Invalid");
            }
            _currentValue =initialValue;
            _instantChange=M.Zero;
            timeline.Advance += OnAdvance;
        }
        #region Function Overrides
        /// <summary>
        /// Since this doesn't have continuous changes, all it does is zero out the instant change
        /// </summary>
        /// <param name="sender">Must be the owning timeline</param>
        /// <param name="timeElapsed">Ignored</param>
        /// <exception cref="InvalidOperationException">Your prize for subscribing this to a timeline it shouldn't be on</exception>
        public override void OnAdvance(object? sender, T timeElapsed)
        {
            if (sender != OwningTimeline)
            {
                throw new InvalidOperationException("Don't subscribe a meter to a timeline it doesn't belong to");
            }
            _instantChange = M.Zero;
        }
        /// <summary>
        /// When will we "touch" a threshold? Given that our value only jumps, this just becomes comparing the threshold and current value
        /// </summary>
        /// <param name="threshold">Threshold to compare against</param>
        /// <param name="allowNegative">Ignored</param>
        /// <remarks>Since we only ever move in discrete jumps, touching only happens if we are at a point exactly</remarks>
        /// <returns>Never if we aren't there or 0 if we are</returns>
        public override TimeOrNever<T> ThresholdTouch(M threshold, bool allowNegative)
        {
            return new TimeOrNever<T>(threshold != CurrentValue);
        }
        /// <summary>
        /// Wrapper for DidInstantlyCross given there are no gradual changes
        /// </summary>
        /// <param name="threshold">What threshold did this cross?</param>
        /// <param name="allowNegative">Ignored</param>
        /// <returns>0 if we did instantly cross, Never otherwise</returns>
        public override TimeOrNever<T> ThresholdCross(M threshold, bool allowNegative)
        {
            return new TimeOrNever<T>(!DidInstantlyCross(threshold,false));
        }
        public override object Clone(CloneContext context)
        {
            if (context.TryGet(this,out var existingClone))
            {
                return existingClone;
            }
            if (!context.TryGet(OwningTimeline, out Timeline<T> owningTimelineClone))
            {
                throw new UnrecoverableCloneException("Owning timeline must be cloned before cloning any meters.");
            }
            InstantChangeMeter<T,M> clone = new(owningTimelineClone, CurrentValue-InstantChange,MinValue,MaxValue);
            context.Register(this, clone);

            //Now update this to match
            clone.AddInstantChange(InstantChange); //Set instant change and bring CurrentValue back to where it is here
            clone.UUID = UUID;
            return clone;
        }
        #endregion
        #region Mutators
        public bool UpdateMin(M newMin, bool allowClamping)
        {
            if (newMin >= MaxValue)
            {
                throw new ArgumentException($"New min {newMin} must be strictly less than the maximum ({MaxValue})");
            }
            if (newMin <= CurrentValue)
            {
                _minValue = newMin;
                return true;
            }
            if (allowClamping)
            {
                _currentValue = newMin;
                _minValue = newMin;
                return true;
            }
            //We'd have to clamp and we're not allowed to, just return false
            return false;
        }

        public bool UpdateMax(M newMax, bool allowClamping)
        {
            if (newMax <= MinValue)
            {
                throw new ArgumentException($"New max {newMax} must be strictly greater than the minimum ({MinValue})");
            }
            if (newMax >= CurrentValue)
            {
                _maxValue = newMax;
                return true;
            }
            if (allowClamping)
            {
                _currentValue = newMax;
                _maxValue = newMax;
                return true;
            }
            //Have to clamp to set it but we're not allowed to, just return false
            return false;
        }
        /// <summary>
        /// Adds the given instant change to the value
        /// </summary>
        /// <param name="additionalChange">What change we wish to apply</param>
        /// <returns>True if the whole additional instant change can be applied, false otherwise.</returns>
        public bool AddInstantChange(M additionalChange)
        {
            M afterState = CurrentValue + additionalChange;
            if (afterState>MaxValue)
            {
                _instantChange += MaxValue - CurrentValue; //amount needed to hit the maximum
                _currentValue= MaxValue;
                return false;
            }
            if (afterState<MinValue)
            {
                _instantChange += MinValue - CurrentValue; //Add how much decrease there is to the existing
                _currentValue= MinValue;
                return false;
            }
            _instantChange += additionalChange;
            _currentValue = afterState;
            return true;
        }
        #endregion

    }
}
