using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Combatants
{
    /// <summary>
    /// Holds what turn should be taken next in a way that ensures only one turn is taken at a time
    /// </summary>
    /// <typeparam name="U">What object represents the next turn</typeparam>
    public class CurrentTurnHolder<U> where U : class
    {
        public CurrentTurnHolder() { }
        /// <summary>
        /// Who should get the next turn? This is the key part of the class
        /// </summary>
        public U? Current { get; private set; }
        /// <summary>
        /// Sets that the claimer should take the next turn
        /// </summary>
        /// <param name="turnClaimer"></param>
        /// <exception cref="TurnClashException"></exception>
        public void Claim(U turnClaimer)
        {
            if (Current is not null)
            {
                throw new TurnClashException($"Turn claimed by {Current}, {turnClaimer} cannot claim it");
            }
            Current = turnClaimer;
        }
        public void Clear()
        {
            if (Current is null)
            {
                throw new TurnClashException("No claim set. Likely you already cleared it");
            }
            Current = null;
        }
    }
}
