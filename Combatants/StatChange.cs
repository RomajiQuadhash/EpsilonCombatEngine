using Meters;
using Rationalz;
using System.Numerics;
using Timeline.OkazoOptionalProperties;

namespace Combatants
{
    /// <summary>
    /// Makes buffs and debuffs: temporary changes to the Ratio of one of a combatant's stats.
    /// </summary>
    /// <remarks>Claude written, Romaji approved</remarks>
    public static class StatChange
    {
        /// <summary>
        /// Makes the event that changes one of a combatant's stats by an amount of Ratio for a length of time, and then takes back what it actually changed.
        /// A change that runs into the stat's limit only takes back the part that fit, so two changes that overlap at a limit never leave the stat lower or higher than it started.
        /// </summary>
        /// <remarks>
        /// Like other events, it isn't put on the timeline. Use AddEvent or AddPossibleEvent once it is made.
        /// Speed is changed the same way as any other stat.
        /// </remarks>
        /// <typeparam name="T">The numeric type used for the timeline</typeparam>
        /// <param name="combatant">The combatant whose stat is changed</param>
        /// <param name="stat">Which stat to change</param>
        /// <param name="ratioChange">How much Ratio to add while this is applied. Negative for a debuff</param>
        /// <param name="waitingTime">How long from now until the change applies. Negative for one that should have applied in the past, which makes the timeline rewind to then</param>
        /// <param name="applyTime">How long the change stays applied before it is removed. 0 or more</param>
        /// <param name="applyPriority">The priority while it is waiting to apply. MeterChangePhaseStart unless there is a reason for another</param>
        /// <param name="removePriority">The priority while it is waiting to be removed. MeterChangePhaseEnd unless there is a reason for another</param>
        /// <returns>The event, for the combatant's stat on the combatant's timeline</returns>
        /// <exception cref="MissingStatException">If the combatant doesn't have that stat. Nothing is made in that case</exception>
        /// <exception cref="ArgumentOutOfRangeException">If applyTime is negative</exception>
        public static TempInstantChange<T, Rational<long>> Create<T>(AbstractCombatant<T> combatant, CombatStats stat, Rational<long> ratioChange, T waitingTime, T applyTime,
            PriorityRank? applyPriority, PriorityRank? removePriority) where T : INumber<T>
        {
            IDictionary<CombatStats, StatModificationMeter<T>> stats = combatant.ChangeableStats;
            if (!stats.TryGetValue(stat, out StatModificationMeter<T>? meter))
            {
                throw new MissingStatException(combatant, stat, stats.Keys);
            }
            return new TempInstantChange<T, Rational<long>>(meter, ratioChange, waitingTime, applyTime, applyPriority, removePriority, combatant.OwningTimeline);
        }
    }
    /// <summary>
    /// Thrown when something tries to use a combat stat on a combatant that doesn't have it (an Attack buff on a combatant with no Attack).
    /// It says which combatant and which stat, so whatever asked can decide whether that is a mistake or just a combatant the effect doesn't apply to.
    /// </summary>
    public class MissingStatException : Exception
    {
        /// <summary>
        /// The combatant that doesn't have the stat.
        /// </summary>
        public object Combatant { get; }
        /// <summary>
        /// The stat it doesn't have.
        /// </summary>
        public CombatStats Stat { get; }
        /// <summary>
        /// The stats it does have.
        /// </summary>
        public IReadOnlyCollection<CombatStats> StatsItHas { get; }
        /// <param name="combatant">The combatant that doesn't have the stat</param>
        /// <param name="stat">The stat it doesn't have</param>
        /// <param name="statsItHas">The stats it does have</param>
        public MissingStatException(object combatant, CombatStats stat, IEnumerable<CombatStats> statsItHas)
            : base($"{combatant} has no {stat} stat. The stats it has are: {string.Join(", ", statsItHas)}")
        {
            Combatant = combatant;
            Stat = stat;
            StatsItHas = statsItHas.ToList();
        }
    }
}
