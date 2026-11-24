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
        protected override TimedTempChange<T, M> CreateEmptyForClone(Timeline<T> owningTimeline, CloneContext context)
        {
            return new TempContinuousChange<T, M>(context.GetOrClone(MeterAppliedTo), DerivativeChange, WaitingTime, ApplyTime, ApplyPriority, RemovePriority, owningTimeline);
        }
    }
}
