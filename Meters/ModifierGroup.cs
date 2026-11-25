using CoreMetrics;
using Rationalz;
using System.Numerics;

namespace Meters
{
    /// <summary>
    /// The bookkeeping for one category of sources of change (ModInstances): which ones there are, their exact total, and which one is the strongest.
    /// A RateManagedMeter has one of these. A meter that treats some of its sources differently from others has one for each category,
    /// and decides for itself how their totals go together.
    /// </summary>
    /// <remarks>Claude written, Romaji </remarks>
    /// <typeparam name="T">The time unit of the timeline</typeparam>
    /// <typeparam name="M">The numeric type the sources change</typeparam>
    public class ModifierGroup<T, M> : ICombatCloneable where T : INumber<T> where M : INumber<M>
    {
        private readonly Dictionary<ModifierKey, ModInstance<T, M>> _modifiers = [];
        private long _nextId;
        /// <summary>
        /// The number that is in every key this group gives out, which is what tells its keys apart from another group's.
        /// 0 for a group made without a counter.
        /// </summary>
        public int GroupNumber { get; private set; }
        /// <summary>
        /// Makes a group with the number 0, for a group that is the only one its keys could be used with.
        /// </summary>
        public ModifierGroup()
        {
        }
        /// <summary>
        /// Makes a group with the next number from a counter, so its keys can't be mistaken for those of any other group made with that counter.
        /// </summary>
        /// <param name="counter">The counter shared by the groups that must be told apart</param>
        public ModifierGroup(ModifierGroupCounter counter)
        {
            GroupNumber = counter.TakeNextGroupNumber();
        }
        /// <summary>
        /// The modifiers in this group.
        /// Keys are unique identifiers this group gives the caller when they add a modifier, and can be used to remove or replace the modifier later.
        /// </summary>
        public IReadOnlyDictionary<ModifierKey, ModInstance<T, M>> Modifiers => _modifiers;
        /// <summary>
        /// Exact sum of every modifier's Magnitude. Never rounded, so removing everything that was added always gets back to exactly 0.
        /// </summary>
        public Rational<long> Sum { get; private set; } = Rational<long>.Zero;
        /// <summary>
        /// The largest |Magnitude| among the modifiers. 0 if there are none.
        /// </summary>
        public Rational<long> MaxMagnitude { get; private set; } = Rational<long>.Zero;
        /// <summary>
        /// The smallest TickRate among the modifiers that have the largest |Magnitude|. Only meaningful if there is at least one modifier.
        /// </summary>
        public T FastestTickRateAtMax { get; private set; } = T.One;

        /// <summary>
        /// Adds a source of change to this group.
        /// </summary>
        /// <param name="modifier">The source to add</param>
        /// <returns>The key to use to replace or remove this source later. Keep it, since the modifier itself is anonymous</returns>
        /// <exception cref="OverflowException">If the total of all the modifiers can't be stored exactly. Nothing is changed in that case</exception>
        public ModifierKey Add(ModInstance<T, M> modifier)
        {
            Rational<long> newSum = RationalConversion.NarrowExact(
                RationalConversion.Widen(Sum) + RationalConversion.Widen(modifier.Magnitude));
            //Everything that could throw has been done, so now it is safe to change things.
            ModifierKey key = new(_nextId++, GroupNumber);
            _modifiers.Add(key, modifier);
            Sum = newSum;
            NoteAdded(modifier);
            return key;
        }
        /// <summary>
        /// Removes a source of change from this group.
        /// </summary>
        /// <param name="key">The key Add gave for the source</param>
        /// <returns>True if it was removed, false if this group has no source with that key (in which case nothing changes)</returns>
        public bool Remove(ModifierKey key)
        {
            if (!_modifiers.TryGetValue(key, out ModInstance<T, M>? removed))
            {
                return false;
            }
            //With nothing left the sum is 0 by definition, so there's nothing to work out.
            Rational<long> newSum = _modifiers.Count == 1
                ? Rational<long>.Zero
                : RationalConversion.NarrowExact(RationalConversion.Widen(Sum) - RationalConversion.Widen(removed.Magnitude));
            _modifiers.Remove(key);
            Sum = newSum;
            NoteRemoved(removed);
            return true;
        }
        /// <summary>
        /// Swaps a source of change for a different one, keeping the same key. Use this when the effect behind a source changes strength.
        /// </summary>
        /// <param name="key">The key Add gave for the source</param>
        /// <param name="modifier">What to replace it with</param>
        /// <returns>True if it was replaced, false if this group has no source with that key (in which case nothing changes)</returns>
        /// <exception cref="OverflowException">If the total of all the modifiers can't be stored exactly. Nothing is changed in that case</exception>
        public bool Replace(ModifierKey key, ModInstance<T, M> modifier)
        {
            if (!_modifiers.TryGetValue(key, out ModInstance<T, M>? removed))
            {
                return false;
            }
            Rational<long> newSum = RationalConversion.NarrowExact(
                RationalConversion.Widen(Sum) + RationalConversion.Widen(modifier.Magnitude) - RationalConversion.Widen(removed.Magnitude));
            _modifiers[key] = modifier;
            Sum = newSum;
            //Removing first means a rescan (if needed) already sees the new modifier, and then adding it again changes nothing.
            NoteRemoved(removed);
            NoteAdded(modifier);
            return true;
        }
        /// <summary>
        /// Removes every source of change from this group at once.
        /// The keys it has given out stop working and are never given out again, so something still holding one can't remove a later source by mistake.
        /// </summary>
        public void Clear()
        {
            _modifiers.Clear();
            Sum = Rational<long>.Zero;
            MaxMagnitude = Rational<long>.Zero;
            FastestTickRateAtMax = T.One;
        }
        /// <summary>
        /// Clones this group: the same modifiers under the same keys, and the same group number, so every key that works on this group works on its clone.
        /// </summary>
        /// <remarks>
        /// The clone takes no number from any counter. It has this group's number whatever order the groups of a combat are cloned in,
        /// so whoever owns a group gets its clone from the context instead of making a new group.
        /// A ModInstance can't change, so both groups can refer to the same ones.
        /// </remarks>
        public object Clone(CloneContext context)
        {
            if (context.TryGet(this, out ModifierGroup<T, M> existingClone))
            {
                return existingClone;
            }
            ModifierGroup<T, M> clone = new();
            context.Register(this, clone);

            //Now update this to match
            foreach (KeyValuePair<ModifierKey, ModInstance<T, M>> modifier in _modifiers)
            {
                clone._modifiers.Add(modifier.Key, modifier.Value);
            }
            clone._nextId = _nextId;
            clone.GroupNumber = GroupNumber;
            clone.Sum = Sum;
            clone.MaxMagnitude = MaxMagnitude;
            clone.FastestTickRateAtMax = FastestTickRateAtMax;
            return clone;
        }
        #region Strongest modifier tracking
        /// <summary>
        /// Updates the strongest modifier after one is added. Only needs to look at the new one.
        /// </summary>
        private void NoteAdded(ModInstance<T, M> added)
        {
            Rational<long> magnitude = Rational<long>.Abs(added.Magnitude);
            int comparison = _modifiers.Count == 1 ? 1 : RationalConversion.Compare(magnitude, MaxMagnitude);
            if (comparison > 0)
            {
                MaxMagnitude = magnitude;
                FastestTickRateAtMax = added.TickRate;
            }
            else if (comparison == 0 && added.TickRate < FastestTickRateAtMax)
            {
                //Equally strong, so the faster one decides the tick rate
                FastestTickRateAtMax = added.TickRate;
            }
        }
        /// <summary>
        /// Updates the strongest modifier after one is removed. Only looks through the rest if the removed one was as strong as the strongest.
        /// </summary>
        /// <remarks>Compares by value, since two different modifiers can be equally strong</remarks>
        private void NoteRemoved(ModInstance<T, M> removed)
        {
            if (_modifiers.Count == 0)
            {
                //Nothing left to be the strongest
                MaxMagnitude = Rational<long>.Zero;
                FastestTickRateAtMax = T.One;
                return;
            }
            if (RationalConversion.Compare(Rational<long>.Abs(removed.Magnitude), MaxMagnitude) < 0)
            {
                //Something stronger is still there, so nothing changes
                return;
            }
            bool first = true;
            foreach (ModInstance<T, M> modifier in _modifiers.Values)
            {
                Rational<long> magnitude = Rational<long>.Abs(modifier.Magnitude);
                int comparison = first ? 1 : RationalConversion.Compare(magnitude, MaxMagnitude);
                if (comparison > 0 || (comparison == 0 && modifier.TickRate < FastestTickRateAtMax))
                {
                    MaxMagnitude = magnitude;
                    FastestTickRateAtMax = modifier.TickRate;
                }
                first = false;
            }
        }
        #endregion
        public override string ToString()
        {
            return $"{_modifiers.Count} modifiers asking for {Sum} per time unit";
        }
    }
    /// <summary>
    /// Identifies one modifier in one ModifierGroup. Only a ModifierGroup makes these.
    /// </summary>
    /// <param name="Id">Which of the group's modifiers this is. A group never gives the same one twice</param>
    /// <param name="Group">The number of the group that gave this key. A key only ever matches a modifier in a group with this number</param>
    public readonly record struct ModifierKey(long Id, int Group = 0);
}
