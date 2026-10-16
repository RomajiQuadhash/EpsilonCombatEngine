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
    /// A meter specifically for the speed stat, including some key calculations and sharing of values
    /// </summary>
    /// <typeparam name="T">Time unit of the timeline</typeparam>
    public class SpeedMeter<T> : StatModificationMeter<T> where T : INumber<T>
    {
        /// <summary>
        /// The Aggregator that stores all the other speeds to compare to
        /// </summary>
        public MaxStatAggregator<T, SpeedMeter<T>> SpeedAggregator { get; init; }
        /// <summary>
        /// Creates a new speed meter.
        /// </summary>
        /// <param name="timeline">The timeline that owns this meter</param>
        /// <param name="baseStat">Positive value for what the stat is at no modification</param>
        /// <param name="minValue">Minimum Ratio this stat can be modified to</param>
        /// <param name="maxValue">Maximum Ratio this stat can be modified to</param>
        /// <param name="aggregator">A stat aggregator so that speeds involved can compare to each other. Share across the whole combat</param>
        public SpeedMeter(Timeline<T> timeline, int baseStat, Rational<long> minValue, Rational<long> maxValue, MaxStatAggregator<T, SpeedMeter<T>> aggregator)
            :base(timeline, baseStat, minValue, maxValue)
        {
            SpeedAggregator = aggregator;
            SpeedAggregator.Meters.Add(this); //In order for speed to work, all speeds must be aggregated.
        }
        /// <summary>
        /// How many times larger than the delay at max speed can the delay be? 
        /// </summary>
        public static readonly Rational<long> MaxDelayMagnitude = new(3, 1); //BATTLE TUNING PROPERTY
        /// <summary>
        /// This speed/max speed. Never use the raw speed value for delay calculations, instead only this fraction
        /// </summary>
        public Rational<long> SpeedFraction { get => GetModifiedStat() / SpeedAggregator.GetMaximum(); }
        /// <summary>
        /// One over SpeedFraction, maxed out at MaxDelayMagnitude. This effectively means SF below 1/MaxDelayMagnitude are all the same
        /// </summary>
        public Rational<long> DelayMultiplier { get =>Rational<long>.MinMagnitude(SpeedAggregator.GetMaximum()/GetModifiedStat(),MaxDelayMagnitude); }

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
            if (!context.TryGet(SpeedAggregator, out var aggregatorClone))
            {
                //We must be the first speed meter to be cloned. Note that cloning the aggregator will clone all the contents, including this
                SpeedAggregator.Clone(context);
                if (context.TryGet(this, out var newClone))
                {
                    return newClone;
                }
                else
                {
                    throw new UnrecoverableCloneException("Somehow, cloning the aggregator for speed didn't clone us.");
                }
            }
            //Okay so at this point, we have everything needed for the constructor
            SpeedMeter<T> clone = new(owningTimelineClone, BaseStat, MinValue, MaxValue, aggregatorClone);
            context.Register(this, clone);

            //Update to match.
            _currentValue = CurrentValue - InstantChange;
            clone.AddInstantChange(InstantChange); //Set instant change and bring CurrentValue back to where it is here
            clone.UUID = UUID;
            return clone;
        }
    }
}
