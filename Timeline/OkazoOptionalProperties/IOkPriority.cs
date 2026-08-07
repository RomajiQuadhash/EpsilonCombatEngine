using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timeline.OkazoOptionalProperties
{
    /// <summary>
    /// Implement this interface for events that have a priority
    /// </summary>
    public interface IOkPriority
    {
        /// <summary>
        /// The priority of this event, used as a tiebreaker for events that occur at the same time. Lower numbers occur first.
        /// If this value is before "None", it will occur before any events without a priority. "None" and null are effectively the same, but prefer "None" to make it clear that the event was intentionally given no priority, rather than just forgetting to set a priority.
        /// </summary>
        public PrioityRank? Priority { get; }
        /// <summary>
        /// Compares the priorty of this event to another event that has a priority
        /// </summary>
        /// <param name="other">Another event with a priority to compare against</param>
        /// <returns>An integer that indicates the relative priority of the events. A positive value indicates this event has higher priority, a negative value indicates the other event has higher priority, and zero indicates equal priority.</returns>
        public int IsHigherPriorityThan(IOkPriority other)
        {
            PrioityRank? thisPriority = this.Priority ?? PrioityRank.None;
            PrioityRank? otherPriority = other.Priority ?? PrioityRank.None;
            // We want lower numbers to be higher priority, so we compare other to this, rather than the usual this to other.
            return otherPriority.Value.CompareTo(thisPriority.Value);
        }
        /// <summary>
        /// Used to compare the priority of this event to an event without a priority
        /// </summary>
        /// <returns>An integer that indicates if this priority is higher, lower, or equal to no priority at all. Positive higher priority, negative lower priority, and zero equal priority.</returns>
        public int IsHigherThanNone()
        {
            if (this.Priority == null)
            {
                return 0;
            }
            return PrioityRank.None.CompareTo(this.Priority.Value);
        }
    }
    /// <summary>
    /// List of event priorities. Lower numbers occur first. This is used as a tiebreaker for events that occur at the same time, that have priorities.
    /// If an event does not have a priority, it is considered to have priority "None".
    /// </summary>
    public enum PrioityRank
    {
        DebugHigh,
        //Lose before win as winning shouldn't be allowed to make people survive when they shouldn't
        InstantLoss,

        PlayerBleedEnd, // If a player stops (end used for consistency with later) bleeding, their min HP should be set to 0, before they can die from having too negative HP.
        PlayerDeath,
        PlayerBleedStart, // If a player starts bleeding, their min HP should be set to negative, before being capped at 0 and normal KOed
        PlayerKO,
        PlayerRes, //Restoring a player from KO should prevent an AllPlayerKO loss, so it should occur before. After KO since a player can't be restored until they're KOed, and it would be annoying for a player to fail to restore (since restore only works on KOed players), then immedietly be KOed.
        AllPlayerKO, //Wait to make sure all players are KOed before losing due to player KO. Any death is a loss, so no need for an AllPlayerDeath
        
        //Special exit conditions that aren't a win or a loss.
        UnknownEnding,
        Escape,

        InstantWin,
        //Same order for enemies
        EnemyBleedEnd,
        EnemyDeath,
        EnemyBleedStart,
        EnemyKO,
        EnemySpawn, //If an enemy spawns, that should occcur before a "no enemies remain" victory
        NoEnemiesRemain,

        //Damage should occur after death/KO, since a dead/KOed person shouldn't deal damage
        InstantDamage, //"Instant" means the damage completely occurs at once. Can "overshoot", unlike progressive damage.
        DamagePhaseEnd,
        DamagePhaseStart,
        //"Meter" includes health (but only healing) and other resources.
        MeterLimit, //This meter is about to pass the max or minimum value (excluding minimum for HP). Stop it from crossing the limit before anything else happens
        MeterTriggerInstant,
        MeterTriggerProgressive, //"Progressive" means the meter change occurs over time, so it won't be ever "overshot" like instant effects.
        //Changes after limits and triggers, since it's possible for a limit or trigger to prevent some or all of the change from occurring,
        //and we want that to be reflected in the timeline.
        InstantMeterChange,
        MeterChangePhaseEnd,
        MeterChangePhaseStart,
        //Status effects are integer or binary changes, so the instant/progressive distinction doesn't apply.
        StatusEffectEnd, //Also used for decreasing stacks
        StatusEffectStart, //Likewise, also used for increasing stacks
        //Complex events likely involve multiple changes, so if they're valid, all of their simple components should should happen before starting another complex event
        //Most should be instant and set up progressive changes, but some might have lingering effects more complex than meter/damage/status changes, so they get their own category.
        ComplexInstant,
        ComplexEnd,
        ComplexStart,

        //"Animations"
        //Used for literal animations in playback, but more directly for timing of moves/attacks (windup, active, and recovery)
        AnimPhaseEnd, //End of an animation phase, usually will start another animation phase or return control to the player/AI
        AnimPhaseStart, //Shouldn't start before the previous phase ends, so a specific entity shouldn't be in two animations at once.

        //Special priority for anything but the absolute last things that should occur.
        //This is the priority for things without a priority, to ensure they still occur before returning control to the player/AI
        None,
        //The last priorities are for returning control to the player or AI. 
        //This is last since the timeline needs to be in Display to be "reliable" for player decision making
        ReturnAIControl,
        ReturnPlayerControl,
        //For testing things that happen after a player gets control back. Likely to be cut.
        DebugLow,
    }
}
