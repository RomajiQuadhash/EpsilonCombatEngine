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
    /// Represents an action to take on a combat.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline inside</typeparam>
    /// <param name="CombatantUUID">The UUID of the combatant </param>
    public abstract record Action<T>(int CombatantUUID) where T : INumber<T>
    {
        /// <summary>
        /// Called by Combat to perform this action
        /// </summary>
        /// <param name="combat">What combat are we trying to do this action in?</param>
        /// <returns>True if the action happened, false otherwise</returns>
        /// <exception cref="InvalidActionException">Thrown if trying to perform this action is so wrong we shouldn't try</exception>
        public bool CommitAction(AbstractCombat<T> combat)
        {
            var combatant = TryGetCombatantByUUID(combat, CombatantUUID);
            if (!combatant.IsActive)
            {
                throw new InvalidActionException($"{combatant} is not active, so it can't take {this}");
            }
            if (OtherInvalidatingIssues(combat,combatant))
            {
                return false;
            }
            CommitValidatedAction(combat, combatant);
            return true;
        }
        protected AbstractCombatant<T> TryGetCombatantByUUID(AbstractCombat<T> combat,int UUID)
        {
            if (!combat.ConvertToCombatant.TryGetValue(UUID, out var combatant))
            {
                throw new InvalidActionException($"Combat {combat} doesn't have a combatant with UUID {UUID} " +
                                                 $"{this} is an invalid action.");
            }
            return combatant;
        }
        /// <summary>
        /// Override if there are other way for this action to be invalid to perform if the combatant exists and is active.
        /// </summary>
        /// <param name="combat">What combat are we trying to do this action in?</param>
        /// <param name="combatant">Which combatant are we?</param>
        /// <returns>True if we shouldn't do the action but it isn't worth throwing an exception</returns>
        /// <exception cref="InvalidActionException">If something is so dramatically wrong it should be an exception</exception>
        protected virtual bool OtherInvalidatingIssues(AbstractCombat<T> combat, AbstractCombatant<T> combatant){ return false; }
        /// <summary>
        /// This is the method that actually modifies the Combat to do the action
        /// </summary>
        /// <param name="combat">What combat are we trying to do this action in?</param>
        /// <param name="combatant">Which combatant are we?</param>
        protected abstract void CommitValidatedAction(AbstractCombat<T> combat, AbstractCombatant<T> combatant);
    }
}
