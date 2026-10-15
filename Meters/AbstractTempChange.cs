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
    /// An event that represents a temporary change to a meter (not necessarily the value, but could be a rate of change or other property).
    /// For example, an attack would temporarily change a health meter to be decreasing at a certain rate, then stop.
    /// This can also be a buff or debuff, by being a temporary value change (implement by instant change at the start and end of the event).
    /// </summary>
    /// <remarks>Note that as an abstract class, this doesn't actually do those things and only exists to plan around</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for the affected metric</typeparam>
    public abstract class AbstractTempChange<T, M>:Okazo<T>,IOkPriority where T : INumber<T> where M : INumber<M>
    {
        //Owners.
        public abstract Timeline<T> OwningTimeline { get; }
        /// <summary>
        /// The singular meter the change should apply to. 
        /// If you need to impact more than one meter, make multiple events.
        /// </summary>
        public abstract AbstractMeter<T,M> MeterAppliedTo {  get; }
        /// <summary>
        /// Priority of the event. Can change when the event changes from Apply to Remove.
        /// Should, in most cases, be one of the following:
        /// PlayerBleedStart, PlayerBleedStop, EnemyBleedStart, EnemyBleedEnd,
        /// DamagePhaseStart, DamagePhaseEnd, InstantMeterChange, MeterChangePhaseStart, MeterChangePhaseEnd
        /// </summary>
        public abstract PriorityRank? Priority { get; set; }
        /// <summary>
        /// If this event is going to apply a new change, or is it going to end the change it made?
        /// </summary>
        public ApplyOrRemove LifeCyclePhase { get; set; }

        /// <summary>
        /// Checks if the priority is a recommended priority for a temporary change.
        /// If you're consistently making events where this value is false, consider if it should be a temp change
        /// </summary>
        /// <returns>True if the phase is InstantMeterChange, or if it is PlayerBleed*, EnemyBleed*, DamagePhase*, MeterChangePhase* 
        /// (where * is Start or End depending on LifeCyclePhase)</returns>
        public bool IsOrdinaryPriority()
        {
            if (Priority == null) return false;
            if (Priority == PriorityRank.InstantMeterChange) return true;

            if (LifeCyclePhase == ApplyOrRemove.Apply)
            {
                return Priority switch
                {
                    PriorityRank.PlayerBleedStart or PriorityRank.EnemyBleedStart or PriorityRank.DamagePhaseStart or PriorityRank.MeterChangePhaseStart => true,
                    _ => false,
                };
            }
            //Otherwise, we're in remove
            return Priority switch
            {
                PriorityRank.PlayerBleedEnd or PriorityRank.EnemyBleedEnd or PriorityRank.DamagePhaseEnd or PriorityRank.MeterChangePhaseEnd => true,
                _ => false,
            };
        }

        /// <summary>
        /// Called by Trigger when in Apply, before adding it to PossibleEvents
        /// </summary>
        public abstract void TriggerWhenApply();
        public abstract void TriggerWhenRemove();
    }
    /// <summary>
    /// Indicates whether this event is applying a change or removing its change.
    /// An event triggering during application will turn to Remove and add itself to PossibleEvents,
    /// and an Event in Remove will be permanently removed during remove, but that part is not required.
    /// </summary>
    public enum ApplyOrRemove
    {
        Apply,
        Remove
    }
}
