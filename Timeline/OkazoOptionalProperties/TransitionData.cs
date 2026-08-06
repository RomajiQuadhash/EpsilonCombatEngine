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
    /// <typeparam name="T">The numeric type used for the metric. Assumed to be able to hold fractional values</typeparam>
    public class TransitionData<T>: IComparable<TransitionData<T>> where T : INumber<T> 
    {
        /// <summary>
        /// The value before crossing the threshold. Always <=0, with 0 meaning no instant change.
        /// If it is negative, this is the amount behind the threshold the meter was before crossing the threshold.
        /// Scaled down by the range, so -1 means the prior value was one end of the range with the threshold at the other.
        /// </summary>
        public T Before { get; private set; }
        /// <summary>
        /// The value after crossing the threshold. Always >=0, with 0 meaning no instant change.
        /// If it is positive, this is the amount past the threshold the meter was after crossing the threshold.
        /// Scaled down by the range, so each 1 is a range worth of overshoot.
        /// </summary>
        public T After { get; private set; }
        /// <summary>
        /// What the progressive change of the meter was at the time of crossing the threshold, scaled down by the range. So each 1 is a range worth of change per time unit.
        /// Usually positive, but could be negative if an instant change went in the opposite direction of the progressive change.
        /// </summary>
        public T ProgressiveChange { get; private set; }
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
            if (instantChange == T.Zero)
            {
                //No instant change, so just absolute value the progressive change and scale by the range.
                ProgressiveChange = T.Abs(progressiveChange) / range;
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
            After = (after-threshold)/range;
            Before = After-(instantChange)/range;
            ProgressiveChange = progressiveChange/range;
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
            //Now, both are either overshooting or not. First, handle if both are overshooting
            if (After > T.Zero && other.After > T.Zero)
            {
                T thisRatio = After /(After-Before);
                T otherRatio = other.After /(other.After-other.Before);
                if (thisRatio < otherRatio)
                {
                    return 1;
                } else if (thisRatio > otherRatio)
                {
                    return -1;
                }
                //If the ratios are the same, try the one with the larger raw overshoot
                if (After < other.After)
                {
                    return 1;
                } else if (After > other.After)
                {
                    return -1;
                }
                //Finally, return the comparison of the progressive changes, with larger progressive change being prioritized
                return other.ProgressiveChange.CompareTo(ProgressiveChange);
            }
            //Both are not overshooting, so prioritize the one with the smaller before value, as that means the change was larger
            if (Before < other.Before)
            {
                return -1;
            } else if (Before > other.Before)
            {
                return 1;
            }
            //Finally, return the comparison of the progressive changes, with larger progressive change being prioritized
            return other.ProgressiveChange.CompareTo(ProgressiveChange);
        }
    }
}
