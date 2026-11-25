using CoreMetrics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Meters
{
    /// <summary>
    /// Event for threshold crossing for a LinearDiscreteMeter. Necessary since ReachThresholdEvent doesn't know how to handle Ticks and will not properly fill TransitionData with DiscreteTransitionData.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for this meter's value. Integer</typeparam>
    public class DiscreteReachThresholdEvent<T, M> : ReachThresholdEvent<T, M> where T : INumber<T> where M : IBinaryInteger<M>
    {
        /// <summary>
        /// What meter is this for? We know it is a LinearDiscreteMeter, so we can cast it to that type for convenience.
        /// </summary>
        public new LinearDiscreteMeter<T, M> OwningMeter => (LinearDiscreteMeter<T, M>)_owningMeter;
        /// <summary>
        /// Computes the transition data for this event, if it is possible to compute it. If the meter is not going to reach the threshold, returns null.
        /// </summary>
        public override DiscreteTransitionData<T,M>? TransitionData { get
            {
                TimeOrNever<T> timeRemaining = ComputeTimeRemaining();
                if (timeRemaining.IsNever)
                {
                    return null;
                }
                if (T.IsZero(timeRemaining.Time))
                {
                    return new(OwningMeter.InstantChange,OwningMeter.CurrentValue,OwningMeter.Derivative,OwningMeter.Range,ThresholdPoint,OwningMeter.DerivativeDenominator,OwningMeter.TimeSinceLastIncrement==T.Zero);
                }
                //Add the time since last increment to the time remaining since it is subtracted by ThresholdReach, and we want the time we divide to be a whole number of ticks, since we're making it into an integer number that will get us past the threshold.
                M ticksUntilThreshold = M.CreateTruncating((timeRemaining.Time+OwningMeter.TimeSinceLastIncrement) / OwningMeter.DerivativeDenominator);
                M futureValue=OwningMeter.CurrentValue + (ticksUntilThreshold * OwningMeter.Derivative);
                //If we are seeing it in the future, then the tick is what we assume will change it, since InstantChange can't be predicted
                return new(M.Zero,futureValue,OwningMeter.Derivative,OwningMeter.Range,ThresholdPoint,OwningMeter.DerivativeDenominator,true);
            } }
        public DiscreteReachThresholdEvent(LinearDiscreteMeter<T, M> owningMeter, M thresholdPoint, PriorityRank? priority,Timeline<T> owningTimeline)
            : base(owningMeter, thresholdPoint, priority, owningTimeline)
        {
        }
        protected DiscreteReachThresholdEvent(LinearDiscreteMeter<T, M> owningMeter, M thresholdPoint, PriorityRank? priority, Timeline<T> owningTimeline, bool instantFlag, bool allowNegative)
            : base(owningMeter, thresholdPoint, priority, owningTimeline, instantFlag, allowNegative)
        {
        }
        public override object Clone(CloneContext context)
        {
            if (context.TryGet(this, out DiscreteReachThresholdEvent<T, M> existing)) return existing;
            if (!context.TryGet(OwningTimeline, out Timeline<T> clonedTimeline))
                throw new UnrecoverableCloneException("Timeline must be cloned before its threshold event.");
            LinearDiscreteMeter<T, M> clonedMeter = context.GetOrClone(OwningMeter);
            DiscreteReachThresholdEvent<T,M> clone = new(clonedMeter,ThresholdPoint,Priority,clonedTimeline,_instantFlag, _allowNegative);
            context.Register(this, clone);
            
            //Move other properties
            clone.UUID=UUID;
            clone.HaltsInstantAction=HaltsInstantAction;
            clone.Direction=Direction;
            clone.CouldOccur=CouldOccur;
            return clone;
        }
    }
}
