using Combatants;
using System.Collections.Generic;
using System.Numerics;
using Timeline;

namespace Combat
{
    public abstract class AbstractCombat<T> where T : INumber<T>
    {
        /// <summary>
        /// Contains what combatants are present and what commander if any are assigned. If there is no commander assigned, the turns are skipped.
        /// </summary>
        public abstract IReadOnlyDictionary<AbstractCombatant<T>,ICommander<T>?> CombatantsAndCommanders { get; }
        /// <summary>
        /// Takes the UUID of a combatant and returns what combatant they are. Useful from mapping actions back from clones.
        /// </summary>
        public abstract IReadOnlyDictionary<int,AbstractCombatant<T>> ConvertToCombatant { get; }
        //The combat is not responsible for storing them in an order. That is the UI's job

        /// <summary>
        /// Holds what turn it is. Should be shared with every Combatant
        /// </summary>
        public abstract CurrentTurnHolder<AbstractCombatant<T>> TurnHolder { get; }
        /// <summary>
        /// The timeline that holds all of the events
        /// </summary>
        public abstract Timeline<T> Timeline { get; }
        /// <summary>
        /// Is the current combat a simulation? 
        /// </summary>
        /// <remarks>Might change to be private/protected if I don't find a good reason for outsiders to know it is a simulation</remarks>
        public abstract bool IsSimulation { get; }
        /// <summary>
        /// The RNG all luck related Actions or combatant decisions should use.
        /// </summary>
        /// <remarks>Not every output will be actually random in a simulation.</remarks>
        public abstract ICombatRng SharedRng { get; }

        #region Combatant management
        /// <summary>
        /// Adds a combatant to the combat
        /// </summary>
        /// <typeparam name="C">What kind of combatant is added. Must match the types allowed by the commander if present</typeparam>
        /// <param name="combatant">The combatant that will be added to the combat</param>
        /// <param name="commander">What commander is in charge of this combatant. Null means they will take no turns</param>
        public abstract void RegisterNewCombatant<C>(C combatant, AbstractCommander<T,C>? commander) where C : AbstractCombatant<T>;
        /// <summary>
        /// Removes a combatant entirely from the simulation. Mostly a cleanup thing if nothing involving it exists anymore. 
        /// Don't do this for say, being knocked out, since it can invalidate anything planning to go that way
        /// </summary>
        /// <param name="combatant">What combatant us to be removed.</param>
        /// <returns>True if successfully removed</returns>
        public abstract bool RemoveCombatant(AbstractCombatant<T> combatant);
        //Tagging out would be removing and registring,
        //using the same UUID for the new combatant if anything going to the old combatant should instead go to the new one
        #endregion
        /// <summary>
        /// Creates a clone of the combat and everything inside (except for Commanders, which are all nulled out).
        /// The clone is created with IsSimulation true, even if the original wasn't a simulation.
        /// The clone is also created with a different SharedRng, that is better for simulating
        /// </summary>
        /// <returns>A clone of the combat</returns>
        /// <remarks>There might be a reason to have options but I don't know any, so I'm leaving them blank for now.</remarks>
        public abstract AbstractCombat<T> CloneForSimulation();

        /// <summary>
        /// Runs the timeline until it terminates. 
        /// A concrete class would want to raise events to tell a UI about what happened, but I don't have such a UI, so that'll be for implementation.
        /// </summary>
        /// <returns>What the timeline terminated with.</returns>
        public abstract TerminationType Run();
    }
}
