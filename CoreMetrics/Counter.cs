using System.Numerics;

namespace CoreMetrics
{
	/// <summary>
	/// This class keeps track of a value that may change over time.
	/// A counter can also tell how long until it passes a threshold or how far in the past that threshold was crossed.
	/// This only works if the counter is linear over that time, however!
	/// </summary>
	/// <typeparam name="T">Number type to use</typeparam>
	public class Counter<T> where T : INumber<T>
    {
		public Counter() { 
			Value = T.Zero;
			RateOfChange = T.Zero;
        }
		public Counter(T initialValue, T rateOfChange)
		{
			Value = initialValue;
			RateOfChange = rateOfChange;
        }

        public T Value { get; set; }
        public T RateOfChange { get; set; }

        #region Progression calculations
        /// <summary>
        /// Advances time forward, updating the value by the rate of change.
        /// </summary>
        /// <param name="timeElapsed">The amount of elapsed time over which to apply the rate of change.</param>
        public void Advance(T timeElapsed)
        {
            Value += RateOfChange * timeElapsed;
        }
        /// <summary>
        /// Caluculates when the counter will cross a given threshold, returning a TimeOrNever indicating the time until crossing or that it will never cross.
        /// Note that this can return a negative time if the is moving away from the threshold, even if the threshold was not previously crossed.
        /// </summary>
        /// <param name="threshold"></param>
        /// <returns>Zero if the value is at the threshold, never if not and the value isn't changing, otherwise (Value - threshold) / RateOfChange</returns>
        public virtual TimeOrNever<T> ThresholdCross(T threshold)
        {
            // Always return zero if we're already at the threshold, even if the rate of change is zero, to avoid returning "never" when we're already there.
            if (Value == threshold)
            {
                return new TimeOrNever<T>(T.Zero);
            }
            if (T.IsZero(RateOfChange))
            {
                return new TimeOrNever<T> { IsNever = true };
            }
            return new TimeOrNever<T>((Value - threshold) / RateOfChange);
        }
        /// <summary>
        /// Calculates the time remaining until the value reaches the specified threshold, or indicates that the
        /// threshold will never be reached.
        /// </summary>
        /// <param name="threshold">The value to compare against the current state to determine when the threshold will be crossed.</param>
        /// <returns>A TimeOrNever<T> representing the time until the threshold is reached. If the threshold will not be reached
        /// in the future, the result indicates 'never'.</returns>
        public virtual TimeOrNever<T> TimeUntil(T threshold)
        {
            var cross = ThresholdCross(threshold);
            // If the expected crossing time is negative, then it won't happen in the future, so return never.
            if (!cross.IsNever && cross.Time.CompareTo(T.Zero) < 0)
            {
                return new TimeOrNever<T> { IsNever = true };
            }
            return cross;
        }
        #endregion

        #region Rate calculations
        /// <summary>
        /// Calculates the rate of change required to achieve a specified total change over a given duration.
        /// </summary>
        /// <param name="delta">The total change to be distributed across the duration.</param>
        /// <param name="duration">The duration over which the change should occur. Must be positive.</param>
        /// <returns>The rate of change per unit of duration needed to reach the specified total change over the given duration.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="duration"/> is zero or negative.</exception>
        public static T SpreadDelta(T delta, T duration)
        {
            if (T.IsZero(duration) || T.IsNegative(duration))
            {
                throw new ArgumentException("Duration of change must be positive");
            }
            return delta / duration;
        }
        /// <summary>
        /// Calculates and sets the rate of change required to interpolate the current value toward the specified target
        /// over the given duration. Note that this doesn't stop the change after the duration, so stop it after that duration has passed to not overshoot the target.
        /// </summary>
        /// <param name="target">The target value to interpolate toward.</param>
        /// <param name="duration">The duration over which the interpolation should occur. Must be greater than zero.</param>
        public virtual void Lerp(T target, T duration)
        {
            RateOfChange = SpreadDelta(target - Value, duration);
        }
        /// <summary>
        /// Adds a delta over a specified duration to the current rate of change, effectively applying a change that will be spread out over that duration.
        /// Note that this doesn't stop the change after the duration, so if you want to apply a change for a specific duration, you should call this method again with the opposite delta after that duration has passed.
        /// </summary>
        /// <param name="delta">The total change to be distributed across the duration.</param>
        /// <param name="duration">The duration over which the change should occur. Must be positive.</param>
        public virtual void ApplySpreadDelta(T delta, T duration)
        {
            RateOfChange += SpreadDelta(delta, duration);
        }
        #endregion

        #region overrides
        public override string ToString()
		{
			return $"Counter(Value: {Value}, RateOfChange: {RateOfChange})";
        }
        #endregion
    }
}
