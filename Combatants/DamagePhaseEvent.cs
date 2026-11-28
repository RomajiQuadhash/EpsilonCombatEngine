using CoreMetrics;
using Meters;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Combatants
{
    /// <summary>
    /// The damage phase of an attack: adds a source of damage over time to a HealthMeter when it applies, and removes that same one when it is removed.
    /// The damage is what it was before Defense. The HealthMeter divides it by the Defense for as long as it stays.
    /// </summary>
    /// <remarks>Claude written, Romaji approved</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <param name="meter">The health the damage is dealt to</param>
    /// <param name="damage">The damage as it is before Defense: how much per tick (more than 0) and how often it ticks</param>
    /// <param name="waitingTime">How long from now until the damage starts. 0 or more</param>
    /// <param name="applyTime">How long the damage keeps coming before it stops. More than 0, since damage that takes no time is instant damage, which is a different thing</param>
    /// <param name="applyPriority">The priority while it is waiting to start</param>
    /// <param name="removePriority">The priority while it is waiting to stop</param>
    /// <param name="owningTimeline">The timeline this is for. Must be the meter's</param>
    public class DamagePhaseEvent<T>(HealthMeter<T> meter, ModInstance<T, int> damage, T waitingTime, T applyTime, PriorityRank? applyPriority, PriorityRank? removePriority, Timeline<T> owningTimeline) : TimedTempChange<T, int>(meter, waitingTime, ValidatedApplyTime(applyTime, damage), applyPriority, removePriority, owningTimeline) where T : INumber<T>
    {
        /// <summary>
        /// Checks what the damage phase is being made with, and gives the apply time back if it is all fine.
        /// It is done on the way into the base constructor so that a refused damage phase was never subscribed to the timeline.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">If the apply time is 0 or less, or the damage per tick is 0 or less</exception>
        private static T ValidatedApplyTime(T applyTime, ModInstance<T, int> damage)
        {
            if (applyTime <= T.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(applyTime), applyTime, "A damage phase has to last more than 0. Damage that takes no time is instant damage");
            }
            if (damage.PerTick <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "Damage has to be more than 0. Healing isn't divided by Defense, so it isn't a damage phase");
            }
            return applyTime;
        }
        /// <summary>
        /// The health the damage is dealt to, as the kind of meter this needs.
        /// </summary>
        public new HealthMeter<T> MeterAppliedTo => (HealthMeter<T>)base.MeterAppliedTo;
        /// <summary>
        /// The damage this adds to the meter while it is applied, as it is before Defense.
        /// </summary>
        public ModInstance<T, int> Damage { get; } = damage;
        /// <summary>
        /// The key the meter gave for the damage when this applied, which is what removes it again. Null until it applies.
        /// </summary>
        public ModifierKey? Key { get; private set; }
        /// <summary>
        /// True once EndEarly has been called. The damage is off the meter, or was never put on it.
        /// </summary>
        public bool HasEndedEarly { get; private set; }
        /// <summary>
        /// How much longer the damage would have kept coming when it was ended early: all of its apply time if it hadn't started yet. 0 unless it was ended early.
        /// </summary>
        public T TimeLeftWhenEnded { get; private set; } = T.Zero;
        /// <summary>
        /// How much longer it would have waited before starting when it was ended early. 0 if it had already started, or wasn't ended early.
        /// </summary>
        public T WaitLeftWhenEnded { get; private set; } = T.Zero;
        /// <summary>
        /// True between Suppress and Unsuppress. The damage phase carries on counting down as usual, but its damage is kept off the health meter.
        /// </summary>
        public bool IsSuppressed { get; private set; }

        protected override void ApplyChange()
        {
            if (HasEndedEarly)
            {
                //Ended before it started, so it never adds its damage. It is removed straight away, which finishes it.
                TimeToEvent = T.Zero;
                return;
            }
            if (IsSuppressed)
            {
                //It has started, but deals nothing until it is unsuppressed
                return;
            }
            Key = MeterAppliedTo.AddDamage(Damage);
        }
        protected override void RemoveChange()
        {
            if (Key is ModifierKey key)
            {
                MeterAppliedTo.RemoveDamage(key);
            }
        }
        /// <summary>
        /// Stops this damage phase now, for when whoever is dealing it can't carry on (they have been paused, say).
        /// If the damage was on the health meter it comes off at once, exactly as if the phase had ended by itself, and if it hadn't started it never does.
        /// CreateContinuation makes a damage phase for the time that was left.
        /// </summary>
        /// <remarks>
        /// An event can't always be taken off the timeline right away. So this one stays on it with no time left, and occurs the next time the timeline runs, doing nothing.
        /// It isn't finished (IsFinished) until then.
        /// </remarks>
        /// <returns>How much longer the damage would have kept coming, which is also TimeLeftWhenEnded. 0 if it had already finished</returns>
        public T EndEarly()
        {
            if (IsFinished || HasEndedEarly)
            {
                return TimeLeftWhenEnded;
            }
            HasEndedEarly = true;
            if (LifeCyclePhase == ApplyOrRemove.Apply)
            {
                TimeLeftWhenEnded = ApplyTime;
                WaitLeftWhenEnded = TimeToEvent;
            }
            else
            {
                TimeLeftWhenEnded = TimeToEvent;
                RemoveChange();
                Key = null; //So that occurring later doesn't try to take the damage off again
            }
            TimeToEvent = T.Zero;
            return TimeLeftWhenEnded;
        }
        /// <summary>
        /// Keeps this damage phase's damage off the health meter until Unsuppress is called, for when whoever is dealing it can't deal damage for a while (they have been nullified, say).
        /// Unlike ending it early, nothing waits: the damage phase keeps to its own times, so whatever it would have dealt while suppressed is lost.
        /// If the damage was on the health meter it comes off at once, exactly as if the phase had ended.
        /// </summary>
        public void Suppress()
        {
            if (IsSuppressed || IsFinished || HasEndedEarly)
            {
                return;
            }
            IsSuppressed = true;
            RemoveChange();
            Key = null;
        }
        /// <summary>
        /// Lets this damage phase deal its damage again. If it has started and not yet ended, the damage goes back onto the health meter as a new source
        /// for whatever is left of its time. If it hasn't started yet it starts as usual, and if it has ended there is nothing to put back.
        /// </summary>
        public void Unsuppress()
        {
            if (!IsSuppressed)
            {
                return;
            }
            IsSuppressed = false;
            if (LifeCyclePhase == ApplyOrRemove.Remove && !IsFinished && !HasEndedEarly)
            {
                Key = MeterAppliedTo.AddDamage(Damage);
            }
        }
        /// <summary>
        /// Makes a damage phase that deals the same damage to the same health for the time this one had left when it was ended early.
        /// It is a new damage phase in every way: when it starts, the damage goes onto the health meter as a new source.
        /// </summary>
        /// <remarks>Like other events, it isn't put on the timeline. Use AddEvent or AddPossibleEvent once it is made.</remarks>
        /// <param name="waitingTime">How long from now until the damage starts again</param>
        /// <returns>The new damage phase</returns>
        /// <exception cref="InvalidOperationException">If this wasn't ended early, or it had no time left when it was</exception>
        public virtual DamagePhaseEvent<T> CreateContinuation(T waitingTime)
        {
            if (!HasEndedEarly)
            {
                throw new InvalidOperationException("Only a damage phase that was ended early has anything to continue");
            }
            if (TimeLeftWhenEnded <= T.Zero)
            {
                throw new InvalidOperationException("This damage phase had no time left when it was ended early, so there is nothing to continue");
            }
            return new DamagePhaseEvent<T>(MeterAppliedTo, Damage, waitingTime, TimeLeftWhenEnded, ApplyPriority, RemovePriority, OwningTimeline);
        }
        protected override TimedTempChange<T, int> CreateEmptyForClone(Timeline<T> owningTimeline, CloneContext context)
        {
            return new DamagePhaseEvent<T>(context.GetOrClone(MeterAppliedTo), Damage, WaitingTime, ApplyTime, ApplyPriority, RemovePriority, owningTimeline);
        }
        protected override void CopyPropertiesForClone(TimedTempChange<T, int> clone, CloneContext context)
        {
            base.CopyPropertiesForClone(clone, context);
            if (clone is DamagePhaseEvent<T> damageClone)
            {
                //The cloned meter has the same damage under the same keys, so the key works on it as it is
                damageClone.Key = Key;
                damageClone.HasEndedEarly = HasEndedEarly;
                damageClone.TimeLeftWhenEnded = TimeLeftWhenEnded;
                damageClone.WaitLeftWhenEnded = WaitLeftWhenEnded;
                damageClone.IsSuppressed = IsSuppressed;
                return;
            }
            throw new UnrecoverableCloneException("Somehow, we cloned a DamagePhaseEvent and got a different type back. This should never happen.");
        }
    }
}
