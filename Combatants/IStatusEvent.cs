using Meters;
using System.Numerics;
using Timeline;

namespace Combatants
{
    /// <summary>
    /// What every status effect event has in common. Like a temporary change to a meter, it occurs twice: once to apply the status and once to remove it.
    /// It always applies after a set length of time. What differs between the kinds is what decides when it is removed:
    /// another set length of time, or a meter reaching a value.
    /// Both times it occurs, it tells the combatant the status is for (through OnOccur). That combatant is the one that decides what to do about it.
    /// LifeCyclePhase says which of the two it is at that moment.
    /// A status is only meant to be removed by an event with the same owner as the status has by then.
    /// So a status that was taken over by another combatant is left alone when its first owner's event comes to remove it.
    /// Everything that implements this is an Okazo. AsOkazo gives it as one, for putting it on the timeline.
    /// </summary>
    /// <remarks>Claude written, Romaji approved</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public interface IStatusEvent<T> where T : INumber<T>
    {
        /// <summary>
        /// The status effect this applies and then removes.
        /// </summary>
        StatusEffects Status { get; }
        /// <summary>
        /// The combatant the status is for.
        /// </summary>
        AbstractCombatant<T> Target { get; }
        /// <summary>
        /// The combatant that owns the status this applies. Null if nobody owns it.
        /// </summary>
        AbstractCombatant<T>? Owner { get; }
        /// <summary>
        /// Whether this is going to apply the status next, or remove it. While it is occurring, this is the one it is doing.
        /// </summary>
        ApplyOrRemove LifeCyclePhase { get; }
        /// <summary>
        /// True once this has occurred for the second time, to remove the status. After that it does nothing more.
        /// </summary>
        bool IsFinished { get; }
        /// <summary>
        /// True once Cancel has been called. A cancelled status event applies and removes nothing, and whoever it tells should take no notice of it.
        /// </summary>
        bool IsCancelled { get; }
        /// <summary>
        /// Stops this from applying or removing its status, for when the combatant it is for refuses it (it already has that status) or everything on that combatant is being cleared.
        /// </summary>
        /// <remarks>
        /// An event can't always be taken off the timeline right away. So it is left with no time to wait, occurs the next time the timeline runs, and is finished.
        /// It still tells its listeners when it occurs. IsCancelled is how they know to take no notice.
        /// </remarks>
        void Cancel();
        /// <summary>
        /// This same event as an Okazo, which is what the timeline's AddEvent and AddPossibleEvent take.
        /// </summary>
        Okazo<T> AsOkazo { get; }
    }
}
