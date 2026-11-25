using CoreMetrics;
using Meters;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Combatants
{
    /// <summary>
    /// The damage phase of an attack: adds a source of damage over time to a HealthMeter when it applies, and removes that same one when it is removed.
    /// The damage is what it was before Defense. The HealthMeter divides it by the Defense for as long as it stays.
    /// </summary>
    /// <remarks>Claude written, Romaji approved</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <param name="meter">The health the damage is dealt to</param>
    /// <param name="damage">The damage as it is before Defense: how much per tick (more than 0) and how often it ticks</param>
    /// <param name="waitingTime">How long from now until the damage starts. 0 or more</param>
    /// <param name="applyTime">How long the damage keeps coming before it stops. More than 0, since damage that takes no time is instant damage, which is a different thing</param>
    /// <param name="applyPriority">The priority while it is waiting to start</param>
    /// <param name="removePriority">The priority while it is waiting to stop</param>
    /// <param name="owningTimeline">The timeline this is for. Must be the meter's</param>
    public class DamagePhaseEvent<T>(HealthMeter<T> meter, ModInstance<T, int> damage, T waitingTime, T applyTime, PriorityRank? applyPriority, PriorityRank? removePriority, Timeline<T> owningTimeline) : TimedTempChange<T, int>(meter, waitingTime, ValidatedApplyTime(applyTime, damage), applyPriority, removePriority, owningTimeline) where T : INumber<T>
    {
        /// <summary>
        /// Checks what the damage phase is being made with, and gives the apply time back if it is all fine.
        /// It is done on the way into the base constructor so that a refused damage phase was never subscribed to the timeline.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">If the apply time is 0 or less, or the damage per tick is 0 or less</exception>
        private static T ValidatedApplyTime(T applyTime, ModInstance<T, int> damage)
        {
            if (applyTime <= T.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(applyTime), applyTime, "A damage phase has to last more than 0. Damage that takes no time is instant damage");
            }
            if (damage.PerTick <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "Damage has to be more than 0. Healing isn't divided by Defense, so it isn't a damage phase");
            }
            return applyTime;
        }
        /// <summary>
        /// The health the damage is dealt to, as the kind of meter this needs.
        /// </summary>
        public new HealthMeter<T> MeterAppliedTo => (HealthMeter<T>)base.MeterAppliedTo;
        /// <summary>
        /// The damage this adds to the meter while it is applied, as it is before Defense.
        /// </summary>
        public ModInstance<T, int> Damage { get; } = damage;
        /// <summary>
        /// The key the meter gave for the damage when this applied, which is what removes it again. Null until it applies.
        /// </summary>
        public ModifierKey? Key { get; private set; }

        protected override void ApplyChange()
        {
            Key = MeterAppliedTo.AddDamage(Damage);
        }
        protected override void RemoveChange()
        {
            if (Key is ModifierKey key)
            {
                MeterAppliedTo.RemoveDamage(key);
            }
        }
        protected override TimedTempChange<T, int> CreateEmptyForClone(Timeline<T> owningTimeline, CloneContext context)
        {
            return new DamagePhaseEvent<T>(context.GetOrClone(MeterAppliedTo), Damage, WaitingTime, ApplyTime, ApplyPriority, RemovePriority, owningTimeline);
        }
        protected override void CopyPropertiesForClone(TimedTempChange<T, int> clone, CloneContext context)
        {
            base.CopyPropertiesForClone(clone, context);
            if (clone is DamagePhaseEvent<T> damageClone)
            {
                //The cloned meter has the same damage under the same keys, so the key works on it as it is
                damageClone.Key = Key;
                return;
            }
            throw new UnrecoverableCloneException("Somehow, we cloned a DamagePhaseEvent and got a different type back. This should never happen.");
        }
    }
}
