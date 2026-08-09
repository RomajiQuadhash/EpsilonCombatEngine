using System.Numerics;
using Meters;
using Timeline;

namespace Combatants
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public abstract class AbstractCombatant<T> where T : INumber<T>
    {
        /// <summary>
        /// The timeline that this combatant is a part of.
        /// Should be set when the combatant is created, and should not be changed after that.
        /// </summary>
        public required Timeline<T> OwningTimeline { get; set; }
        /// <summary>
        /// What the combatant's health is. Also includes the combatant's max and min health (min only not zero if the combatant is bleeding).
        /// </summary>
        /// <remarks>Should be a type designed to take changes over time well.</remarks>
        public required AbstractMeter<T,int> HealthMeter { get; set; }

        #region Status Flags
        //These should be used for universal status flags, like "is dead", "is KO'd", "can be killed", etc.
        //Other flags should likely use some kind of set or dictionary for more flexibility, extensibility, and keeping core ideas separate from more specific ideas.
        //These should all be "computed" properties, to avoid any illegal states (like being active and dead at the same time).

        /// <summary>
        /// Can this combatant take actions?
        /// </summary>
        /// <remarks>Likely similar to TurnCard is not null, but there might be cases where they're different</remarks>
        public abstract bool IsActive { get; }
        /// <summary>
        /// Might be per enemy or battle generally (like a human enemy combatant not killable but the aliens are)
        /// </summary>
        public abstract bool CanBeKilled { get; }
        /// <summary>
        /// If the combatant is dead. If this is a player, this will be a game over condition
        /// </summary>
        public abstract bool IsDead { get; }
        /// <summary>
        /// Is the combatant in a state where they can go to negative health and die? Note that if a combatant is bleeding,
        /// they remain active past 0 HP but will be KO'd if they stop below 0 or die if they reach their negative health limit.
        /// </summary>
        /// <remarks>This should likely be tied to if the minimum of the HealthMeter is negative, rather than being directly set.
        /// However, I could imagine setting this to trigger a change in the HealthMeter</remarks>
        public abstract bool IsBleeding { get; }
        /// <summary>
        /// HP at 0 and not bleeding or was KO'd when bleeding started?
        /// Cleared if the combatant is healed above 0 or if they die.
        /// </summary>
        /// <remarks>Will need slightly more logic than just "HP<=0?". Be careful using HP<=0.</remarks>
        public abstract bool IsKOd { get; }
        /// <summary>
        /// Is the combatant unable to act but not dead or KO'd? Check specific flags for more details
        /// </summary>
        public abstract bool IsOtherwiseIncapacitated { get; }
        #endregion
        #region Pace, Speed, and Turns
        //"Pace" is a general term for properties about when a combatant can act.
        //Speed is a specific property used in relation to other combatants' speeds to determine when they can act.
        //Turns are when a combatant can make a decision about what to do next and is loosely based on their speed but also,
        //what actions they last took.

        /// <summary>
        /// Represents when the combatant's next turn will occur.
        /// May be updated if the combatant is waiting for something to happen and this is the longest they will wait before giving up.
        /// If this is null, the combatant should likely not be considered active, but use IsActive for that, since it could be
        /// that the combatant is active but can't compute when their next turn will be.
        /// </summary>
        public AbstractTurnCard<T, object>? TurnCard { get; set; } = null;
        /// <summary>
        /// How fast the combatant is. Note that not all elements of the events this combatant takes will be based on Speed,
        /// but higher Speed should generally reduce the time spent waiting between taking meaningful actions (windup, recovery, etc.).
        /// Speed is only reletave to other combatants' speeds, so if you get faster and you're already the fastest,
        /// it will slow others down rather than make you faster.
        /// </summary>
        /// <remarks>Uses the timeline's numerical unit since that should be the "most precise" unit and common to all.</remarks>
        public abstract T Speed { get; }
        /// <summary>
        /// Should be a meter set based on the max speed of the combat, the min-delay of the next action, and the combatant's speed.
        /// Should be such that when the meter reaches its max, the combatant's next action can occur.
        /// </summary>
        public abstract AbstractMeter<T, T>? DelayBar { get; }
        #endregion

    }
}
