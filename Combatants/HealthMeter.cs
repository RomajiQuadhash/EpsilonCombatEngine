using CoreMetrics;
using Meters;
using Rationalz;
using System.Numerics;
using Timeline;

namespace Combatants
{
    /// <summary>
    /// A combatant's health. It is the defender's side of the damage formula: attackers hand it damage from before Defense is applied,
    /// and it divides by the Defense as it is right now, for as long as that damage keeps coming.
    /// Healing and anything else Defense has no say in goes through Add, Remove and Replace as on any RateManagedMeter.
    /// </summary>
    /// <remarks>Claude written, Romaji approved after a renaming</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public class HealthMeter<T> : RateManagedMeter<T, int> where T : INumber<T>
    {
        /// <summary>
        /// The lowest that Defense can count as, so that damage can't explode as a debuffed Defense gets close to 0.
        /// </summary>
        public static readonly Rational<long> MinDefense = BattleTuning.MinDefense; //The value is set in CoreMetrics' BattleTuning
        /// <summary>
        /// The Defense stat that damage is divided by, with whatever buffs and debuffs are on it.
        /// </summary>
        public StatModificationMeter<T> Defense { get; }
        /// <summary>
        /// The sources of damage over time, as they were before Defense. Kept apart from the meter's own modifiers since only these are divided by Defense.
        /// </summary>
        private readonly ModifierGroup<T, int> _damage;
        /// <summary>
        /// What numbered this meter's two groups of modifiers, so that a key from Add can never match damage and a key from AddDamage can never match anything from Add.
        /// </summary>
        public ModifierGroupCounter GroupCounter { get; }
        /// <summary>
        /// The sources of damage over time that are currently affecting this meter, as they were before Defense. Positive is damage.
        /// Keys are given by AddDamage and only work with RemoveDamage and ReplaceDamage. A key from Add never matches any of these.
        /// </summary>
        public IReadOnlyDictionary<ModifierKey, ModInstance<T, int>> DamageModifiers => _damage.Modifiers;

        /// <summary>
        /// Creates a health meter with nothing changing it yet.
        /// </summary>
        /// <param name="owningTimeline">The timeline that owns this meter</param>
        /// <param name="initialValue">The health it starts at</param>
        /// <param name="minValue">The lowest the health can go</param>
        /// <param name="maxValue">The highest the health can go</param>
        /// <param name="defense">The Defense stat of whoever this health belongs to. Must be on the same timeline</param>
        /// <param name="groupCounter">What numbers this meter's groups of modifiers. One just for this meter is enough. Sharing one across a combat also tells the meters' keys apart</param>
        public HealthMeter(Timeline<T> owningTimeline, int initialValue, int minValue, int maxValue, StatModificationMeter<T> defense, ModifierGroupCounter groupCounter)
            : base(owningTimeline, initialValue, minValue, maxValue, groupCounter)
        {
            GroupCounter = groupCounter;
            _damage = new(groupCounter);
            Defense = defense;
            //A meter's own events aren't carried over to a clone by rewiring, so this is done by both constructors
            Defense.InstantChangeEvt += OnDefenseChanged;
        }
        /// <summary>
        /// Clone constructor: for a meter whose spillover meter already exists. CopyPropertiesForClone fills in everything else.
        /// </summary>
        protected HealthMeter(Timeline<T> owningTimeline, int initialValue, int minValue, int maxValue, LinearContinuousMeter<T, Rational<long>> spilloverFraction, ModifierGroup<T, int> sources, ModifierGroup<T, int> damage, StatModificationMeter<T> defense, ModifierGroupCounter groupCounter)
            : base(owningTimeline, initialValue, minValue, maxValue, spilloverFraction, sources)
        {
            GroupCounter = groupCounter;
            _damage = damage;
            Defense = defense;
            Defense.InstantChangeEvt += OnDefenseChanged;
        }

        #region Defense
        /// <summary>
        /// What instant damage is divided by: the Defense stat with its buffs and debuffs, and never less than MinDefense.
        /// </summary>
        public Rational<long> BaseDefense
        {
            get
            {
                Rational<long> defense = Defense.GetModifiedStat();
                return RationalConversion.Compare(defense, MinDefense) < 0 ? MinDefense : defense;
            }
        }
        /// <summary>
        /// What damage over time is divided by. The same as BaseDefense for now, since this meter has no Guard yet.
        /// </summary>
        public Rational<long> EffectiveDefense => BaseDefense;
        /// <summary>
        /// Called when the Defense stat's buffs and debuffs change. No modifier is touched: the damage is kept as it was before Defense,
        /// so all there is to do is work the rate out again.
        /// </summary>
        private void OnDefenseChanged(object sender, Rational<long> before, Rational<long> after)
        {
            if (_damage.Modifiers.Count == 0)
            {
                return; //Nothing here is divided by Defense right now
            }
            ApplyRateChange();
        }
        #endregion
        #region Damage over time
        /// <summary>
        /// Adds a source of damage over time, which is divided by the Defense for as long as it stays.
        /// </summary>
        /// <param name="damage">The damage as it is before Defense: how much per tick (positive is damage) and how often it ticks</param>
        /// <returns>The key to use with ReplaceDamage or RemoveDamage later</returns>
        /// <exception cref="OverflowException">If the total of all the damage can't be stored exactly. Nothing is changed in that case</exception>
        public ModifierKey AddDamage(ModInstance<T, int> damage)
        {
            ModifierKey key = _damage.Add(damage); //If this throws, nothing has been changed
            ApplyRateChange();
            return key;
        }
        /// <summary>
        /// Removes a source of damage over time.
        /// </summary>
        /// <param name="key">The key AddDamage gave for the source</param>
        /// <returns>True if it was removed, false if there is no damage with that key (in which case nothing changes)</returns>
        public bool RemoveDamage(ModifierKey key)
        {
            if (!_damage.Remove(key))
            {
                return false;
            }
            ApplyRateChange();
            return true;
        }
        /// <summary>
        /// Swaps a source of damage over time for a different one, keeping the same key.
        /// </summary>
        /// <param name="key">The key AddDamage gave for the source</param>
        /// <param name="damage">What to replace it with, as it is before Defense</param>
        /// <returns>True if it was replaced, false if there is no damage with that key (in which case nothing changes)</returns>
        /// <exception cref="OverflowException">If the total of all the damage can't be stored exactly. Nothing is changed in that case</exception>
        public bool ReplaceDamage(ModifierKey key, ModInstance<T, int> damage)
        {
            if (!_damage.Replace(key, damage)) //If this throws, nothing has been changed
            {
                return false;
            }
            ApplyRateChange();
            return true;
        }
        /// <summary>
        /// The change per unit of time this meter is asked for: everything from Add, minus the damage over time divided by the Defense as it is right now.
        /// </summary>
        /// <remarks>
        /// With no damage this is exact. With damage it is rounded to a small denominator, the same as the meter does with its spillover's rate.
        /// </remarks>
        public override Rational<long> TrueRate
        {
            get
            {
                if (_damage.Sum.Numerator == 0)
                {
                    return Sources.Sum;
                }
                return RationalConversion.LimitDenominator(
                    RationalConversion.Widen(Sources.Sum) - RationalConversion.Widen(_damage.Sum) / RationalConversion.Widen(EffectiveDefense));
            }
        }
        /// <summary>
        /// The tick rate of whichever is stronger: the strongest damage over time once it is divided by the Defense, or the strongest of everything else.
        /// If they are equally strong, the one that ticks more often.
        /// </summary>
        protected override T PaceTickRate
        {
            get
            {
                if (_damage.Modifiers.Count == 0)
                {
                    return Sources.FastestTickRateAtMax;
                }
                if (Sources.Modifiers.Count == 0)
                {
                    return _damage.FastestTickRateAtMax;
                }
                Rational<long> strongestDamage = RationalConversion.LimitDenominator(
                    RationalConversion.Widen(_damage.MaxMagnitude) / RationalConversion.Widen(EffectiveDefense));
                int comparison = RationalConversion.Compare(strongestDamage, Sources.MaxMagnitude);
                if (comparison > 0)
                {
                    return _damage.FastestTickRateAtMax;
                }
                if (comparison < 0)
                {
                    return Sources.FastestTickRateAtMax;
                }
                return T.Min(_damage.FastestTickRateAtMax, Sources.FastestTickRateAtMax);
            }
        }
        #endregion
        #region Instant damage
        /// <summary>
        /// Deals damage all at once. It is divided by BaseDefense and rounded up to a whole number, so any damage at all takes at least 1.
        /// </summary>
        /// <param name="magnitude">The damage as it is before Defense. 0 or more</param>
        /// <returns>How much health was actually lost, which is less than the damage if the minimum was reached</returns>
        /// <exception cref="ArgumentOutOfRangeException">If magnitude is negative</exception>
        public int ApplyInstantDamage(Rational<long> magnitude)
        {
            if (magnitude.Numerator < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magnitude), magnitude, "Instant damage can't be negative");
            }
            Rational<Int128> exact = RationalConversion.Widen(magnitude) / RationalConversion.Widen(BaseDefense);
            Int128 roundedUp = (exact.Numerator + exact.Denominator - Int128.One) / exact.Denominator;
            int before = CurrentValue;
            AddInstantChange(-int.CreateChecked(roundedUp));
            return before - CurrentValue;
        }
        #endregion
        #region Pausing
        //Two things pause this meter, and it stays paused for as long as either of them holds.
        /// <summary>
        /// True between StartIntangible and EndIntangible: whoever this health belongs to can't be touched for a while (Intangible).
        /// </summary>
        public bool IsIntangible { get; private set; }
        /// <summary>
        /// True between KnockOut and Revive: whoever this health belongs to is KO'd or dead, so nothing over time changes it.
        /// </summary>
        public bool IsDown { get; private set; }
        /// <summary>
        /// Stops time from changing this meter until EndIntangible is called, for a combatant that can't be touched for a while (Intangible).
        /// Everything changing it is kept, and carries on from the same point when it is resumed.
        /// </summary>
        /// <remarks>Instant changes still happen while paused, so whoever deals instant damage has to check for themselves.</remarks>
        public void StartIntangible()
        {
            IsIntangible = true;
            Pause();
        }
        /// <summary>
        /// Lets time change this meter again, from exactly where it was when StartIntangible was called.
        /// If it is also down, it stays paused until Revive.
        /// </summary>
        public void EndIntangible()
        {
            IsIntangible = false;
            if (!IsDown)
            {
                Resume();
            }
        }
        /// <summary>
        /// Stops time from changing this meter, for a combatant that has been KO'd or has died. Nothing that was changing it over time lands after this,
        /// which is why healing over time can't bring a combatant back.
        /// </summary>
        /// <remarks>
        /// Instant changes still happen, so whoever heals instantly has to check for themselves.
        /// A dead combatant is knocked out and never revived.
        /// </remarks>
        public void KnockOut()
        {
            IsDown = true;
            Pause();
        }
        /// <summary>
        /// Brings a combatant back at the given health with nothing changing it: all damage over time and everything from Add is removed (their keys stop working),
        /// and whatever tick was in progress when they went down is dropped without landing.
        /// </summary>
        /// <remarks>Also works on a meter that isn't down, to wipe everything that is changing it. If it is Intangible, it stays paused.</remarks>
        /// <param name="newHealth">The health to come back at. Must be within the minimum and maximum</param>
        /// <exception cref="ArgumentOutOfRangeException">If newHealth is below the minimum or above the maximum. Nothing is changed in that case</exception>
        public void Revive(int newHealth)
        {
            if (IsOutsideRange(newHealth))
            {
                throw new ArgumentOutOfRangeException(nameof(newHealth), newHealth, $"The health to come back at must be in [{MinValue},{MaxValue}]");
            }
            _damage.Clear();
            IsDown = false;
            StartFresh(newHealth);
            if (IsIntangible)
            {
                Pause();
            }
        }
        #endregion
        #region Cloning
        protected override RateManagedMeter<T, int> CreateEmptyForClone(Timeline<T> owningTimeline, LinearContinuousMeter<T, Rational<long>> spilloverFraction, ModifierGroup<T, int> sources, CloneContext context)
        {
            //None of these know anything about this meter, so they are safe to clone before this is registered.
            //The damage group is cloned, not made again, so it keeps its number and its keys.
            return new HealthMeter<T>(owningTimeline, CurrentValue - InstantChange, MinValue, MaxValue, spilloverFraction, sources, context.GetOrClone(_damage), context.GetOrClone(Defense), context.GetOrClone(GroupCounter));
        }
        protected override void CopyPropertiesForClone(RateManagedMeter<T, int> clone, CloneContext context)
        {
            if (clone is not HealthMeter<T> healthClone)
            {
                throw new UnrecoverableCloneException("Somehow, we cloned a HealthMeter and got a different type back. This should never happen.");
            }
            healthClone.IsIntangible = IsIntangible;
            healthClone.IsDown = IsDown;
            base.CopyPropertiesForClone(clone, context);
        }
        #endregion
        public override string ToString()
        {
            return base.ToString() + $", {_damage} before a Defense of {EffectiveDefense}"
                + (IsDown ? ", down" : "")
                + (IsIntangible ? ", intangible" : "");
        }
    }
}
