using CoreMetrics;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Meters
{
    /// <summary>
    /// A temporary change to a meter's value: an instant change when it applies, and the opposite instant change when it is removed.
    /// This is what a buff or debuff of a fixed size is. Works on any meter that supports instant changes, which is all of them.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for the affected meter</typeparam>
    /// <param name="meter">The meter the change applies to</param>
    /// <param name="change">How much to add to the meter while this is applied. Negative to take away</param>
    /// <param name="waitingTime">How long from now until the change applies. 0 or more</param>
    /// <param name="applyTime">How long the change stays applied before it is removed. 0 or more</param>
    /// <param name="applyPriority">The priority while it is waiting to apply</param>
    /// <param name="removePriority">The priority while it is waiting to be removed</param>
    /// <param name="owningTimeline">The timeline this is for. Must be the meter's</param>
    /// <remarks>Claude notice</remarks>
    public class TempInstantChange<T, M>(InstantChangeMeter<T, M> meter, M change, T waitingTime, T applyTime, PriorityRank? applyPriority, PriorityRank? removePriority, Timeline<T> owningTimeline) : TimedTempChange<T, M>(meter, waitingTime, applyTime, applyPriority, removePriority, owningTimeline) where T : INumber<T> where M : INumber<M>
    {
        /// <summary>
        /// The meter the change applies to, as the kind of meter this needs.
        /// </summary>
        public new InstantChangeMeter<T, M> MeterAppliedTo => (InstantChangeMeter<T, M>)base.MeterAppliedTo;
        /// <summary>
        /// How much this tries to add to the meter while it is applied.
        /// </summary>
        public M Change { get; } = change;
        /// <summary>
        /// How much it actually added when it applied, which is less than Change if the meter hit its minimum or maximum.
        /// This is what gets taken back off, so that removing it never takes more than it gave. 0 until it applies.
        /// </summary>
        public M AppliedChange { get; private set; } = M.Zero;

        protected override void ApplyChange()
        {
            M before = MeterAppliedTo.CurrentValue;
            MeterAppliedTo.AddInstantChange(Change);
            AppliedChange = MeterAppliedTo.CurrentValue - before;
        }
        protected override void RemoveChange()
        {
            MeterAppliedTo.AddInstantChange(-AppliedChange);
        }
        protected override TimedTempChange<T, M> CreateEmptyForClone(Timeline<T> owningTimeline, CloneContext context)
        {
            return new TempInstantChange<T, M>(context.GetOrClone(MeterAppliedTo), Change, WaitingTime, ApplyTime, ApplyPriority, RemovePriority, owningTimeline);
        }
        protected override void CopyPropertiesForClone(TimedTempChange<T, M> clone, CloneContext context)
        {
            base.CopyPropertiesForClone(clone, context);
            if (clone is TempInstantChange<T, M> instantClone)
            {
                instantClone.AppliedChange = AppliedChange;
                return;
            }
            throw new UnrecoverableCloneException("Somehow, we cloned a TempInstantChange and got a different type back. This should never happen.");
        }
    }
}
