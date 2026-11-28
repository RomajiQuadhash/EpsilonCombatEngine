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
    /// The bare minimum of a combatant: health, speed, and whether it is up, knocked out or dead.
    /// It has no Guard of its own, no attack and none of the other stats. Classes that add those are built on this, the way Card is built on BaseCard.
    /// It makes no decisions, and it is the same whether a player or an AI is in control of it.
    /// Everything else an AbstractCombatant has is here as a stand-in that does nothing: it reads as empty, null or false, and setting it is ignored.
    /// Each one says so. A subclass that implements one of them overrides it.
    /// </summary>
    /// <remarks>Claude written, Romaji approved</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public class BaseCombatant<T> : AbstractCombatant<T> where T : INumber<T>
    {
        /// <summary>
        /// The meter behind this combatant's health. Changes to health are made to this, whatever state the combatant is in.
        /// What the combatant counts as having is Health, which hides this while it is knocked out or dead.
        /// </summary>
        public override HealthMeter<T> HealthMeter { get; }
        /// <summary>
        /// How fast the combatant is, compared with the fastest in the combat. Made by this combatant from the speed stat it was given.
        /// </summary>
        public override SpeedMeter<T> Speed { get; }
        /// <summary>
        /// The priority this combatant's knock out events are given: PlayerKO or EnemyKO. It is the only thing here that depends on whose side it is on,
        /// and only since the timeline orders the two differently.
        /// </summary>
        public PriorityRank KnockOutPriority { get; }
        /// <summary>
        /// Whether Kill does anything. Might be set per combatant or per battle (a human enemy that can't be killed while the aliens can).
        /// </summary>
        public override bool CanBeKilled { get; set; }
        /// <summary>
        /// Whether the combatant is up, knocked out or dead.
        /// </summary>
        public CombatantState State { get; private set; } = CombatantState.Up;
        /// <summary>
        /// The health this combatant counts as having: what the health meter says while it is up, and 0 while it is knocked out or dead.
        /// </summary>
        /// <remarks>
        /// The meter itself is not protected while the combatant is down. An instant heal still changes it.
        /// But that changes nothing about the combatant, since this stays 0 and only Revive brings it back.
        /// </remarks>
        public int Health => State == CombatantState.Up ? HealthMeter.CurrentValue : 0;
        /// <summary>
        /// True if the combatant has been knocked out and not revived.
        /// </summary>
        public override bool IsKOd => State == CombatantState.KnockedOut;
        /// <summary>
        /// True if the combatant is dead. Nothing brings it back.
        /// </summary>
        public override bool IsDead => State == CombatantState.Dead;
        /// <summary>
        /// True if the combatant is up, so it is able to do things.
        /// </summary>
        public override bool IsActive => State == CombatantState.Up;
        /// <summary>
        /// The event that will knock this combatant out when its health falls to 0. Null while it is knocked out or dead.
        /// It occurs once. Reviving makes a new one.
        /// </summary>
        /// <remarks>Something that wants to know when this combatant is knocked out can listen to this, and to the new one after each revive.</remarks>
        public DiscreteReachThresholdEvent<T, int>? PendingKnockOut { get; private set; }

        /// <summary>
        /// Creates a combatant that is up, makes its speed meter, and puts the event that will knock it out on the timeline.
        /// </summary>
        /// <param name="healthMeter">The meter for this combatant's health. The combatant is on this meter's timeline</param>
        /// <param name="speedAggregator">What every speed in the combat is compared through. Share one across the whole combat</param>
        /// <param name="speedStat">The speed with no buffs or debuffs. More than 0</param>
        /// <param name="minSpeedRatio">The lowest Ratio the speed can be debuffed to. 0 or less</param>
        /// <param name="maxSpeedRatio">The highest Ratio the speed can be buffed to. 0 or more</param>
        /// <param name="knockOutPriority">The priority for this combatant's knock out events: PlayerKO or EnemyKO</param>
        [SetsRequiredMembers]
        public BaseCombatant(HealthMeter<T> healthMeter, MaxStatAggregator<T, SpeedMeter<T>> speedAggregator, int speedStat, Rational<long> minSpeedRatio, Rational<long> maxSpeedRatio, PriorityRank knockOutPriority)
            : this(healthMeter, new SpeedMeter<T>(healthMeter.OwningTimeline, speedStat, minSpeedRatio, maxSpeedRatio, speedAggregator), knockOutPriority)
        {
            WatchForKnockOut();
        }
        /// <summary>
        /// Constructor that only sets what it is given, for the public constructor and for clones. It puts nothing on the timeline.
        /// </summary>
        /// <param name="healthMeter">The meter for this combatant's health</param>
        /// <param name="speed">This combatant's speed meter, already made</param>
        /// <param name="knockOutPriority">The priority for this combatant's knock out events</param>
        [SetsRequiredMembers]
        protected BaseCombatant(HealthMeter<T> healthMeter, SpeedMeter<T> speed, PriorityRank knockOutPriority)
        {
            OwningTimeline = healthMeter.OwningTimeline;
            HealthMeter = healthMeter;
            Speed = speed;
            KnockOutPriority = knockOutPriority;
        }

        #region Combat stats
        /// <summary>
        /// The combat stats this combatant has, whichever of its parts holds them:
        /// Speed (the same meter the Speed property gives), Defense (which the health meter divides damage by),
        /// and GuardStrength if the health meter was made with a Guard.
        /// Made new each time it is read, so adding to or removing from it changes nothing.
        /// </summary>
        /// <remarks>
        /// Setting it is ignored. This combatant's stats are the ones its meters were made with.
        /// For anything about timing, use the Speed property and not the Speed entry here, which is only the raw speed.
        /// </remarks>
        public override IDictionary<CombatStats, StatModificationMeter<T>> ChangeableStats
        {
            get
            {
                Dictionary<CombatStats, StatModificationMeter<T>> stats = new()
                {
                    [CombatStats.Speed] = Speed,
                    [CombatStats.Defense] = HealthMeter.Defense
                };
                if (HealthMeter.GuardStrength is not null)
                {
                    stats[CombatStats.GuardStrength] = HealthMeter.GuardStrength;
                }
                return stats;
            }
            set { }
        }
        /// <summary>
        /// The meters this combatant has besides health: the Guard meter, if the health meter was made with one.
        /// Made new each time it is read, so adding to or removing from it changes nothing.
        /// </summary>
        /// <remarks>Setting it is ignored.</remarks>
        public override IDictionary<MeterTypes, IMeter<T>> AdditionalMeters
        {
            get
            {
                Dictionary<MeterTypes, IMeter<T>> meters = [];
                if (HealthMeter.Guard is not null)
                {
                    meters[MeterTypes.Guard] = HealthMeter.Guard;
                }
                return meters;
            }
            set { }
        }
        #endregion
        #region Not implemented here
        //Everything in this region is a stand-in. It reads as empty, null or false, and setting or calling it does nothing.
        /// <summary>
        /// Not implemented in BaseCombatant: it never bleeds, so this is always false.
        /// </summary>
        public override bool IsBleeding => false;
        /// <summary>
        /// Not implemented in BaseCombatant: nothing but being knocked out or dead stops it, so this is always false.
        /// </summary>
        public override bool IsOtherwiseIncapacitated => false;
        /// <summary>
        /// Not implemented in BaseCombatant: it has no status effects. Always empty, made new each time it is read, and setting it is ignored.
        /// </summary>
        public override IDictionary<StatusEffects, AbstractCombatant<T>?> StatusEffects { get => new Dictionary<StatusEffects, AbstractCombatant<T>?>(); set { } }
        /// <summary>
        /// Not implemented in BaseCombatant: it takes no turns. Always null, and setting it is ignored.
        /// </summary>
        public override AbstractTurnCard<T, object>? TurnCard { get => null; set { } }
        /// <summary>
        /// Not implemented in BaseCombatant: it takes no turns, so this is a holder of its own that nothing else shares and nothing ever claims.
        /// </summary>
        public override CurrentTurnHolder<AbstractCombatant<T>> TurnHolder { get; } = new();
        /// <summary>
        /// Not implemented in BaseCombatant: it does nothing about events it is told of.
        /// </summary>
        public override void OnOccur(object _, Okazo<T> okazo)
        {
        }
        /// <summary>
        /// Not implemented in BaseCombatant: it has no events specific to itself
        /// </summary>
        protected override void OnOccurSelf(object _, Okazo<T> okazo)
        {
        }
        #endregion
        #region Going down and coming back
        /// <summary>
        /// Puts the event for health falling to 0 on the timeline, with this combatant listening to it.
        /// </summary>
        private void WatchForKnockOut()
        {
            //The health meter listens to this first, and stops time from changing it. Then this combatant notes that it is knocked out.
            PendingKnockOut = HealthMeter.CreateKnockOutEvent(0, KnockOutPriority);
            PendingKnockOut.Occurring += OnKnockOutEvent;
            OwningTimeline.AddPossibleEvent(PendingKnockOut);
        }
        /// <summary>
        /// Stops caring about the pending knock out event, if there is one.
        /// It can't always be taken off the timeline right away, so it is told it can't occur and is no longer listened to.
        /// </summary>
        private void DropPendingKnockOut()
        {
            if (PendingKnockOut is null)
            {
                return;
            }
            PendingKnockOut.Occurring -= OnKnockOutEvent;
            PendingKnockOut.CouldOccur = false;
            PendingKnockOut = null;
        }
        /// <summary>
        /// Called when the knock out event occurs.
        /// </summary>
        private void OnKnockOutEvent(object sender, Okazo<T> okazo)
        {
            if (!ReferenceEquals(okazo, PendingKnockOut))
            {
                return; //Not one this combatant still cares about
            }
            PendingKnockOut = null;
            if (State == CombatantState.Up)
            {
                State = CombatantState.KnockedOut;
            }
        }
        /// <summary>
        /// Kills the combatant, whether it was up or knocked out. Its health counts as 0 from then on, time stops changing its health meter, and it can't be revived.
        /// </summary>
        /// <returns>True if it is now dead. False if it can't be killed, in which case nothing changes</returns>
        public bool Kill()
        {
            if (!CanBeKilled)
            {
                return false;
            }
            if (State == CombatantState.Dead)
            {
                return true;
            }
            DropPendingKnockOut();
            State = CombatantState.Dead;
            HealthMeter.KnockOut();
            return true;
        }
        /// <summary>
        /// Brings a knocked out combatant back up at the given health, with nothing changing its health, and able to be knocked out again.
        /// This is the only thing that brings a combatant back. Healing of any kind doesn't.
        /// </summary>
        /// <param name="newHealth">The health to come back at. Must be more than 0 and no more than the health meter's maximum</param>
        /// <returns>True if it was revived. False if it wasn't knocked out (it is up, or it is dead), in which case nothing changes</returns>
        /// <exception cref="ArgumentOutOfRangeException">If newHealth is 0 or less, or above the health meter's maximum. Nothing is changed in that case</exception>
        public bool Revive(int newHealth)
        {
            if (newHealth <= 0 || newHealth > HealthMeter.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(newHealth), newHealth, $"The health to come back at must be more than 0 and at most {HealthMeter.MaxValue}");
            }
            if (State != CombatantState.KnockedOut)
            {
                return false;
            }
            HealthMeter.Revive(newHealth);
            State = CombatantState.Up;
            WatchForKnockOut();
            return true;
        }
        /// <summary>
        /// Makes the card that revives this combatant at a given health after a length of time. The health to come back at is the card's face.
        /// If the combatant isn't knocked out when the card occurs, the card does nothing.
        /// </summary>
        /// <remarks>Like other cards, it isn't put on the timeline. Use AddEvent or AddPossibleEvent once it is made.</remarks>
        /// <param name="timeToEvent">How long from now until the revive</param>
        /// <param name="newHealth">The health to come back at. Must be more than 0 and no more than the health meter's maximum</param>
        /// <param name="priority">PlayerRes for a player's combatant. Enemies have no priority of their own for this yet</param>
        /// <returns>The card, which revives this combatant when it occurs</returns>
        /// <exception cref="ArgumentOutOfRangeException">If newHealth is 0 or less, or above the health meter's maximum</exception>
        public Card<T, int> CreateReviveCard(T timeToEvent, int newHealth, PriorityRank priority)
        {
            if (newHealth <= 0 || newHealth > HealthMeter.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(newHealth), newHealth, $"The health to come back at must be more than 0 and at most {HealthMeter.MaxValue}");
            }
            Card<T, int> revive = new(timeToEvent, OwningTimeline, priority) { Face = newHealth };
            revive.Occurring += OnReviveCard;
            return revive;
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
        /// <summary>
        /// Clones this combatant along with its meters and its pending knock out event.
        /// Subclasses override CreateEmptyForClone, and CopyPropertiesForClone if they have more to copy.
        /// </summary>
        /// <remarks>
        /// The timeline has to be cloned first, and the timeline's events rewired after every combatant is cloned.
        /// The clone doesn't listen to its events here. Rewiring does that, as for anything else that listens to an event.
        /// </remarks>
        /// <exception cref="UnrecoverableCloneException">If the owning timeline hasn't been cloned yet</exception>
        public override object Clone(CloneContext context)
        {
            if (context.TryGet(this, out BaseCombatant<T> existingClone))
            {
                return existingClone;
            }
            if (!context.TryGet(OwningTimeline, out Timeline<T> _))
            {
                throw new UnrecoverableCloneException("Owning timeline must be cloned before cloning any combatants.");
            }
            BaseCombatant<T> clone = CreateEmptyForClone(context);
            context.Register(this, clone);
            CopyPropertiesForClone(clone, context);
            return clone;
        }
        /// <summary>
        /// Makes a new instance of this class from the clones of this combatant's meters, with nothing else set.
        /// A subclass overrides this to make one of itself with its own clone constructor.
        /// </summary>
        /// <param name="context">The clone context, for the clones of the meters</param>
        protected virtual BaseCombatant<T> CreateEmptyForClone(CloneContext context)
        {
            return new BaseCombatant<T>(context.GetOrClone(HealthMeter), context.GetOrClone(Speed), KnockOutPriority);
        }
        /// <summary>
        /// Fills in a clone to match this combatant. Override this (but always call base.CopyPropertiesForClone) in subclasses to copy any additional properties.
        /// </summary>
        /// <param name="clone">The new clone to modify. Already registered with the context</param>
        /// <param name="context">The clone context, for the clones of the events this combatant refers to</param>
        protected virtual void CopyPropertiesForClone(BaseCombatant<T> clone, CloneContext context)
        {
            clone.UUID = UUID;
            clone.CanBeKilled = CanBeKilled;
            clone.State = State;
            clone.PendingKnockOut = PendingKnockOut is null ? null : context.GetOrClone(PendingKnockOut);
        }
        #endregion
        public override string ToString()
        {
            return $"{GetType().Name} (UUID: {UUID}) that is {State} with {Health} health";
        }
    }
    /// <summary>
    /// Whether a combatant is able to do things, and if not, whether it can come back.
    /// </summary>
    public enum CombatantState
    {
        /// <summary>Able to do things</summary>
        Up,
        /// <summary>Health reached 0. Nothing over time changes its health, and only a revive brings it back</summary>
        KnockedOut,
        /// <summary>Dead. Nothing brings it back</summary>
        Dead
    }
}
