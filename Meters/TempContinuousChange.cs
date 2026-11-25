using CoreMetrics;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Meters
{
    /// <summary>
    /// A temporary change to how fast a continuous meter is changing: adds to its derivative when it applies, and takes the same amount back off when it is removed.
    /// This is what something like a regeneration or a drain over a fixed length of time is.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for the affected meter</typeparam>
    /// <param name="meter">The meter the change applies to</param>
    /// <param name="derivativeChange">How much to add to the meter's derivative while this is applied. Negative to slow it or make it fall</param>
    /// <param name="waitingTime">How long from now until the change applies. 0 or more</param>
    /// <param name="applyTime">How long the change stays applied before it is removed. 0 or more</param>
    /// <param name="applyPriority">The priority while it is waiting to apply</param>
    /// <param name="removePriority">The priority while it is waiting to be removed</param>
    /// <param name="owningTimeline">The timeline this is for. Must be the meter's</param>
    /// <remarks>Claude made, romaji renamed</remarks>
    public class TempContinuousChange<T, M>(LinearContinuousMeter<T, M> meter, M derivativeChange, T waitingTime, T applyTime, PriorityRank? applyPriority, PriorityRank? removePriority, Timeline<T> owningTimeline) : TimedTempChange<T, M>(meter, waitingTime, applyTime, applyPriority, removePriority, owningTimeline) where T : INumber<T> where M : INumber<M>
    {
        /// <summary>
        /// The meter the change applies to, as the kind of meter this needs.
        /// </summary>
        public new LinearContinuousMeter<T, M> MeterAppliedTo => (LinearContinuousMeter<T, M>)base.MeterAppliedTo;
        /// <summary>
        /// How much this adds to the meter's derivative while it is applied.
        /// </summary>
        public M DerivativeChange { get; } = derivativeChange;

        protected override void ApplyChange()
        {
            MeterAppliedTo.AddToDerivative(DerivativeChange);
        }
        protected override void RemoveChange()
        {
            MeterAppliedTo.AddToDerivative(-DerivativeChange);
        }
        /// <summary>
        /// Makes the event for changing a meter by a set amount over a set length of time (a LERP).
        /// If the length of time is zero, that is one instant change, so an InstantChangeEvent is made.
        /// Otherwise it is a TempContinuousChange whose rate is the amount divided by the time, applied for that time.
        /// </summary>
        /// <remarks>Like the constructors, this doesn't put the event on the timeline and doesn't touch the meter. Use AddEvent or AddPossibleEvent on the AsOkazo of what it returns.</remarks>
        /// <param name="meter">The meter the change applies to</param>
        /// <param name="totalChange">How much the meter should have changed by once the whole time has passed. Negative to take away</param>
        /// <param name="waitingTime">How long from now until the change starts</param>
        /// <param name="lerpTime">How long the change takes, from when it starts. 0 for all at once</param>
        /// <param name="applyPriority">The priority while the change is waiting to start, whether it is gradual or all at once</param>
        /// <param name="removePriority">The priority while a gradual change is waiting to stop</param>
        /// <param name="owningTimeline">The timeline this is on. Must be the meter's timeline</param>
        /// <param name="actualTotalChange">
        /// How much of the change would fit between the meter's minimum and maximum, going by where the meter is right now.
        /// The same as totalChange if all of it fits. The meter may be somewhere else by the time the change starts, so this is an estimate.
        /// </param>
        /// <returns>The InstantChangeEvent or TempContinuousChange that makes the change</returns>
        /// <exception cref="ArgumentOutOfRangeException">If lerpTime is negative</exception>
        public static IMeterChangeEvent<T, M> CreateEventFromLerp(LinearContinuousMeter<T, M> meter, M totalChange, T waitingTime, T lerpTime,
            PriorityRank? applyPriority, PriorityRank? removePriority, Timeline<T> owningTimeline, out M actualTotalChange)
        {
            if (T.IsNegative(lerpTime))
            {
                throw new ArgumentOutOfRangeException(nameof(lerpTime), lerpTime, "The time a change takes can't be negative");
            }
            actualTotalChange = M.Clamp(meter.CurrentValue + totalChange, meter.MinValue, meter.MaxValue) - meter.CurrentValue;
            if (T.IsZero(lerpTime))
            {
                return new InstantChangeEvent<T, M>(meter, totalChange, waitingTime, applyPriority, owningTimeline);
            }
            M rate = totalChange / RationalConversion.ConvertTruncating<T, M>(lerpTime);
            return new TempContinuousChange<T, M>(meter, rate, waitingTime, lerpTime, applyPriority, removePriority, owningTimeline);
        }
        protected override TimedTempChange<T, M> CreateEmptyForClone(Timeline<T> owningTimeline, CloneContext context)
        {
            return new TempContinuousChange<T, M>(context.GetOrClone(MeterAppliedTo), DerivativeChange, WaitingTime, ApplyTime, ApplyPriority, RemovePriority, owningTimeline);
        }
    }
}
