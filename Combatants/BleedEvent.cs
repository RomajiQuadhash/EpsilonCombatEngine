using CoreMetrics;
using Meters;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Combatants
{
    /// <summary>
    /// A bleed: a damage phase on a combatant's health that also tells that combatant when it starts and when it ends (through OnOccur).
    /// The damage always happens, the same as any damage phase. What bleeding means beyond the damage is up to the combatant, which keeps count of the bleeds on it:
    /// while it has any it can go below 0 health without being knocked out, and can die.
    /// Bleeding isn't a status effect, so a combatant can have several of these at once.
    /// The combatant hears about it before the damage goes onto its health, so it can get its health ready first.
    /// Like other events, it isn't put on the timeline automatically. Use AddEvent or AddPossibleEvent once it is made.
    /// </summary>
    /// <remarks>Claude written, Romaji approved</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public class BleedEvent<T> : DamagePhaseEvent<T> where T : INumber<T>
    {
        /// <summary>
        /// The combatant that is bleeding.
        /// </summary>
        public AbstractCombatant<T> Target { get; private set; }
        /// <summary>
        /// The combatant that caused the bleed. Null if nobody did.
        /// </summary>
        public AbstractCombatant<T>? Owner { get; private set; }
        /// <summary>
        /// Makes the bleed on the target's health, with the target listening to it.
        /// </summary>
        /// <param name="target">The combatant that is bleeding</param>
        /// <param name="owner">The combatant that caused the bleed. Null if nobody did</param>
        /// <param name="damage">The damage as it is before Defense: how much per tick (more than 0) and how often it ticks</param>
        /// <param name="waitingTime">How long from now until the bleed starts. Negative for one that should have started in the past, which makes the timeline rewind to then</param>
        /// <param name="applyTime">How long the bleed lasts. More than 0</param>
        /// <param name="applyPriority">The priority while it is waiting to start: PlayerBleedStart or EnemyBleedStart</param>
        /// <param name="removePriority">The priority while it is waiting to stop: PlayerBleedEnd or EnemyBleedEnd</param>
        /// <exception cref="ArgumentOutOfRangeException">If the apply time is 0 or less, or the damage per tick is 0 or less</exception>
        public BleedEvent(AbstractCombatant<T> target, AbstractCombatant<T>? owner, ModInstance<T, int> damage, T waitingTime, T applyTime, PriorityRank? applyPriority, PriorityRank? removePriority)
            : this(target, owner, target.HealthMeter, target.OwningTimeline, damage, waitingTime, applyTime, applyPriority, removePriority)
        {
            Occurring += Target.OnOccur;
        }
        /// <summary>
        /// Constructor that only sets what it is given, for the public constructor and for clones. The target doesn't listen to the event it makes.
        /// </summary>
        /// <param name="health">The health the damage goes onto: the target's, or for a clone, the clone of it</param>
        /// <param name="owningTimeline">The timeline this is for</param>
        protected BleedEvent(AbstractCombatant<T> target, AbstractCombatant<T>? owner, HealthMeter<T> health, Timeline<T> owningTimeline, ModInstance<T, int> damage, T waitingTime, T applyTime, PriorityRank? applyPriority, PriorityRank? removePriority)
            : base(health, damage, waitingTime, applyTime, applyPriority, removePriority, owningTimeline)
        {
            Target = target;
            Owner = owner;
        }
        /// <summary>
        /// Makes a bleed on the same combatant, from the same owner, for the time this one had left when it was ended early.
        /// It is a new bleed in every way, so the combatant hears about it starting and ending as it would any other.
        /// </summary>
        /// <param name="waitingTime">How long from now until the bleed starts again</param>
        /// <exception cref="InvalidOperationException">If this wasn't ended early, or it had no time left when it was</exception>
        public override DamagePhaseEvent<T> CreateContinuation(T waitingTime)
        {
            if (!HasEndedEarly)
            {
                throw new InvalidOperationException("Only a bleed that was ended early has anything to continue");
            }
            if (TimeLeftWhenEnded <= T.Zero)
            {
                throw new InvalidOperationException("This bleed had no time left when it was ended early, so there is nothing to continue");
            }
            return new BleedEvent<T>(Target, Owner, Damage, waitingTime, TimeLeftWhenEnded, ApplyPriority, RemovePriority);
        }
        protected override TimedTempChange<T, int> CreateEmptyForClone(Timeline<T> owningTimeline, CloneContext context)
        {
            //The combatants are not cloned here. They are swapped in by CopyPropertiesForClone, once this clone is registered.
            //A combatant keeps hold of the events that are for it, so cloning one from here would have it ask for this event's clone before there is one,
            //and a second clone of this event would get made.
            //Rewiring is what makes the cloned target listen to it.
            return new BleedEvent<T>(Target, Owner, context.GetOrClone(MeterAppliedTo), owningTimeline, Damage, WaitingTime, ApplyTime, ApplyPriority, RemovePriority);
        }
        protected override void CopyPropertiesForClone(TimedTempChange<T, int> clone, CloneContext context)
        {
            base.CopyPropertiesForClone(clone, context);
            if (clone is BleedEvent<T> bleedClone)
            {
                bleedClone.Target = context.GetOrClone(Target);
                bleedClone.Owner = Owner is null ? null : context.GetOrClone(Owner);
                return;
            }
            throw new UnrecoverableCloneException("Somehow, we cloned a BleedEvent and got a different type back. This should never happen.");
        }
        public override string ToString()
        {
            return $"Bleed on {Target}. " + base.ToString();
        }
    }
}
