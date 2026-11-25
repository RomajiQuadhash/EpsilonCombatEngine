using CoreMetrics;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Meters
{
    /// <summary>
    /// A temporary source of change for a RateManagedMeter: adds a ModInstance when it applies, and removes that same one when it is removed.
    /// The meter works out what that does to its rate, including merging it with whatever else is affecting it.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for the affected meter. Integer</typeparam>
    /// <param name="meter">The meter the change applies to</param>
    /// <param name="modifier">The source of change to add to the meter while this is applied</param>
    /// <param name="waitingTime">How long from now until the change applies. 0 or more</param>
    /// <param name="applyTime">How long the change stays applied before it is removed. 0 or more</param>
    /// <param name="applyPriority">The priority while it is waiting to apply</param>
    /// <param name="removePriority">The priority while it is waiting to be removed</param>
    /// <param name="owningTimeline">The timeline this is for. Must be the meter's</param>
    /// <remarks>Claude with only minor changes</remarks>
    public class TempRMMChange<T, M>(RateManagedMeter<T, M> meter, ModInstance<T, M> modifier, T waitingTime, T applyTime, PriorityRank? applyPriority, PriorityRank? removePriority, Timeline<T> owningTimeline) : TimedTempChange<T, M>(meter, waitingTime, applyTime, applyPriority, removePriority, owningTimeline) where T : INumber<T> where M : IBinaryInteger<M>
    {
        /// <summary>
        /// The meter the change applies to, as the kind of meter this needs.
        /// </summary>
        public new RateManagedMeter<T, M> MeterAppliedTo => (RateManagedMeter<T, M>)base.MeterAppliedTo;
        /// <summary>
        /// The source of change this adds to the meter while it is applied.
        /// </summary>
        public ModInstance<T, M> Modifier { get; } = modifier;
        /// <summary>
        /// The key the meter gave for the modifier when this applied, which is what removes it again. Null until it applies.
        /// </summary>
        public ModifierKey? Key { get; private set; }

        protected override void ApplyChange()
        {
            Key = MeterAppliedTo.Add(Modifier);
        }
        protected override void RemoveChange()
        {
            if (Key is ModifierKey key)
            {
                MeterAppliedTo.Remove(key);
            }
        }
        /// <summary>
        /// Makes the event for changing a meter by a set amount over a set length of time, split into a set number of equal ticks.
        /// If the length of time is zero, that is one instant change, so an InstantChangeEvent is made and the ticks and rounding aren't used.
        /// Otherwise it is a TempRMMChange whose ModInstance adds the amount divided by the ticks, once every time divided by the ticks, for that time.
        /// </summary>
        /// <remarks>
        /// Like the constructors, this doesn't put the event on the timeline and doesn't touch the meter. Use AddEvent or AddPossibleEvent on the AsOkazo of what it returns.
        /// The ticks are what this source asks for. If the meter has other sources at the same time, it merges them all into its own ticks.
        /// </remarks>
        /// <param name="meter">The meter the change applies to</param>
        /// <param name="totalChange">How much the meter should have changed by once the whole time has passed. Negative to take away</param>
        /// <param name="waitingTime">How long from now until the change starts</param>
        /// <param name="lerpTime">How long the change takes, from when it starts. 0 for all at once</param>
        /// <param name="ticks">How many ticks to split the change into. 1 or more</param>
        /// <param name="rounding">What to do when the change doesn't divide evenly into the ticks</param>
        /// <param name="applyPriority">The priority while the change is waiting to start, whether it is in ticks or all at once</param>
        /// <param name="removePriority">The priority while a change in ticks is waiting to stop</param>
        /// <param name="owningTimeline">The timeline this is for. Must be the meter's</param>
        /// <param name="actualTotalChange">
        /// How much the meter would change by: what the ticks add up to after rounding, or as much of that as fits between the meter's minimum and maximum,
        /// going by where the meter is right now. The meter may be somewhere else by the time the change starts, so this is an estimate.
        /// </param>
        /// <returns>The InstantChangeEvent or TempRMMChange that makes the change</returns>
        /// <exception cref="ArgumentOutOfRangeException">If lerpTime is negative, or the change takes time and ticks is less than 1</exception>
        /// <exception cref="ArgumentException">If the time doesn't divide into that many ticks on this kind of timeline (10 into 3 on a whole-number timeline)</exception>
        public static IMeterChangeEvent<T, M> CreateEventFromTickedLerp(RateManagedMeter<T, M> meter, M totalChange, T waitingTime, T lerpTime, int ticks, TickRounding rounding,
            PriorityRank? applyPriority, PriorityRank? removePriority, Timeline<T> owningTimeline, out M actualTotalChange)
        {
            if (T.IsNegative(lerpTime))
            {
                throw new ArgumentOutOfRangeException(nameof(lerpTime), lerpTime, "The time a change takes can't be negative");
            }
            if (T.IsZero(lerpTime))
            {
                actualTotalChange = M.Clamp(meter.CurrentValue + totalChange, meter.MinValue, meter.MaxValue) - meter.CurrentValue;
                return new InstantChangeEvent<T, M>(meter, totalChange, waitingTime, applyPriority, owningTimeline);
            }
            if (ticks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(ticks), ticks, "A change that takes time needs at least 1 tick");
            }
            T tickRate = lerpTime / T.CreateChecked(ticks);
            if (RationalConversion.Compare(RationalConversion.ToRational(tickRate) * ticks, RationalConversion.ToRational(lerpTime)) != 0)
            {
                throw new ArgumentException($"A time of {lerpTime} doesn't divide into {ticks} ticks on this timeline");
            }
            M count = M.CreateChecked(ticks);
            M perTick = totalChange / count; //Drops any fraction, toward zero
            M leftOver = totalChange - perTick * count;
            bool roundAway = rounding switch
            {
                TickRounding.AwayFromZero => !M.IsZero(leftOver),
                TickRounding.Nearest => M.Abs(leftOver) * (M.One + M.One) >= count,
                _ => false,
            };
            if (roundAway)
            {
                perTick += M.CreateChecked(M.Sign(totalChange));
            }
            M roundedTotal = perTick * count;
            actualTotalChange = M.Clamp(meter.CurrentValue + roundedTotal, meter.MinValue, meter.MaxValue) - meter.CurrentValue;
            return new TempRMMChange<T, M>(meter, new ModInstance<T, M>(perTick, tickRate), waitingTime, lerpTime, applyPriority, removePriority, owningTimeline);
        }
        protected override TimedTempChange<T, M> CreateEmptyForClone(Timeline<T> owningTimeline, CloneContext context)
        {
            return new TempRMMChange<T, M>(context.GetOrClone(MeterAppliedTo), Modifier, WaitingTime, ApplyTime, ApplyPriority, RemovePriority, owningTimeline);
        }
        protected override void CopyPropertiesForClone(TimedTempChange<T, M> clone, CloneContext context)
        {
            base.CopyPropertiesForClone(clone, context);
            if (clone is TempRMMChange<T, M> modInstanceClone)
            {
                //The cloned meter has the same modifiers under the same keys, so the key works on it as it is
                modInstanceClone.Key = Key;
                return;
            }
            throw new UnrecoverableCloneException("Somehow, we cloned a TempRMMChange and got a different type back. This should never happen.");
        }
    }
    /// <summary>
    /// What to do when a change doesn't divide evenly into the number of ticks it is being split into.
    /// Every tick adds the same whole amount, so the total that is actually delivered can be a little under or over what was asked for.
    /// </summary>
    public enum TickRounding
    {
        /// <summary>
        /// Each tick drops the fraction, so the total is never more than was asked for. 10 over 4 ticks is 2 a tick, 8 in all.
        /// If the change is smaller than the number of ticks, each tick is 0 and nothing happens.
        /// </summary>
        TowardZero,
        /// <summary>
        /// Each tick is rounded up in size, so the total is never less than was asked for. 10 over 4 ticks is 3 a tick, 12 in all.
        /// </summary>
        AwayFromZero,
        /// <summary>
        /// Each tick is rounded to the nearest whole amount, with a half rounding up in size. 10 over 4 ticks is 3 a tick, 12 in all, and 9 over 4 is 2 a tick, 8 in all.
        /// </summary>
        Nearest
    }
}
