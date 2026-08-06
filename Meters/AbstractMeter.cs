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
    public abstract class AbstractMeter<T, M> : IAdvanceable<T> where T : INumber<T> where M : INumber<M>
    {
        /// <summary>
        /// The timeline this meter belongs to. Should NEVER change, since a timeline should exist for the entire lifetime of a battle,
        /// and all meters should be destroyed when the battle ends.
        /// TODO: see if I can make this set once only.
        /// </summary>
        public required Timeline<T> OwningTimeline { get; set; }
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
        /// The range is more useful for making transition data, so we provide it here for convenience. 
        /// It also is NOT virtual, since increasing the range should be done by changing the min and max values to set how it should expand.
        /// </summary>
        public M Range => MaxValue - MinValue;
        public abstract void OnAdvance(object? sender, T timeElapsed); //For a simple meter, this is really simple.
        //But, I'm not going to bake that in at this stage, given I'm not coding anything at this stage

        /// <summary>
        /// Returns, given current properties, the time at which the meter will touch the specified threshold.
        /// Note that this is a prediction based on current properties.
        /// If M is a discrete type, this is the earliest time at which the meter will be equal to the threshold.
        /// </summary>
        /// <param name="threshold">The value to see how long it will take to reach</param>
        /// <param name="allowNegative">If true, will return the earliest even if it is in the past. If false, if the only transition(s) are in the past, will return Never. Otherwise, the first one at or equal to time 0</param>
        /// <returns>The time at which the meter will touch the threshold or Never if it won't. 0 if already there</returns>
        public abstract TimeOrNever<T> ThresholdTouch(M threshold,bool allowNegative);
        /// <summary>
        /// Almost the same as ThresholdTouch, but with a few differences:
        /// 1. If the meter isn't moving, it will return Never instead of 0 if the meter is already at the threshold.
        /// 2. If M is a discrete type, this is the latest time at which the meter will be equal to the threshold, rather than the earliest.
        /// </summary>
        /// <param name="threshold">The value to see how long it will take to pass</param>
        /// <param name="allowNegative">If true, will return the earliest even if it is in the past. If false, if the only transition(s) are in the past, will return Never. Otherwise, the first one at or equal to time 0</param>
        /// <returns>The time at which the meter will cross the threshold or Never if it won't. 0 if already there and the threshold is moving</returns>
        public abstract TimeOrNever<T> ThresholdCross(M threshold, bool allowNegative);

        //Note that there's no setters because that is specific to what the meter is holding and how it works.
        //These properties are for AbstractThresholdEvent to work with any kind of meter
    }
}
