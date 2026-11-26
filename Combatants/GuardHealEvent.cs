using CoreMetrics;
using Meters;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Combatants
{
    /// <summary>
    /// The Guard healing back what one source of damage chipped off it. It is a temporary change to the Guard meter's rate that can also be cancelled,
    /// for when the Guard is put back to full and nothing from before should carry on.
    /// A HealthMeter makes these when a source of damage ends.
    /// </summary>
    /// <remarks>Claude written, Romaji </remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <param name="guard">The Guard meter that heals</param>
    /// <param name="rate">How much the Guard heals per unit of time while this is applied</param>
    /// <param name="waitingTime">How long from now until the heal starts. 0 or more</param>
    /// <param name="applyTime">How long the heal lasts. 0 or more</param>
    /// <param name="applyPriority">The priority while it is waiting to start</param>
    /// <param name="removePriority">The priority while it is waiting to stop</param>
    /// <param name="owningTimeline">The timeline this is for. Must be the meter's</param>
    public class GuardHealEvent<T>(LinearContinuousMeter<T, double> guard, double rate, T waitingTime, T applyTime, PriorityRank? applyPriority, PriorityRank? removePriority, Timeline<T> owningTimeline) : TempContinuousChange<T, double>(guard, rate, waitingTime, applyTime, applyPriority, removePriority, owningTimeline) where T : INumber<T>
    {
        /// <summary>
        /// True once Cancel has been called. A cancelled heal still occurs when it was going to, but does nothing to the meter.
        /// </summary>
        public bool IsCancelled { get; private set; }
        /// <summary>
        /// Stops this from doing anything more to the Guard meter. It doesn't take back what it has already done:
        /// whoever cancels it is expected to set the meter's value and rate to what they should now be.
        /// </summary>
        /// <remarks>An event can't always be taken off the timeline right away, so it stays there and does nothing when its time comes.</remarks>
        public void Cancel()
        {
            IsCancelled = true;
        }
        protected override void ApplyChange()
        {
            if (!IsCancelled)
            {
                base.ApplyChange();
            }
        }
        protected override void RemoveChange()
        {
            if (!IsCancelled)
            {
                base.RemoveChange();
            }
        }
        protected override TimedTempChange<T, double> CreateEmptyForClone(Timeline<T> owningTimeline, CloneContext context)
        {
            return new GuardHealEvent<T>(context.GetOrClone(MeterAppliedTo), DerivativeChange, WaitingTime, ApplyTime, ApplyPriority, RemovePriority, owningTimeline);
        }
        protected override void CopyPropertiesForClone(TimedTempChange<T, double> clone, CloneContext context)
        {
            base.CopyPropertiesForClone(clone, context);
            if (clone is GuardHealEvent<T> healClone)
            {
                healClone.IsCancelled = IsCancelled;
                return;
            }
            throw new UnrecoverableCloneException("Somehow, we cloned a GuardHealEvent and got a different type back. This should never happen.");
        }
    }
}
