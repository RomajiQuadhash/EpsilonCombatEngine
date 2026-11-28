using CoreMetrics;
using Meters;
using Rationalz;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Combatants
{
    /// <summary>
    /// A combatant with everything a BaseCombatant only has stand-ins for, except turns:
    /// status effects, bleeding, the damage it is dealing to others, and whichever further stats and meters it was given.
    /// It still makes no decisions, and it is the same whether a player or an AI is in control of it.
    /// </summary>
    /// <remarks>Claude written, Romaji approved</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public class Combatant<T> : BaseCombatant<T> where T : INumber<T>
    {
        /// <summary>
        /// The priority this combatant's death event is given: PlayerDeath or EnemyDeath.
        /// </summary>
        public PriorityRank DeathPriority { get; }
        private readonly Dictionary<CombatStats, StatModificationMeter<T>> _otherStats;
        private readonly Dictionary<MeterTypes, IMeter<T>> _otherMeters;

        /// <summary>
        /// Creates a combatant that is up, with no status effects, not bleeding and dealing nothing.
        /// </summary>
        /// <param name="healthMeter">The meter for this combatant's health. The combatant is on this meter's timeline</param>
        /// <param name="speedAggregator">What every speed in the combat is compared through. Share one across the whole combat</param>
        /// <param name="speedStat">The speed with no buffs or debuffs. More than 0</param>
        /// <param name="minSpeedRatio">The lowest Ratio the speed can be debuffed to. 0 or less</param>
        /// <param name="maxSpeedRatio">The highest Ratio the speed can be buffed to. 0 or more</param>
        /// <param name="knockOutPriority">The priority for this combatant's knock out events: PlayerKO or EnemyKO</param>
        /// <param name="deathPriority">The priority for this combatant's death event: PlayerDeath or EnemyDeath</param>
        /// <param name="otherStats">
        /// The stats this combatant has besides Speed, Defense and GuardStrength (which come from its speed and health): any of Attack, Precision and Evasion.
        /// Leave out the ones it doesn't have, or all of them
        /// </param>
        /// <param name="otherMeters">The meters this combatant has besides health and Guard: any of MP, Vibe, Charge and Progression. Each has to be cloneable</param>
        /// <exception cref="ArgumentException">If otherStats has Speed, Defense or GuardStrength, or otherMeters has Guard, or one of the meters can't be cloned</exception>
        [SetsRequiredMembers]
        public Combatant(HealthMeter<T> healthMeter, MaxStatAggregator<T, SpeedMeter<T>> speedAggregator, int speedStat, Rational<long> minSpeedRatio, Rational<long> maxSpeedRatio,
            PriorityRank knockOutPriority, PriorityRank deathPriority,
            IReadOnlyDictionary<CombatStats, StatModificationMeter<T>>? otherStats = null, IReadOnlyDictionary<MeterTypes, IMeter<T>>? otherMeters = null)
            : base(healthMeter, speedAggregator, speedStat, minSpeedRatio, maxSpeedRatio, knockOutPriority)
        {
            DeathPriority = deathPriority;
            _otherStats = [];
            _otherMeters = [];
            if (otherStats is not null)
            {
                foreach (KeyValuePair<CombatStats, StatModificationMeter<T>> stat in otherStats)
                {
                    if (stat.Key is CombatStats.Speed or CombatStats.Defense or CombatStats.GuardStrength)
                    {
                        throw new ArgumentException($"{stat.Key} comes from the combatant's speed or its health meter, so it can't be given again", nameof(otherStats));
                    }
                    _otherStats.Add(stat.Key, stat.Value);
                }
            }
            if (otherMeters is not null)
            {
                foreach (KeyValuePair<MeterTypes, IMeter<T>> meter in otherMeters)
                {
                    if (meter.Key == MeterTypes.Guard)
                    {
                        throw new ArgumentException("The Guard meter comes from the combatant's health meter, so it can't be given again", nameof(otherMeters));
                    }
                    if (meter.Value is not ICombatCloneable)
                    {
                        throw new ArgumentException($"The {meter.Key} meter can't be cloned, so a combat with this combatant in it couldn't be simulated", nameof(otherMeters));
                    }
                    _otherMeters.Add(meter.Key, meter.Value);
                }
            }
        }
        /// <summary>
        /// Constructor that only sets what it is given, for clones. It puts nothing on the timeline.
        /// </summary>
        [SetsRequiredMembers]
        protected Combatant(HealthMeter<T> healthMeter, SpeedMeter<T> speed, PriorityRank knockOutPriority, PriorityRank deathPriority,
            Dictionary<CombatStats, StatModificationMeter<T>> otherStats, Dictionary<MeterTypes, IMeter<T>> otherMeters)
            : base(healthMeter, speed, knockOutPriority)
        {
            DeathPriority = deathPriority;
            _otherStats = otherStats;
            _otherMeters = otherMeters;
        }

        #region Combat stats and meters
        /// <summary>
        /// The combat stats this combatant has, whichever of its parts holds them: Speed, Defense, GuardStrength if its health has a Guard,
        /// and whichever of Attack, Precision and Evasion it was given.
        /// Made new each time it is read, so adding to or removing from it changes nothing.
        /// </summary>
        /// <remarks>Setting it is ignored. For anything about timing, use the Speed property and not the Speed entry here, which is only the raw speed.</remarks>
        public override IDictionary<CombatStats, StatModificationMeter<T>> ChangeableStats
        {
            get
            {
                IDictionary<CombatStats, StatModificationMeter<T>> stats = base.ChangeableStats;
                foreach (KeyValuePair<CombatStats, StatModificationMeter<T>> stat in _otherStats)
                {
                    stats[stat.Key] = stat.Value;
                }
                return stats;
            }
            set { }
        }
        /// <summary>
        /// The meters this combatant has besides health: the Guard meter if its health has a Guard, and whichever of MP, Vibe, Charge and Progression it was given.
        /// Made new each time it is read, so adding to or removing from it changes nothing.
        /// </summary>
        /// <remarks>Setting it is ignored.</remarks>
        public override IDictionary<MeterTypes, IMeter<T>> AdditionalMeters
        {
            get
            {
                IDictionary<MeterTypes, IMeter<T>> meters = base.AdditionalMeters;
                foreach (KeyValuePair<MeterTypes, IMeter<T>> meter in _otherMeters)
                {
                    meters[meter.Key] = meter.Value;
                }
                return meters;
            }
            set { }
        }
        #endregion
        #region Hearing events
        /// <summary>
        /// Called when an event that involves this combatant occurs. The status effect events and bleeds made for this combatant call it,
        /// once when they apply and once when they are removed. A dead combatant takes no notice of anything.
        /// </summary>
        public override void OnOccur(object _, Okazo<T> okazo)
        {
            if (IsDead)
            {
                return;
            }
            if (okazo is IStatusEvent<T> statusEvent && ReferenceEquals(statusEvent.Target, this))
            {
                OnStatusEvent(statusEvent);
            }
            else if (okazo is BleedEvent<T> bleed && ReferenceEquals(bleed.Target, this))
            {
                OnBleedEvent(bleed);
            }
        }
        #endregion
        #region Status effects
        private readonly Dictionary<StatusEffects, AbstractCombatant<T>?> _statusOwners = [];
        /// <summary>
        /// The event that applied each status this combatant has, so that it can be cancelled if the status is cleared some other way.
        /// </summary>
        private readonly Dictionary<StatusEffects, IStatusEvent<T>> _statusEvents = [];
        /// <summary>
        /// The status effects on this combatant, and for each one, the combatant that owns it (null if nobody does).
        /// Made new each time it is read, so adding to or removing from it changes nothing.
        /// </summary>
        /// <remarks>Setting it is ignored. Statuses come and go through status effect events.</remarks>
        public override IDictionary<StatusEffects, AbstractCombatant<T>?> StatusEffects
        {
            get => new Dictionary<StatusEffects, AbstractCombatant<T>?>(_statusOwners);
            set { }
        }
        /// <summary>
        /// True if this combatant has the status effect right now.
        /// </summary>
        public bool HasStatus(StatusEffects status)
        {
            return _statusOwners.ContainsKey(status);
        }
        /// <summary>
        /// The event that applied a status this combatant has, which is also the one that will remove it. Null if it doesn't have that status.
        /// Whoever owns the status can use this to change when it ends.
        /// </summary>
        public IStatusEvent<T>? StatusEventFor(StatusEffects status)
        {
            return _statusEvents.GetValueOrDefault(status);
        }
        /// <summary>
        /// True if the combatant is asleep or paused, which stop it from acting without it being knocked out or dead.
        /// </summary>
        public override bool IsOtherwiseIncapacitated => HasStatus(Combatants.StatusEffects.Sleep) || HasStatus(Combatants.StatusEffects.Paused);
        /// <summary>
        /// A status effect event for this combatant is applying or being removed.
        /// A status this combatant already has is not applied again: the second event is cancelled, so it never removes anything either.
        /// A status is only removed by an event with the same owner as the status has.
        /// </summary>
        private void OnStatusEvent(IStatusEvent<T> statusEvent)
        {
            if (statusEvent.IsCancelled)
            {
                return;
            }
            StatusEffects status = statusEvent.Status;
            if (statusEvent.LifeCyclePhase == ApplyOrRemove.Apply)
            {
                if (_statusOwners.ContainsKey(status))
                {
                    //Immune to what it already has
                    statusEvent.Cancel();
                    return;
                }
                _statusOwners[status] = statusEvent.Owner;
                _statusEvents[status] = statusEvent;
                OnStatusStarted(status);
                return;
            }
            if (_statusOwners.TryGetValue(status, out AbstractCombatant<T>? owner) && ReferenceEquals(owner, statusEvent.Owner))
            {
                _statusOwners.Remove(status);
                _statusEvents.Remove(status);
                OnStatusEnded(status);
            }
        }
        /// <summary>
        /// Called when this combatant has just got a status effect. Does what that status means for the parts of the combatant that exist so far:
        /// Intangible stops time from changing its health. Paused stops the damage it is dealing, and all of what was left is dealt once the pause ends.
        /// Nullified keeps the damage it is dealing from landing, while each attack carries on running, so what would have landed in that time is lost.
        /// The others are only recorded.
        /// </summary>
        protected virtual void OnStatusStarted(StatusEffects status)
        {
            switch (status)
            {
                case Combatants.StatusEffects.Intangible:
                    HealthMeter.StartIntangible();
                    break;
                case Combatants.StatusEffects.Paused:
                    PauseDealing();
                    break;
                case Combatants.StatusEffects.Nullified:
                    SuppressDealing();
                    break;
            }
        }
        /// <summary>
        /// Called when a status effect has just been removed from this combatant. Undoes what OnStatusStarted did for it, where there is something to undo.
        /// </summary>
        protected virtual void OnStatusEnded(StatusEffects status)
        {
            switch (status)
            {
                case Combatants.StatusEffects.Intangible:
                    HealthMeter.EndIntangible();
                    break;
                case Combatants.StatusEffects.Paused:
                    ResumeDealing();
                    break;
                case Combatants.StatusEffects.Nullified:
                    UnsuppressDealing();
                    break;
            }
        }
        /// <summary>
        /// Takes every status effect off this combatant at once, and cancels the events that applied them so that none of them does anything later.
        /// </summary>
        private void ClearStatuses()
        {
            foreach (IStatusEvent<T> statusEvent in _statusEvents.Values)
            {
                statusEvent.Cancel();
            }
            foreach (StatusEffects status in _statusOwners.Keys.ToList())
            {
                _statusOwners.Remove(status);
                _statusEvents.Remove(status);
                OnStatusEnded(status);
            }
        }
        #endregion
        #region Damage this combatant is dealing
        /// <summary>
        /// The damage phases and bleeds this combatant made that might still be running. Finished ones are cleared out whenever another is added.
        /// </summary>
        private readonly List<DamagePhaseEvent<T>> _dealing = [];
        /// <summary>
        /// The damage phases and bleeds that were stopped when this combatant was paused, to be carried on when the pause ends.
        /// </summary>
        private readonly List<DamagePhaseEvent<T>> _pausedDealing = [];
        /// <summary>
        /// The damage phases and bleeds this combatant is dealing right now, or is waiting to.
        /// </summary>
        public IReadOnlyList<DamagePhaseEvent<T>> Dealing
        {
            get
            {
                _dealing.RemoveAll(IsOver);
                return _dealing.ToList();
            }
        }
        private static bool IsOver(DamagePhaseEvent<T> damagePhase)
        {
            return damagePhase.IsFinished || damagePhase.HasEndedEarly;
        }
        /// <summary>
        /// Makes a damage phase that this combatant is dealing to some health, and remembers it, so that it stops if this combatant can no longer deal it
        /// (it is knocked out, killed or paused), and deals nothing while this combatant is nullified.
        /// </summary>
        /// <remarks>
        /// Like other events, it isn't put on the timeline. Use AddEvent or AddPossibleEvent once it is made.
        /// This doesn't check whether the combatant is able to deal damage right now. That is for whatever decides its actions.
        /// </remarks>
        /// <param name="targetHealth">The health the damage is dealt to</param>
        /// <param name="damage">The damage as it is before Defense: how much per tick (more than 0) and how often it ticks</param>
        /// <param name="waitingTime">How long from now until the damage starts</param>
        /// <param name="applyTime">How long the damage keeps coming. More than 0</param>
        /// <param name="applyPriority">The priority while it is waiting to start</param>
        /// <param name="removePriority">The priority while it is waiting to stop</param>
        public DamagePhaseEvent<T> CreateDamagePhase(HealthMeter<T> targetHealth, ModInstance<T, int> damage, T waitingTime, T applyTime, PriorityRank? applyPriority, PriorityRank? removePriority)
        {
            DamagePhaseEvent<T> damagePhase = new(targetHealth, damage, waitingTime, applyTime, applyPriority, removePriority, OwningTimeline);
            RememberDealing(damagePhase);
            return damagePhase;
        }
        /// <summary>
        /// Makes a bleed that this combatant causes on another (or on itself), owned by this combatant, and remembers it the same way as a damage phase.
        /// </summary>
        /// <remarks>Like other events, it isn't put on the timeline. Use AddEvent or AddPossibleEvent once it is made.</remarks>
        /// <param name="target">The combatant that will bleed</param>
        /// <param name="damage">The damage as it is before Defense: how much per tick (more than 0) and how often it ticks</param>
        /// <param name="waitingTime">How long from now until the bleed starts</param>
        /// <param name="applyTime">How long the bleed lasts. More than 0</param>
        /// <param name="applyPriority">The priority while it is waiting to start: PlayerBleedStart or EnemyBleedStart</param>
        /// <param name="removePriority">The priority while it is waiting to stop: PlayerBleedEnd or EnemyBleedEnd</param>
        public BleedEvent<T> CreateBleed(AbstractCombatant<T> target, ModInstance<T, int> damage, T waitingTime, T applyTime, PriorityRank? applyPriority, PriorityRank? removePriority)
        {
            BleedEvent<T> bleed = new(target, this, damage, waitingTime, applyTime, applyPriority, removePriority);
            RememberDealing(bleed);
            return bleed;
        }
        private void RememberDealing(DamagePhaseEvent<T> damagePhase)
        {
            _dealing.RemoveAll(IsOver);
            _dealing.Add(damagePhase);
            if (HasStatus(Combatants.StatusEffects.Nullified))
            {
                //Made while nullified, so it deals nothing until that ends
                damagePhase.Suppress();
            }
        }
        /// <summary>
        /// Keeps the damage of everything this combatant is dealing from landing, without stopping any of it. UnsuppressDealing lets it land again.
        /// </summary>
        private void SuppressDealing()
        {
            foreach (DamagePhaseEvent<T> damagePhase in _dealing)
            {
                damagePhase.Suppress();
            }
        }
        /// <summary>
        /// Lets the damage of everything this combatant is dealing land again. Each damage phase or bleed deals whatever is left of its own time, which may be nothing.
        /// </summary>
        private void UnsuppressDealing()
        {
            foreach (DamagePhaseEvent<T> damagePhase in _dealing)
            {
                damagePhase.Unsuppress();
            }
        }
        /// <summary>
        /// Ends every damage phase and bleed this combatant is dealing, for good. Nothing carries them on.
        /// </summary>
        private void EndDealing()
        {
            foreach (DamagePhaseEvent<T> damagePhase in _dealing)
            {
                damagePhase.EndEarly();
            }
            _dealing.Clear();
            _pausedDealing.Clear();
        }
        /// <summary>
        /// Ends every damage phase and bleed this combatant is dealing, and keeps them so that ResumeDealing can carry each one on for the time it had left.
        /// </summary>
        private void PauseDealing()
        {
            foreach (DamagePhaseEvent<T> damagePhase in _dealing)
            {
                if (IsOver(damagePhase))
                {
                    continue;
                }
                damagePhase.EndEarly();
                if (damagePhase.TimeLeftWhenEnded > T.Zero)
                {
                    _pausedDealing.Add(damagePhase);
                }
            }
            _dealing.Clear();
        }
        /// <summary>
        /// Carries on what PauseDealing stopped: each one gets a new damage phase (or bleed) for the time it had left, after whatever wait it still had,
        /// and that goes on the timeline.
        /// </summary>
        private void ResumeDealing()
        {
            foreach (DamagePhaseEvent<T> paused in _pausedDealing)
            {
                DamagePhaseEvent<T> continuation = paused.CreateContinuation(paused.WaitLeftWhenEnded);
                RememberDealing(continuation);
                OwningTimeline.AddPossibleEvent(continuation);
            }
            _pausedDealing.Clear();
        }
        #endregion
        #region Bleeding
        /// <summary>
        /// The bleeds on this combatant right now. Always empty for one that can't be killed, since bleeding doesn't exist for it.
        /// </summary>
        private readonly List<BleedEvent<T>> _bleeds = [];
        /// <summary>
        /// Occurs when a bleeding combatant's health falls to the lowest it can go, which kills it. Null unless it is bleeding.
        /// </summary>
        private DiscreteReachThresholdEvent<T, int>? _deathWatch;
        /// <summary>
        /// True while there is at least one bleed on this combatant. It can then go below 0 health without being knocked out, and dies if it gets to BleedMinimum.
        /// </summary>
        public override bool IsBleeding => _bleeds.Count > 0;
        /// <summary>
        /// The health at which this combatant dies while bleeding: below 0 by BattleTuning.BleedLimit of its maximum health, rounded up to a whole number.
        /// </summary>
        public int BleedMinimum
        {
            get
            {
                Rational<long> limit = RationalConversion.NarrowExact(
                    new Rational<Int128>(HealthMeter.MaxValue, Int128.One) * RationalConversion.Widen(BattleTuning.BleedLimit));
                //At least 1 below 0, however small the maximum health is
                return -int.Max(RationalConversion.FromRational<int>(limit, FractionRounding.AtLeast), 1);
            }
        }
        /// <summary>
        /// A bleed on this combatant is starting or ending. Its damage is the health meter's business and happens whatever this does.
        /// A combatant that can't be killed takes no notice, so for it a bleed is only damage.
        /// </summary>
        private void OnBleedEvent(BleedEvent<T> bleed)
        {
            if (bleed.LifeCyclePhase == ApplyOrRemove.Apply)
            {
                //One that was ended before it started still occurs, with nothing to it
                if (!CanBeKilled || bleed.HasEndedEarly)
                {
                    return;
                }
                _bleeds.Add(bleed);
                if (_bleeds.Count == 1)
                {
                    StartBleeding();
                }
                return;
            }
            if (_bleeds.Remove(bleed) && _bleeds.Count == 0)
            {
                StopBleeding();
            }
        }
        /// <summary>
        /// Called when the first bleed starts. The health can now go below 0, down to BleedMinimum, and getting there kills.
        /// A combatant that is up stops being knocked out at 0. One that is already knocked out stays knocked out,
        /// but its health meter is started fresh at 0 so that it can fall (and be healed, which the combatant hides while it is knocked out).
        /// </summary>
        private void StartBleeding()
        {
            if (State == CombatantState.KnockedOut)
            {
                HealthMeter.Revive(0);
            }
            DropPendingKnockOut();
            HealthMeter.UpdateMin(BleedMinimum, false);
            _deathWatch = HealthMeter.CreateKnockOutEvent(HealthMeter.MinValue, DeathPriority);
            _deathWatch.Occurring += OnDeathEvent;
            OwningTimeline.AddPossibleEvent(_deathWatch);
        }
        /// <summary>
        /// Called when the last bleed ends. The health can't go below 0 any more.
        /// A combatant that is up and at 0 or below is knocked out now. One that was already knocked out has time stop changing its health again.
        /// </summary>
        private void StopBleeding()
        {
            DropDeathWatch();
            bool atZeroOrBelow = HealthMeter.CurrentValue <= 0;
            HealthMeter.UpdateMin(0, true);
            if (State == CombatantState.KnockedOut)
            {
                HealthMeter.KnockOut();
            }
            else if (State == CombatantState.Up)
            {
                if (atZeroOrBelow)
                {
                    State = CombatantState.KnockedOut;
                    HealthMeter.KnockOut();
                    OnKnockedOut();
                }
                else
                {
                    WatchForKnockOut();
                }
            }
        }
        /// <summary>
        /// Stops caring about the death event, if there is one.
        /// It can't always be taken off the timeline right away, so it is told it can't occur and is no longer listened to.
        /// </summary>
        private void DropDeathWatch()
        {
            if (_deathWatch is null)
            {
                return;
            }
            _deathWatch.Occurring -= OnDeathEvent;
            _deathWatch.CouldOccur = false;
            _deathWatch = null;
        }
        /// <summary>
        /// Called when a bleeding combatant's health has fallen to the lowest it can go.
        /// </summary>
        private void OnDeathEvent(object sender, Okazo<T> okazo)
        {
            if (!ReferenceEquals(okazo, _deathWatch))
            {
                return; //Not one this combatant still cares about
            }
            _deathWatch = null;
            Kill();
        }
        /// <summary>
        /// Ends every bleed on this combatant without going through what the last one ending would do, for when everything about it is being cleared.
        /// The bleeds are ended early, so their damage comes off its health.
        /// </summary>
        private void ClearBleeds()
        {
            DropDeathWatch();
            foreach (BleedEvent<T> bleed in _bleeds.ToList())
            {
                bleed.EndEarly();
            }
            _bleeds.Clear();
            if (HealthMeter.MinValue < 0)
            {
                HealthMeter.UpdateMin(0, true);
            }
        }
        #endregion
        #region Going down and coming back
        /// <summary>
        /// A knocked out combatant deals no damage, so everything it was dealing ends. Its status effects stay, and are cleared if it is revived.
        /// </summary>
        protected override void OnKnockedOut()
        {
            EndDealing();
        }
        /// <summary>
        /// Kills the combatant, whether it was up or knocked out. Everything it was dealing ends, and its status effects and bleeds are cleared, since nothing can happen to the dead.
        /// </summary>
        /// <returns>True if it is now dead. False if it can't be killed, in which case nothing changes</returns>
        public override bool Kill()
        {
            bool wasDead = IsDead;
            if (!base.Kill())
            {
                return false;
            }
            if (!wasDead)
            {
                EndDealing();
                ClearStatuses();
                ClearBleeds();
            }
            return true;
        }
        /// <summary>
        /// Brings a knocked out combatant back up at the given health with nothing on it: no status effects, no bleeds, and nothing changing its health.
        /// </summary>
        /// <param name="newHealth">The health to come back at. Must be more than 0 and no more than the health meter's maximum</param>
        /// <returns>True if it was revived. False if it wasn't knocked out (it is up, or it is dead), in which case nothing changes</returns>
        /// <exception cref="ArgumentOutOfRangeException">If newHealth is 0 or less, or above the health meter's maximum. Nothing is changed in that case</exception>
        public override bool Revive(int newHealth)
        {
            if (State != CombatantState.KnockedOut || newHealth <= 0 || newHealth > HealthMeter.MaxValue)
            {
                //Nothing to clear. The base says no, or says what is wrong with the health.
                return base.Revive(newHealth);
            }
            ClearStatuses();
            ClearBleeds();
            return base.Revive(newHealth);
        }
        #endregion
        #region Cloning
        protected override BaseCombatant<T> CreateEmptyForClone(CloneContext context)
        {
            Dictionary<CombatStats, StatModificationMeter<T>> otherStats = [];
            foreach (KeyValuePair<CombatStats, StatModificationMeter<T>> stat in _otherStats)
            {
                otherStats.Add(stat.Key, context.GetOrClone(stat.Value));
            }
            Dictionary<MeterTypes, IMeter<T>> otherMeters = [];
            foreach (KeyValuePair<MeterTypes, IMeter<T>> meter in _otherMeters)
            {
                //The constructor checked that every one of these can be cloned
                otherMeters.Add(meter.Key, (IMeter<T>)context.GetOrClone((ICombatCloneable)meter.Value));
            }
            return new Combatant<T>(context.GetOrClone(HealthMeter), context.GetOrClone(Speed), KnockOutPriority, DeathPriority, otherStats, otherMeters);
        }
        protected override void CopyPropertiesForClone(BaseCombatant<T> clone, CloneContext context)
        {
            base.CopyPropertiesForClone(clone, context);
            if (clone is not Combatant<T> combatantClone)
            {
                throw new UnrecoverableCloneException("Somehow, we cloned a Combatant and got a different type back. This should never happen.");
            }
            //The clone doesn't listen to any of these events here. Rewiring the originals does that.
            foreach (KeyValuePair<StatusEffects, AbstractCombatant<T>?> status in _statusOwners)
            {
                combatantClone._statusOwners.Add(status.Key, status.Value is null ? null : context.GetOrClone(status.Value));
            }
            foreach (KeyValuePair<StatusEffects, IStatusEvent<T>> status in _statusEvents)
            {
                combatantClone._statusEvents.Add(status.Key, (IStatusEvent<T>)context.GetOrClone(status.Value.AsOkazo));
            }
            foreach (DamagePhaseEvent<T> damagePhase in _dealing)
            {
                if (!IsOver(damagePhase))
                {
                    combatantClone._dealing.Add(context.GetOrClone(damagePhase));
                }
            }
            foreach (DamagePhaseEvent<T> paused in _pausedDealing)
            {
                combatantClone._pausedDealing.Add(context.GetOrClone(paused));
            }
            foreach (BleedEvent<T> bleed in _bleeds)
            {
                combatantClone._bleeds.Add((BleedEvent<T>)context.GetOrClone<DamagePhaseEvent<T>>(bleed));
            }
            combatantClone._deathWatch = _deathWatch is null ? null : context.GetOrClone(_deathWatch);
        }
        #endregion
        public override string ToString()
        {
            return base.ToString()
                + (IsBleeding ? $", bleeding from {_bleeds.Count}" : "")
                + (_statusOwners.Count > 0 ? $", with {string.Join(", ", _statusOwners.Keys)}" : "");
        }
    }
}
