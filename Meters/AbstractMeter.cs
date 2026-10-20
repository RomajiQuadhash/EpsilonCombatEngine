using CoreMetrics;
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
    /// An abstract placeholder for meters that track a numeric value over time.
    /// Examples might be health, mana, or a vibe meter.
    /// Unlike a Counter, we don't need the time and metric types to be the same.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for the metric</typeparam>
    public abstract class AbstractMeter<T, M> : IAdvanceable<T>,IMeter<T>,ICombatCloneable, IEquatable<AbstractMeter<T,M>> where T : INumber<T> where M : INumber<M>
    {
        /// <summary>
        /// The timeline this meter belongs to. Should NEVER change, since a timeline should exist for the entire lifetime of a battle,
        /// and all meters should be destroyed when the battle ends.
        /// </summary>
        public abstract Timeline<T> OwningTimeline { get; init; }
        /// <summary>
        /// Note that there might be sub-details if say, M is a discrete type.
        /// You should use this for calculations though.
        /// </summary>
        public abstract M CurrentValue { get; }
        /// <summary>
        /// The rate of change of the meter's value, possibly needing to be divided by DerivativeDenominator to get the actual rate of change.
        /// Note that if we have more complex meters, advancing time might not just add this to the current value.
        /// But no matter what, a subclass should set this so we can use it for ThresholdEvents
        /// </summary>
        public abstract M Derivative { get; }
        /// <summary>
        /// If M is a discrete type, this is the value that Derivative is divided by to get the actual rate of change.
        /// If M is a continuous type, this should be 1, since we can just divide the two.
        /// </summary>
        public abstract T DerivativeDenominator { get; }
        /// <summary>
        /// What the most recent instant change to the meter was. Set to 0 after each advance, since it is only relevant for transitions,
        /// and keeping it around would lead to incorrect data about the most recent transition.
        /// </summary>
        public abstract M InstantChange { get; }
        //We need to know the min and max values of the meter, since it is needed for threshold events and to know if the meter is at a limit.
        public abstract M MinValue { get; }
        public abstract M MaxValue { get; }

        /// <summary>
        /// How to tell two meters apart from each other
        /// </summary>
        public int UUID { get; set; } = Guid.NewGuid().GetHashCode();

        /// <summary>
        /// UUID and type based equality. Used for hashing so the mutable properties are ignored
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(AbstractMeter<T, M>? other) => other is not null && other.GetType() == GetType() && other.UUID == UUID;
        public override bool Equals(object? obj) => Equals(obj as AbstractMeter<T, M>);
        public override int GetHashCode() => HashCode.Combine(GetType(), UUID);

        /// <summary>
        /// The range is more useful for making transition data, so we provide it here for convenience. 
        /// It also is NOT virtual, since increasing the range should be done by changing the min and max values to set how it should expand.
        /// </summary>
        public M Range => MaxValue - MinValue;
        public abstract void OnAdvance(object? sender, T timeElapsed); //For a simple meter, this is really simple.

        /// <summary>
        /// Returns, given current properties, the time at which the meter will touch the specified threshold.
        /// Note that this is a prediction based on current properties.
        /// If M is a discrete type, this is the first time at which the meter is at or past the threshold
        /// in the direction of movement, i.e., the first tick whose value reaches or crosses the threshold,
        /// not necessarily a tick whose value equals it exactly
        /// </summary>
        /// <param name="threshold">The value to see how long it will take to reach</param>
        /// <param name="allowNegative">If true, will return the first (without ticking again) even if it is in the past. 
        /// If false, if the only transition(s) are in the past, will return Never. Otherwise, the first one at or equal to time 0</param>
        /// <returns>The time at which the meter will touch the threshold or Never if it won't. 0 if already there</returns>
        public abstract TimeOrNever<T> ThresholdTouch(M threshold,bool allowNegative);
        /// <summary>
        /// Almost the same as ThresholdTouch, but with a few differences:
        /// 1. If the meter isn't moving, it will return Never instead of 0 if the meter is already at the threshold.
        /// 2. If M is a discrete type and a tick lands exactly on the threshold, it will be the next tick
        /// </summary>
        /// <param name="threshold">The value to see how long it will take to pass</param>
        /// <param name="allowNegative">If true, will return the earliest even if it is in the past. If false, if the only transition(s) are in the past, will return Never. Otherwise, the first one at or equal to time 0</param>
        /// <returns>The time at which the meter will cross the threshold or Never if it won't. 0 if already there and the meter is moving (or next tick if discrete)</returns>
        public abstract TimeOrNever<T> ThresholdCross(M threshold, bool allowNegative);
        /// <summary>
        /// Did the Instant Change move it past this threshold?
        /// </summary>
        /// <remarks>Note that this will only happen if at exactly that point since advancing clears the last instant transition</remarks>
        /// <param name="threshold">The threshold we want to see if we passed</param>
        /// <param name="includeTouch">If we should count where the before or after exactly includes the threshold. Not both though, since no crossing occurs</param>
        /// <returns>True if it was on one side before the instant change and then the other after</returns>
        public bool DidInstantlyCross(M threshold,bool includeTouch) //We can implement this here since instant changes are a value
        {
            if (M.IsZero(InstantChange))
            {
                return false; //Can't cross if we aren't moving
            }
            int thresholdComparison = threshold.CompareTo(CurrentValue) * threshold.CompareTo(CurrentValue - InstantChange);
            if (thresholdComparison == 0)
            {
                return includeTouch; //one of the comparisons was 0, meaning that we touched at one endpoint, so true if allowing touch
            }
            return thresholdComparison < 0; //One of before or after is on the other side of the other
        }
        public abstract object Clone(CloneContext context);

        //Note that there's no setters because that is specific to what the meter is holding and how it works.
        //These properties are for AbstractThresholdEvent to work with any kind of meter
    }
}
