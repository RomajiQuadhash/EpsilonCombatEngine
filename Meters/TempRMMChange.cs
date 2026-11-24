using CoreMetrics;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Meters
{
    /// <summary>
    /// A temporary source of change for a RateManagedMeter: adds a ModInstance when it applies, and removes that same one when it is removed.
    /// The meter works out what that does to its rate, including merging it with whatever else is affecting it.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for the affected meter. Integer</typeparam>
    /// <param name="meter">The meter the change applies to</param>
    /// <param name="modifier">The source of change to add to the meter while this is applied</param>
    /// <param name="waitingTime">How long from now until the change applies. 0 or more</param>
    /// <param name="applyTime">How long the change stays applied before it is removed. 0 or more</param>
    /// <param name="applyPriority">The priority while it is waiting to apply</param>
    /// <param name="removePriority">The priority while it is waiting to be removed</param>
    /// <param name="owningTimeline">The timeline this is for. Must be the meter's</param>
    /// <remarks>Claude with only minor changes</remarks>
    public class TempRMMChange<T, M>(RateManagedMeter<T, M> meter, ModInstance<T, M> modifier, T waitingTime, T applyTime, PriorityRank? applyPriority, PriorityRank? removePriority, Timeline<T> owningTimeline) : TimedTempChange<T, M>(meter, waitingTime, applyTime, applyPriority, removePriority, owningTimeline) where T : INumber<T> where M : IBinaryInteger<M>
    {
        /// <summary>
        /// The meter the change applies to, as the kind of meter this needs.
        /// </summary>
        public new RateManagedMeter<T, M> MeterAppliedTo => (RateManagedMeter<T, M>)base.MeterAppliedTo;
        /// <summary>
        /// The source of change this adds to the meter while it is applied.
        /// </summary>
        public ModInstance<T, M> Modifier { get; } = modifier;
        /// <summary>
        /// The key the meter gave for the modifier when this applied, which is what removes it again. Null until it applies.
        /// </summary>
        public ModifierKey? Key { get; private set; }

        protected override void ApplyChange()
        {
            Key = MeterAppliedTo.Add(Modifier);
        }
        protected override void RemoveChange()
        {
            if (Key is ModifierKey key)
            {
                MeterAppliedTo.Remove(key);
            }
        }
        protected override TimedTempChange<T, M> CreateEmptyForClone(Timeline<T> owningTimeline, CloneContext context)
        {
            return new TempRMMChange<T, M>(context.GetOrClone(MeterAppliedTo), Modifier, WaitingTime, ApplyTime, ApplyPriority, RemovePriority, owningTimeline);
        }
        protected override void CopyPropertiesForClone(TimedTempChange<T, M> clone, CloneContext context)
        {
            base.CopyPropertiesForClone(clone, context);
            if (clone is TempRMMChange<T, M> modInstanceClone)
            {
                //The cloned meter has the same modifiers under the same keys, so the key works on it as it is
                modInstanceClone.Key = Key;
                return;
            }
            throw new UnrecoverableCloneException("Somehow, we cloned a TempRMMChange and got a different type back. This should never happen.");
        }
    }
}
