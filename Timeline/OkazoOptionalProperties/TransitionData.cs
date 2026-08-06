using CoreMetrics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Timeline.OkazoOptionalProperties
{
    /// <summary>
    /// Holds data about a transition across a threshold, used to determine the priority of events triggered by crossing a threshold.
    /// Order of priorities:
    /// 1. Furthest across the threshold/size of the change
    /// 2. Furthest across the threshold
    /// 3. Furthest behind the threshold before the crossing
    /// 4. Largest progressive change at the time of crossing
    /// </summary>
    /// <typeparam name="T">The numeric type used for the metric. Note that this will be multiplied by other ranges, so make sure that'll fit in the scaling of types</typeparam>
    public class TransitionData<T>: IComparable<TransitionData<T>> where T : INumber<T> 
    {
        /// <summary>
        /// The value before crossing the threshold. Always <=0, with 0 meaning no instant change.
        /// If it is negative, this is the amount behind the threshold the meter was before crossing the threshold.
        /// Not scaled down by the range, so - range is going from one end of the range to the other.
        /// </summary>
        public T Before { get; private set; }
        /// <summary>
        /// The value after crossing the threshold. Always >=0, with 0 meaning no instant change.
        /// If it is positive, this is the amount past the threshold the meter was after crossing the threshold.
        /// Not scaled down by the range so range is the max value and a range worth of overshoot.
        /// </summary>
        public T After { get; private set; }
        /// <summary>
        /// What the progressive change of the meter was at the time of crossing the threshold, not scaled down by the range.
        /// Usually positive, but could be negative if an instant change went in the opposite direction of the progressive change.
        /// </summary>
        public T ProgressiveChange { get; private set; }
        /// <summary>
        /// The range of the meter, used to compare with other values
        /// </summary>
        public T Range { get; private set; }
        /// <summary>
        /// Converts the raw data about a threshold crossing into standard values to compare.
        /// </summary>
        /// <param name="instantChange">The instant change that caused crossing the threshold</param>
        /// <param name="after">The value after crossing the threshold, before being capped by the range</param>
        /// <param name="progressiveChange">The progressive change at the time of crossing the threshold</param>
        /// <param name="range">The range of the meter</param>
        /// <param name="threshold">The threshold value</param>
        /// <exception cref="ArgumentException">If the range is less than or equal to zero</exception>
        /// <exception cref="InvalidOperationException">If the after value didn't cross or reach the threshold</exception>
        public TransitionData(T instantChange, T after, T progressiveChange,T range,T threshold)
        {
            if (range <= T.Zero)
            {
                throw new ArgumentException("Range must be greater than zero", nameof(range));
            }
            //Range will always be set, so do it before the if statements to avoid having to set it in multiple places.
            Range = range;

            if (instantChange == T.Zero)
            {
                //No instant change, so just absolute value the progressive change and scale by the range.
                ProgressiveChange = T.Abs(progressiveChange);
                Before=T.Zero;
                After=T.Zero;
                return;
            }
            if (instantChange < T.Zero)
            {
                //Normalize so all changes are increasing.
                after = -after;
                instantChange = T.Abs(instantChange);
                threshold = -threshold;
                progressiveChange = -progressiveChange;
            }
            if (after<threshold) //Note that instantChange is guaranteed to be positive here and in the same direction as the overall change, which is why we can just check if after<threshold to see if it crossed the threshold.
            {
                throw new InvalidOperationException("After value didn't cross the threshold");
            }
            After = (after-threshold);
            Before = After-(instantChange);
            ProgressiveChange = progressiveChange;
        }

        public int CompareTo(TransitionData<T>? other)
        {
            ArgumentNullException.ThrowIfNull(other);
            
            //First, prioritize crossings that overshot the threshold if the other didn't
            if (After==T.Zero && other.After > T.Zero)
            {
                return 1;
            } else if (After > T.Zero && other.After == T.Zero)
            {
                return -1;
            }
            //We'll need these scaled to the same range regardless of whether they overshot or not, so scale them to the other range for comparison.
            T beforeScaledToOtherRange = Before * other.Range;
            T otherBeforeScaledToThisRange = other.Before * Range;
            //Now, both are either overshooting or not. First, handle if both are overshooting
            if (After > T.Zero && other.After > T.Zero)
            {
                //Note that we don't have the two values scaled to the same range, so we need to scale them to the same range before comparing.
                //To avoid requiring fractional math, we can just cross multiply to compare.
                T afterScaledToOtherRange = After * other.Range;
                T otherAfterScaledToThisRange = other.After * Range;
                
                //Instead of computing ratios, we can just cross multiply to avoid needing fractional math.
                T thisAfterByOtherDifference = afterScaledToOtherRange * (otherAfterScaledToThisRange - otherBeforeScaledToThisRange);
                T otherAfterByThisDifference = otherAfterScaledToThisRange * (afterScaledToOtherRange - beforeScaledToOtherRange);
                //It's like we multiplied both sides of the ratio comparison by the denominators,
                //So we can just compare them
                if (thisAfterByOtherDifference < otherAfterByThisDifference)
                {
                    return 1;
                } else if (thisAfterByOtherDifference > otherAfterByThisDifference)
                {
                    return -1;
                }
                //If the ratios are the same, try the one with the larger scaled overshoot
                if (afterScaledToOtherRange < otherAfterScaledToThisRange)
                {
                    return 1;
                } else if (afterScaledToOtherRange > otherAfterScaledToThisRange)
                {
                    return -1;
                }
                //We'll compare the progressive changes outside of this if-else block, so we don't need to do it here.
            }
            else
            {
                //Both are not overshooting, so prioritize the one with the smaller before value, as that means the change was larger
                if (beforeScaledToOtherRange < otherBeforeScaledToThisRange)
                {
                    return -1;
                }
                else if (beforeScaledToOtherRange > otherBeforeScaledToThisRange)
                {
                    return 1;
                }
            }
            
            //Finally, if all else is equal, prioritize the one with the larger scaled progressive change
            T progressiveChangeScaledToOtherRange = ProgressiveChange * other.Range;
            T otherProgressiveChangeScaledToThisRange = other.ProgressiveChange * Range;
            return otherProgressiveChangeScaledToThisRange.CompareTo(progressiveChangeScaledToOtherRange);
        }
    }
}
