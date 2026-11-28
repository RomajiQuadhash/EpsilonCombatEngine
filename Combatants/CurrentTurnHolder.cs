using CoreMetrics;

namespace Combatants
{
    /// <summary>
    /// Holds what turn should be taken next in a way that ensures only one turn is taken at a time
    /// </summary>
    /// <typeparam name="U">What object represents the next turn</typeparam>
    public class CurrentTurnHolder<U> : ICombatCloneable where U : class, ICombatCloneable
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
        /// <summary>
        /// Clones this holder, holding the clone of whoever holds the turn here (or nobody, if nobody does).
        /// </summary>
        /// <remarks>
        /// Whoever holds the turn is cloned through the context if it hasn't been yet.
        /// If that one clones this holder as part of cloning itself, it has to register itself with the context before it does, or the two will go round forever.
        /// </remarks>
        public object Clone(CloneContext context)
        {
            if (context.TryGet(this, out CurrentTurnHolder<U> existingClone))
            {
                return existingClone;
            }
            CurrentTurnHolder<U> clone = new();
            context.Register(this, clone);
            clone.Current = Current is null ? null : context.GetOrClone(Current);
            return clone;
        }
    }
}
