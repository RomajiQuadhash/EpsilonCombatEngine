using CoreMetrics;
using Meters;
using Rationalz;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Combatants
{
    /// <summary>
    /// A combatant's health. It is the defender's side of the damage formula: attackers hand it damage from before Defense is applied,
    /// and it divides by the Defense as it is right now, for as long as that damage keeps coming.
    /// Healing and anything else Defense has no say in goes through Add, Remove and Replace as on any RateManagedMeter.
    /// It can also have a Guard, which adds to the Defense until enough overlapping damage breaks it. Without one it is the same meter with no Guard.
    /// </summary>
    /// <remarks>Claude written, Romaji approved after a renaming</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public class HealthMeter<T> : RateManagedMeter<T, int> where T : INumber<T>
    {
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
        /// The Guard meter (Guard HP), or null if whoever this health belongs to has no Guard. Its maximum is the Guard stat.
        /// Only this health meter should change it.
        /// </summary>
        public LinearContinuousMeter<T, double>? Guard { get; }
        /// <summary>
        /// The GuardStrength stat, which divides what each source of damage chips off the Guard. Null if there is no Guard.
        /// </summary>
        public StatModificationMeter<T>? GuardStrength { get; }
        /// <summary>
        /// How much the Guard heals back when each source of damage ends, by the key AddDamage gave for that source.
        /// </summary>
        private readonly Dictionary<ModifierKey, double> _guardHealOwed = [];
        /// <summary>
        /// The heals that have been started and could still be running, so that they can all be cancelled if the Guard is put back to full.
        /// Finished ones are cleared out whenever another is added.
        /// </summary>
        private readonly List<GuardHealEvent<T>> _guardHeals = [];
        /// <summary>
        /// The state the Guard was in the last time a GuardStateCard was put on the timeline for it, which is what the next change is told apart from.
        /// </summary>
        private GuardState _shownGuardState = GuardState.Up;
        /// <summary>
        /// How many GuardStateCards this meter has made. Each card gets the count as it was made, which is what keeps cards made at the same moment in order.
        /// </summary>
        private long _guardCardsMade;
        /// <summary>
        /// Occurs when a Guard that is at 0 or below heals back above 0. On the timeline for as long as this refers to it, and forgotten when it occurs.
        /// It tells this meter through Occurring, so the timeline's own cloning and rewiring covers it.
        /// </summary>
        private ReachThresholdEvent<T, double>? _guardRestored;
        /// <summary>
        /// Looks again one time unit after _guardRestored occurred too early to act on. Its face is this meter, so it is clear in a debugger why it is there.
        /// </summary>
        private Card<T, HealthMeter<T>>? _guardRestoreCheck;

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
        /// Creates a health meter with a Guard and nothing changing it yet.
        /// </summary>
        /// <param name="owningTimeline">The timeline that owns this meter</param>
        /// <param name="initialValue">The health it starts at</param>
        /// <param name="minValue">The lowest the health can go</param>
        /// <param name="maxValue">The highest the health can go</param>
        /// <param name="defense">The Defense stat of whoever this health belongs to. Must be on the same timeline</param>
        /// <param name="groupCounter">What numbers this meter's groups of modifiers. One just for this meter is enough. Sharing one across a combat also tells the meters' keys apart</param>
        /// <remarks>BuildGuard makes a Guard meter and a GuardStrength stat that go together, which is the easy way to get these two right.</remarks>
        /// <param name="guard">The Guard meter. Its minimum must be 0, its maximum is the Guard stat, and it must be on the same timeline. Nothing else should change it</param>
        /// <param name="guardStrength">The GuardStrength stat. Its base stat must be the Guard stat too, and it must be on the same timeline</param>
        /// <exception cref="ArgumentException">If the Guard meter's minimum isn't 0, or its maximum isn't the GuardStrength's base stat</exception>
        public HealthMeter(Timeline<T> owningTimeline, int initialValue, int minValue, int maxValue, StatModificationMeter<T> defense, ModifierGroupCounter groupCounter,
            LinearContinuousMeter<T, double> guard, StatModificationMeter<T> guardStrength)
            : this(owningTimeline, initialValue, minValue, maxValue, defense, groupCounter)
        {
            if (guard.MinValue != 0)
            {
                throw new ArgumentException($"A Guard meter's minimum has to be 0, since below 0 is how this tells that the Guard is Broken. It was {guard.MinValue}", nameof(guard));
            }
            if (guard.MaxValue != guardStrength.BaseStat)
            {
                throw new ArgumentException($"The Guard meter's maximum ({guard.MaxValue}) and the GuardStrength's base stat ({guardStrength.BaseStat}) are both the Guard stat, so they have to be the same. BuildGuard makes a pair that are", nameof(guard));
            }
            Guard = guard;
            GuardStrength = guardStrength;
        }
        /// <summary>
        /// Clone constructor: for a meter whose spillover meter and groups already exist. CopyPropertiesForClone fills in everything else.
        /// </summary>
        protected HealthMeter(Timeline<T> owningTimeline, int initialValue, int minValue, int maxValue, LinearContinuousMeter<T, Rational<long>> spilloverFraction, ModifierGroup<T, int> sources, ModifierGroup<T, int> damage, StatModificationMeter<T> defense, ModifierGroupCounter groupCounter,
            LinearContinuousMeter<T, double>? guard, StatModificationMeter<T>? guardStrength)
            : base(owningTimeline, initialValue, minValue, maxValue, spilloverFraction, sources)
        {
            GroupCounter = groupCounter;
            _damage = damage;
            Defense = defense;
            Defense.InstantChangeEvt += OnDefenseChanged;
            Guard = guard;
            GuardStrength = guardStrength;
        }

        #region Defense
        /// <summary>
        /// What instant damage is divided by: the Defense stat with its buffs and debuffs, and never less than BattleTuning.MinDefense.
        /// </summary>
        public Rational<long> BaseDefense
        {
            get
            {
                Rational<long> defense = Defense.GetModifiedStat();
                return RationalConversion.Compare(defense, BattleTuning.MinDefense) < 0 ? BattleTuning.MinDefense : defense;
            }
        }
        /// <summary>
        /// What damage over time is divided by: the Defense stat plus the Guard stat (unless there is no Guard or it is Broken),
        /// then the Defense's buffs and debuffs, and never less than BattleTuning.MinDefense.
        /// </summary>
        /// <remarks>The buffs and debuffs come after the Guard is added, so they count the same whether the Guard is up or Broken.</remarks>
        public Rational<long> EffectiveDefense
        {
            get
            {
                if (Guard is null || IsGuardBroken)
                {
                    return BaseDefense;
                }
                Rational<long> defense = RationalConversion.LimitDenominator(
                    (new Rational<Int128>(Defense.BaseStat, Int128.One) + RationalConversion.Widen(GuardStat)) * RationalConversion.Widen(Defense.GetMultiplier()));
                return RationalConversion.Compare(defense, BattleTuning.MinDefense) < 0 ? BattleTuning.MinDefense : defense;
            }
        }
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
        /// <remarks>If there is a Guard, the damage also chips it as it starts, which can Stagger or Break it. Nothing else has to be done for the Guard.</remarks>
        /// <param name="damage">The damage as it is before Defense: how much per tick (positive is damage) and how often it ticks</param>
        /// <returns>The key to use with ReplaceDamage or RemoveDamage later</returns>
        /// <exception cref="OverflowException">If the total of all the damage can't be stored exactly. Nothing is changed in that case</exception>
        public ModifierKey AddDamage(ModInstance<T, int> damage)
        {
            ModifierKey key = _damage.Add(damage); //If this throws, nothing has been changed
            //Before the rate is worked out, so that a Guard this breaks is already not counted
            double healOwed = ChipGuard(damage);
            if (healOwed > 0)
            {
                _guardHealOwed[key] = healOwed;
            }
            ApplyRateChange();
            return key;
        }
        /// <summary>
        /// Removes a source of damage over time.
        /// </summary>
        /// <remarks>If there is a Guard, it starts healing back what this source chipped off it. Nothing else has to be done for the Guard.</remarks>
        /// <param name="key">The key AddDamage gave for the source</param>
        /// <returns>True if it was removed, false if there is no damage with that key (in which case nothing changes)</returns>
        public bool RemoveDamage(ModifierKey key)
        {
            if (!_damage.Remove(key))
            {
                return false;
            }
            StartGuardHeal(key);
            ApplyRateChange();
            return true;
        }
        /// <summary>
        /// Swaps a source of damage over time for a different one, keeping the same key.
        /// </summary>
        /// <remarks>
        /// If there is a Guard, the chip changes at once by the difference between what the new source chips and what the old one did:
        /// a stronger one takes more off the Guard, a weaker one gives some back, and what the Guard heals back when the source ends changes to match.
        /// A stronger one can take the Guard to 0 (Staggered) but never Breaks it, since only a source starting does that.
        /// </remarks>
        /// <param name="key">The key AddDamage gave for the source</param>
        /// <param name="damage">What to replace it with, as it is before Defense</param>
        /// <returns>True if it was replaced, false if there is no damage with that key (in which case nothing changes)</returns>
        /// <exception cref="OverflowException">If the total of all the damage can't be stored exactly. Nothing is changed in that case</exception>
        public bool ReplaceDamage(ModifierKey key, ModInstance<T, int> damage)
        {
            if (!_damage.Modifiers.TryGetValue(key, out ModInstance<T, int>? replaced))
            {
                return false;
            }
            _damage.Replace(key, damage); //If this throws, nothing has been changed
            //Before the rate is worked out, the same as when a source starts
            RechipGuard(key, replaced, damage);
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
        #region Guard
        /// <summary>
        /// Makes the two meters a health meter needs for a Guard from the one Guard stat, so that they always agree:
        /// a Guard meter that is full, with the Guard stat as its maximum, and a GuardStrength stat with the Guard stat as its base stat.
        /// Hand both to the HealthMeter constructor that takes a Guard.
        /// </summary>
        /// <param name="owningTimeline">The timeline the health meter will be on</param>
        /// <param name="guardStat">The Guard stat: how much the Guard holds, and what it adds to the Defense while it isn't Broken. More than 0</param>
        /// <param name="minStrengthRatio">The lowest Ratio the GuardStrength can be debuffed to. 0 or less</param>
        /// <param name="maxStrengthRatio">The highest Ratio the GuardStrength can be buffed to. 0 or more</param>
        /// <param name="guard">The Guard meter that was made</param>
        /// <param name="guardStrength">The GuardStrength stat that was made</param>
        /// <exception cref="ArgumentException">If guardStat isn't more than 0</exception>
        public static void BuildGuard(Timeline<T> owningTimeline, int guardStat, Rational<long> minStrengthRatio, Rational<long> maxStrengthRatio,
            out LinearContinuousMeter<T, double> guard, out StatModificationMeter<T> guardStrength)
        {
            //Made first, since it is the one that refuses a Guard stat of 0 or less
            guardStrength = new(owningTimeline, guardStat, minStrengthRatio, maxStrengthRatio);
            guard = new(owningTimeline, guardStat, 0, guardStat);
        }
        /// <summary>
        /// The Guard stat as a fraction: the Guard meter's maximum, and what is added to the Defense while the Guard isn't Broken. 0 if there is no Guard.
        /// </summary>
        public Rational<long> GuardStat => Guard is null ? Rational<long>.Zero : RationalConversion.ToRational(Guard.MaxValue);
        /// <summary>
        /// True while the Guard is Broken: damage over time is divided by the Defense alone. It ends when the Guard heals back above 0.
        /// </summary>
        public bool IsGuardBroken => Guard is not null && Guard.MinValue < 0;
        /// <summary>
        /// True while the Guard is at 0 but not Broken. The next source of damage to start will Break it.
        /// </summary>
        public bool IsGuardStaggered => Guard is not null && !IsGuardBroken && Guard.CurrentValue <= 0;
        /// <summary>
        /// How many times more damage over time is dealt while the Guard is Broken: (Defense + Guard) / Defense. Buffs and debuffs never change it.
        /// 1 if there is no Guard.
        /// </summary>
        public Rational<long> BreakReward => RationalConversion.LimitDenominator(
            (new Rational<Int128>(Defense.BaseStat, Int128.One) + RationalConversion.Widen(GuardStat)) / new Rational<Int128>(Defense.BaseStat, Int128.One));
        /// <summary>
        /// Which state the Guard is in. Up if there is no Guard.
        /// </summary>
        public GuardState CurrentGuardState => IsGuardBroken ? GuardState.Broken : IsGuardStaggered ? GuardState.Staggered : GuardState.Up;
        /// <summary>
        /// How much a source of damage chips off the Guard. It comes from how fast the damage is, not how much there is in total, and isn't reduced by Defense.
        /// </summary>
        /// <returns>The chip, or 0 if there is no Guard or the source isn't damage</returns>
        private double ChipOf(ModInstance<T, int> damage)
        {
            if (Guard is null || GuardStrength is null)
            {
                return 0;
            }
            Rational<Int128> exactChip = RationalConversion.Widen(damage.Magnitude) * RationalConversion.Widen(BattleTuning.GuardScale)
                / RationalConversion.Widen(GuardStrength.GetMultiplier());
            double chip = (double)exactChip.Numerator / (double)exactChip.Denominator;
            return chip > 0 ? chip : 0;
        }
        /// <summary>
        /// One step of the Guard for a source of damage that is starting.
        /// A Guard above 0 is chipped and stops at 0 (Staggered). A Staggered Guard is Broken. A Broken Guard is chipped further down.
        /// </summary>
        /// <param name="damage">The source of damage that is starting, as it is before Defense</param>
        /// <returns>How much the Guard heals back when this source ends. 0 if there is no Guard or the damage chips nothing</returns>
        private double ChipGuard(ModInstance<T, int> damage)
        {
            double chip = ChipOf(damage);
            if (Guard is null || chip <= 0)
            {
                return 0;
            }
            double healOwed;
            if (IsGuardBroken)
            {
                Guard.AddInstantChange(-chip);
                healOwed = chip;
            }
            else if (Guard.CurrentValue > 0)
            {
                //Stops at 0, and only what was actually taken off is healed back
                healOwed = Math.Min(Guard.CurrentValue, chip);
                Guard.AddInstantChange(-chip);
            }
            else
            {
                //Staggered, so this source Breaks it. The floor goes first so that the whole chip fits.
                Guard.UpdateMin((double)BattleTuning.GuardBrokenFloor.Numerator / BattleTuning.GuardBrokenFloor.Denominator, false);
                Guard.AddInstantChange(-chip);
                healOwed = chip;
            }
            AfterGuardChanged();
            return healOwed;
        }
        /// <summary>
        /// Changes the Guard for a source of damage that has just been replaced by a different one, by the difference between what the two chip.
        /// A bigger chip takes more off, stopping at 0 unless the Guard is Broken. A smaller one gives back at once, up to what the source still owed.
        /// What the Guard heals back when the source ends changes by what was actually taken or given.
        /// </summary>
        /// <param name="key">The key of the source that was replaced</param>
        /// <param name="replaced">What the source was</param>
        /// <param name="replacement">What the source is now</param>
        private void RechipGuard(ModifierKey key, ModInstance<T, int> replaced, ModInstance<T, int> replacement)
        {
            if (Guard is null)
            {
                return;
            }
            double difference = ChipOf(replacement) - ChipOf(replaced);
            _guardHealOwed.TryGetValue(key, out double healOwed);
            if (difference > 0)
            {
                double before = Guard.CurrentValue;
                Guard.AddInstantChange(-difference);
                healOwed += before - Guard.CurrentValue;
            }
            else if (difference < 0)
            {
                double givenBack = Math.Min(healOwed, -difference);
                Guard.AddInstantChange(givenBack);
                healOwed -= givenBack;
            }
            if (healOwed > 0)
            {
                _guardHealOwed[key] = healOwed;
            }
            else
            {
                _guardHealOwed.Remove(key);
            }
            AfterGuardChanged();
        }
        /// <summary>
        /// Called whenever this meter has changed the Guard or its floor. If the Guard's state is different from the last one shown,
        /// a GuardStateCard for each change goes on the timeline to occur right now. And while the Guard is at 0 or below, makes sure something is watching for it healing back above 0.
        /// </summary>
        private void AfterGuardChanged()
        {
            if (Guard is null)
            {
                return;
            }
            GuardState state = CurrentGuardState;
            if (state != _shownGuardState)
            {
                if (_shownGuardState == GuardState.Staggered)
                {
                    ShowGuardTransition(GuardTransition.LeftStaggered);
                }
                else if (_shownGuardState == GuardState.Broken)
                {
                    ShowGuardTransition(GuardTransition.LeftBroken);
                }
                if (state == GuardState.Staggered)
                {
                    ShowGuardTransition(GuardTransition.BecameStaggered);
                }
                else if (state == GuardState.Broken)
                {
                    ShowGuardTransition(GuardTransition.BecameBroken);
                }
                _shownGuardState = state;
            }
            if (state != GuardState.Up && _guardRestored is null && _guardRestoreCheck is null)
            {
                WatchForGuardRestored();
            }
        }
        /// <summary>
        /// Puts a card on the timeline, to occur right now, saying the Guard has changed state.
        /// </summary>
        private void ShowGuardTransition(GuardTransition transition)
        {
            OwningTimeline.AddPossibleEvent(new GuardStateCard<T>(this, transition, _guardCardsMade++, OwningTimeline));
        }
        /// <summary>
        /// Puts the event for the Guard healing back above 0 on the timeline.
        /// </summary>
        private void WatchForGuardRestored()
        {
            if (Guard is null)
            {
                return;
            }
            _guardRestored = new(Guard, (double)BattleTuning.GuardRestoreThreshold.Numerator / BattleTuning.GuardRestoreThreshold.Denominator, PriorityRank.MeterTriggerProgressive, OwningTimeline)
            {
                Direction = ThresholdDirection.Up
            };
            _guardRestored.Occurring += OnGuardRestored;
            OwningTimeline.AddPossibleEvent(_guardRestored);
        }
        /// <summary>
        /// Starts the Guard healing back what a source of damage chipped off it, now that the source has ended.
        /// Each heal runs for the same length of time on its own, whatever else the Guard is doing.
        /// </summary>
        /// <param name="key">The key AddDamage gave for the source that ended</param>
        private void StartGuardHeal(ModifierKey key)
        {
            if (Guard is null || !_guardHealOwed.Remove(key, out double healOwed))
            {
                return;
            }
            T healTime = RationalConversion.FromRational<T>(BattleTuning.GuardHealDuration);
            if (healTime <= T.Zero)
            {
                //No time to spread it over, so it all comes back at once
                Guard.AddInstantChange(healOwed);
                AfterGuardChanged();
                return;
            }
            GuardHealEvent<T> heal = new(Guard, healOwed / RationalConversion.ConvertChecked<T, double>(healTime), T.Zero, healTime,
                PriorityRank.MeterChangePhaseStart, PriorityRank.MeterChangePhaseEnd, OwningTimeline);
            _guardHeals.RemoveAll(IsHealFinished);
            _guardHeals.Add(heal);
            OwningTimeline.AddPossibleEvent(heal);
        }
        private static bool IsHealFinished(GuardHealEvent<T> heal)
        {
            return heal.IsFinished;
        }
        /// <summary>
        /// Puts the Guard back as if it were new: full, not Broken, not healing, and with nothing owed to it. Nothing from before carries on.
        /// Does nothing if there is no Guard.
        /// </summary>
        private void ResetGuard()
        {
            _guardHealOwed.Clear();
            if (Guard is null)
            {
                return;
            }
            //The heals can't always be taken off the timeline right away, so they are told to do nothing more and the rate they had added is wiped here
            foreach (GuardHealEvent<T> heal in _guardHeals)
            {
                heal.Cancel();
            }
            _guardHeals.Clear();
            Guard.SetDerivative(0);
            if (_guardRestored is not null)
            {
                //The same goes for this: it is told it can't occur and is no longer listened to
                _guardRestored.Occurring -= OnGuardRestored;
                _guardRestored.CouldOccur = false;
                _guardRestored = null;
            }
            if (_guardRestoreCheck is not null)
            {
                _guardRestoreCheck.Occurring -= OnGuardRestoreCheck;
                if (OwningTimeline.RemoveEvent(_guardRestoreCheck))
                {
                    OwningTimeline.Advance -= _guardRestoreCheck.OnAdvance;
                }
                _guardRestoreCheck = null;
            }
            if (IsGuardBroken)
            {
                Guard.UpdateMin(0, true);
            }
            Guard.AddInstantChange(Guard.MaxValue - Guard.CurrentValue);
            AfterGuardChanged();
        }
        /// <summary>
        /// Called when the event for the Guard healing back above 0 occurs.
        /// </summary>
        /// <remarks>
        /// On a whole-number timeline the event's time has its fraction dropped, so it can occur up to one time unit before the Guard is really above 0.
        /// Ending a Break then would push the Guard up to 0 for free. So if the Guard isn't above 0 yet, this looks again one time unit later.
        /// </remarks>
        private void OnGuardRestored(object sender, Okazo<T> okazo)
        {
            if (Guard is null || !ReferenceEquals(okazo, _guardRestored))
            {
                return; //Not one this meter still cares about
            }
            _guardRestored = null;
            if (Guard.CurrentValue > 0)
            {
                GuardIsAboveZero();
                return;
            }
            _guardRestoreCheck = new(T.One, OwningTimeline, PriorityRank.MeterTriggerProgressive) { Face = this };
            _guardRestoreCheck.Occurring += OnGuardRestoreCheck;
            OwningTimeline.AddPossibleEvent(_guardRestoreCheck);
        }
        /// <summary>
        /// Called one time unit after the event for the Guard healing back occurred too early.
        /// By now the Guard is above 0, unless something chipped it again or its heals ran out, in which case the event is put back to wait for it.
        /// </summary>
        private void OnGuardRestoreCheck(object sender, Okazo<T> okazo)
        {
            if (Guard is null || !ReferenceEquals(okazo, _guardRestoreCheck))
            {
                return; //Not one this meter still cares about
            }
            _guardRestoreCheck = null;
            if (Guard.CurrentValue > 0)
            {
                GuardIsAboveZero();
            }
            else
            {
                WatchForGuardRestored();
            }
        }
        /// <summary>
        /// Called once the Guard has healed back above 0. A Break ends: the Guard's floor goes back to 0 and it counts toward the Defense again.
        /// Any heal still running carries on.
        /// </summary>
        private void GuardIsAboveZero()
        {
            if (Guard is null)
            {
                return;
            }
            if (IsGuardBroken)
            {
                Guard.UpdateMin(0, true);
                if (_damage.Modifiers.Count > 0)
                {
                    ApplyRateChange();
                }
            }
            AfterGuardChanged();
        }
        #endregion
        #region Instant damage
        /// <summary>
        /// Deals damage all at once. It is divided by BaseDefense and rounded up to a whole number, so any damage at all takes at least 1.
        /// </summary>
        /// <param name="magnitude">The damage as it is before Defense. 0 or more</param>
        /// <returns>How much health was actually lost, which is less than the damage if the minimum was reached</returns>
        /// <exception cref="ArgumentOutOfRangeException">If magnitude is negative</exception>
        /// <exception cref="OverflowException">If the damage after Defense can't be stored exactly. Nothing is changed in that case</exception>
        public int ApplyInstantDamage(Rational<long> magnitude)
        {
            if (magnitude.Numerator < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magnitude), magnitude, "Instant damage can't be negative");
            }
            Rational<long> exact = RationalConversion.NarrowExact(RationalConversion.Widen(magnitude) / RationalConversion.Widen(BaseDefense));
            int before = CurrentValue;
            AddInstantChange(-RationalConversion.FromRational<int>(exact, FractionRounding.AtLeast));
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
        /// If there is a Guard, it comes back as if it were new: full, not Broken, not healing, with nothing kept from before.
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
            ResetGuard();
            _damage.Clear();
            IsDown = false;
            StartFresh(newHealth);
            if (IsIntangible)
            {
                Pause();
            }
        }
        #endregion
        #region Events for going down and coming back
        /// <summary>
        /// Makes the event that knocks this health out when it falls to a value: 0 for a KO, or the minimum for a death while bleeding.
        /// When it occurs, anything listening hears about it first and then KnockOut is called.
        /// </summary>
        /// <remarks>
        /// Like other events, it isn't put on the timeline. Use AddEvent or AddPossibleEvent once it is made.
        /// It occurs once. After a revive, make another.
        /// </remarks>
        /// <param name="threshold">The health at which whoever this belongs to goes down</param>
        /// <param name="priority">Suggested to be a Death or KO</param>
        /// <returns>The event, listening for health falling to the threshold</returns>
        public DiscreteReachThresholdEvent<T, int> CreateKnockOutEvent(int threshold, PriorityRank priority)
        {
            DiscreteReachThresholdEvent<T, int> knockOut = new(this, threshold, priority, OwningTimeline)
            {
                Direction = ThresholdDirection.Down
            };
            knockOut.Occurring += OnKnockOutEvent;
            return knockOut;
        }
        /// <summary>
        /// Makes the card that revives this health at a given value after a length of time. The health to come back at is the card's face.
        /// </summary>
        /// <remarks>Like other cards, it isn't put on the timeline. Use AddEvent or AddPossibleEvent once it is made.</remarks>
        /// <param name="timeToEvent">How long from now until the revive</param>
        /// <param name="newHealth">The health to come back at. Must be within the minimum and maximum</param>
        /// <param name="priority">Likely should be a revive priority</param>
        /// <returns>The card, which revives this health when it occurs</returns>
        /// <exception cref="ArgumentOutOfRangeException">If newHealth is below the minimum or above the maximum</exception>
        public Card<T, int> CreateReviveCard(T timeToEvent, int newHealth, PriorityRank priority)
        {
            if (IsOutsideRange(newHealth))
            {
                throw new ArgumentOutOfRangeException(nameof(newHealth), newHealth, $"The health to come back at must be in [{MinValue},{MaxValue}]");
            }
            Card<T, int> revive = new(timeToEvent, OwningTimeline, priority) { Face = newHealth };
            revive.Occurring += OnReviveCard;
            return revive;
        }
        /// <summary>
        /// Called when an event from CreateKnockOutEvent occurs.
        /// </summary>
        private void OnKnockOutEvent(object sender, Okazo<T> okazo)
        {
            KnockOut();
        }
        /// <summary>
        /// Called when a card from CreateReviveCard occurs. Revives at the health on the card's face.
        /// </summary>
        private void OnReviveCard(object sender, Okazo<T> okazo)
        {
            if (okazo is Card<T, int> revive)
            {
                Revive(revive.Face);
            }
        }
        #endregion
        #region Cloning
        protected override RateManagedMeter<T, int> CreateEmptyForClone(Timeline<T> owningTimeline, LinearContinuousMeter<T, Rational<long>> spilloverFraction, ModifierGroup<T, int> sources, CloneContext context)
        {
            //None of these know anything about this meter, so they are safe to clone before this is registered.
            //The damage group is cloned, not made again, so it keeps its number and its keys.
            return new HealthMeter<T>(owningTimeline, CurrentValue - InstantChange, MinValue, MaxValue, spilloverFraction, sources, context.GetOrClone(_damage), context.GetOrClone(Defense), context.GetOrClone(GroupCounter),
                Guard is null ? null : context.GetOrClone(Guard), GuardStrength is null ? null : context.GetOrClone(GuardStrength));
        }
        protected override void CopyPropertiesForClone(RateManagedMeter<T, int> clone, CloneContext context)
        {
            if (clone is not HealthMeter<T> healthClone)
            {
                throw new UnrecoverableCloneException("Somehow, we cloned a HealthMeter and got a different type back. This should never happen.");
            }
            healthClone.IsIntangible = IsIntangible;
            healthClone.IsDown = IsDown;
            foreach (KeyValuePair<ModifierKey, double> healOwed in _guardHealOwed)
            {
                healthClone._guardHealOwed.Add(healOwed.Key, healOwed.Value);
            }
            foreach (GuardHealEvent<T> heal in _guardHeals)
            {
                if (!heal.IsFinished)
                {
                    healthClone._guardHeals.Add(context.GetOrClone(heal));
                }
            }
            healthClone._shownGuardState = _shownGuardState;
            healthClone._guardCardsMade = _guardCardsMade;
            //The clone doesn't listen to this here. Rewiring the original's events does that, as for the leap year events.
            healthClone._guardRestored = _guardRestored is null ? null : context.GetOrClone(_guardRestored);
            //This card's face is this meter, so it can only be cloned now that the clone is registered
            healthClone._guardRestoreCheck = _guardRestoreCheck is null ? null : context.GetOrClone(_guardRestoreCheck);
            base.CopyPropertiesForClone(clone, context);
        }
        #endregion
        public override string ToString()
        {
            return base.ToString() + $", {_damage} before a Defense of {EffectiveDefense}"
                + (Guard is null ? "" : IsGuardBroken ? ", Guard Broken" : IsGuardStaggered ? ", Guard Staggered" : $", Guard of {Guard.CurrentValue}")
                + (IsDown ? ", down" : "")
                + (IsIntangible ? ", intangible" : "");
        }
    }
}
