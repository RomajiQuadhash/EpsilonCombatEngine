using Combatants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Combat
{
    /// <summary>
    /// Holds onto one or more combatant and does the actions (either based on its own AI or the player selections) they
    /// are allowed to do on their turn
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline in the combat</typeparam>
    /// <typeparam name="C">The type of combatants this commander should be used for</typeparam>
    public abstract class AbstractCommander<T,C> : ICommander<T> where T : INumber<T> where C : AbstractCombatant<T>
    {
        /// <summary>
        /// Take the turn of one of your combatants
        /// </summary>
        /// <param name="clonedCombatant">The combatant that is being acted upon. Note this is from the simulationBase clone, any modification on the real combatant must be returned as Actions</param>
        /// <param name="simulationBase">A clone of the combat, for making futher clones for testing. Done so you can't change anyone else.</param>
        /// <returns>A list of actions to take</returns>
        public abstract IEnumerable<Action<T>> TakeTurn(C clonedCombatant, AbstractCombat<T> simulationBase);
    }
}
