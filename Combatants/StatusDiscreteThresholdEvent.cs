using CoreMetrics;
using Meters;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Combatants
{
    /// <summary>
    /// A status effect that is applied after a set length of time and removed when a meter of whole numbers that ticks reaches a value.
    /// It occurs twice, the way a temporary change to a meter does: when it applies it goes back on the timeline to wait for the meter.
    /// Only what the meter does after the status is applied counts. If the meter is sitting exactly on the value when the status is applied, it is removed straight away.
    /// It isn't put on the timeline automatically the first time. Use AddEvent or AddPossibleEvent once it is made.
    /// </summary>
    /// <remarks>Claude written, Romaji approved</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for the meter. Integer</typeparam>
    public class StatusDiscreteThresholdEvent<T, M> : DiscreteReachThresholdEvent<T, M>, IStatusEvent<T> where T : INumber<T> where M : IBinaryInteger<M>
    {
        public StatusEffects Status { get; }
        public AbstractCombatant<T> Target { get; private set; }
        public AbstractCombatant<T>? Owner { get; private set; }
        public ApplyOrRemove LifeCyclePhase { get; private set; } = ApplyOrRemove.Apply;
        public bool IsFinished { get; private set; }
        public bool IsCancelled { get; private set; }
        public Okazo<T> AsOkazo => this;
        /// <summary>
        /// How long until the status is applied. Only counts down, and only means anything, while this is waiting to apply.
        /// </summary>
        public T TimeToApply { get; private set; }
        /// <summary>
        /// The priority while it is waiting to apply the status.
        /// </summary>
        public PriorityRank? ApplyPriority { get; }
        /// <summary>
        /// The priority while it is waiting to remove the status.
        /// </summary>
        public PriorityRank? RemovePriority { get; }
        /// <summary>
        /// Makes the event, with the combatant the status is for listening to it.
        /// </summary>
        /// <param name="target">The combatant the status is for</param>
        /// <param name="status">The status effect this applies and then removes</param>
        /// <param name="owner">The combatant that owns the status. Null if nobody owns it</param>
        /// <param name="waitingTime">How long from now until the status is applied. Negative for one that should have applied in the past, which makes the timeline rewind to then</param>
        /// <param name="owningMeter">The meter whose value removes the status. It doesn't have to be one of the target's</param>
        /// <param name="thresholdPoint">The value the meter has to reach for the status to be removed</param>
        /// <param name="applyPriority">The priority while it is waiting to apply. StatusEffectStart unless there is a reason for another</param>
        /// <param name="removePriority">The priority while it is waiting to remove. StatusEffectEnd unless there is a reason for another</param>
        /// <param name="owningTimeline">The timeline this is for. Must be the meter's and the target's</param>
        public StatusDiscreteThresholdEvent(AbstractCombatant<T> target, StatusEffects status, AbstractCombatant<T>? owner, T waitingTime, LinearDiscreteMeter<T, M> owningMeter, M thresholdPoint,
            PriorityRank? applyPriority, PriorityRank? removePriority, Timeline<T> owningTimeline)
            : this(target, status, owner, waitingTime, owningMeter, thresholdPoint, applyPriority, removePriority, owningTimeline, false, false)
        {
            Occurring += Target.OnOccur;
        }
        /// <summary>
        /// Constructor that only sets what it is given, for the public constructor and for clones. The target doesn't listen to the event it makes.
        /// </summary>
        protected StatusDiscreteThresholdEvent(AbstractCombatant<T> target, StatusEffects status, AbstractCombatant<T>? owner, T timeToApply, LinearDiscreteMeter<T, M> owningMeter, M thresholdPoint,
            PriorityRank? applyPriority, PriorityRank? removePriority, Timeline<T> owningTimeline, bool instantFlag, bool allowNegative)
            : base(owningMeter, thresholdPoint, applyPriority, owningTimeline, instantFlag, allowNegative)
        {
            Target = target;
            Status = status;
            Owner = owner;
            TimeToApply = timeToApply;
            ApplyPriority = applyPriority;
            RemovePriority = removePriority;
        }
        /// <summary>
        /// While waiting to apply, counts down the time left. While waiting to remove, the meter decides the time.
        /// </summary>
        public override void OnAdvance(object? sender, T timeElapsed)
        {
            base.OnAdvance(sender, timeElapsed);
            if (LifeCyclePhase == ApplyOrRemove.Apply)
            {
                TimeToApply -= timeElapsed;
            }
        }
        protected override TimeOrNever<T> ComputeTimeRemaining()
        {
            if (IsCancelled)
            {
                return new(false); //Nothing left to wait for, so it occurs as soon as the timeline runs and is finished
            }
            return LifeCyclePhase == ApplyOrRemove.Apply ? new(TimeToApply) : base.ComputeTimeRemaining();
        }
        public void Cancel()
        {
            if (!IsFinished)
            {
                IsCancelled = true;
            }
        }
        /// <summary>
        /// Nothing about the meter decides when the status is applied, so there is no transition data until it is waiting to remove.
        /// </summary>
        public override DiscreteTransitionData<T, M>? TransitionData => LifeCyclePhase == ApplyOrRemove.Apply ? null : base.TransitionData;
        /// <summary>
        /// Tells whoever is listening that the status is being applied or removed.
        /// After applying, it goes back on the timeline to wait for the meter. After removing, it is finished.
        /// </summary>
        /// <exception cref="InvalidOperationException">If the time isn't zero</exception>
        public override void Trigger()
        {
            if (LifeCyclePhase == ApplyOrRemove.Remove || IsCancelled)
            {
                //A cancelled one finishes the first time it occurs, whichever of the two that would have been
                base.Trigger();
                LifeCyclePhase = ApplyOrRemove.Remove;
                IsFinished = true;
                return;
            }
            if (TimeToApply != T.Zero)
            {
                throw new InvalidOperationException("Events should only occur at time zero.");
            }
            InvokeOccurring();
            LifeCyclePhase = ApplyOrRemove.Remove;
            SettablePriority = RemovePriority;
            //What the meter did while this was waiting to apply doesn't count toward removing it
            _instantFlag = false;
            _allowNegative = false;
            OwningTimeline.AddPossibleEvent(this);
        }
        protected override ReachThresholdEvent<T, M> CreateEmptyForClone(Timeline<T> owningTimeline, CloneContext context)
        {
            //The combatants are not cloned here. They are swapped in by CopyPropertiesForClone, once this clone is registered.
            //A combatant keeps hold of the events that are for it, so cloning one from here would have it ask for this event's clone before there is one,
            //and a second clone of this event would get made.
            //Rewiring is what makes the cloned target listen to it.
            return new StatusDiscreteThresholdEvent<T, M>(Target, Status, Owner, TimeToApply,
                context.GetOrClone(OwningMeter), ThresholdPoint, ApplyPriority, RemovePriority, owningTimeline, _instantFlag, _allowNegative);
        }
        protected override void CopyPropertiesForClone(ReachThresholdEvent<T, M> clone, CloneContext context)
        {
            base.CopyPropertiesForClone(clone, context);
            if (clone is StatusDiscreteThresholdEvent<T, M> statusClone)
            {
                statusClone.Target = context.GetOrClone(Target);
                statusClone.Owner = Owner is null ? null : context.GetOrClone(Owner);
                statusClone.LifeCyclePhase = LifeCyclePhase;
                statusClone.SettablePriority = SettablePriority;
                statusClone.IsFinished = IsFinished;
                statusClone.IsCancelled = IsCancelled;
                return;
            }
            throw new UnrecoverableCloneException("Somehow, we cloned a StatusDiscreteThresholdEvent and got a different type back. This should never happen.");
        }
        public override string? ToString()
        {
            string state = IsFinished ? "finished" : LifeCyclePhase == ApplyOrRemove.Apply ? $"applies in {TimeToApply}" : "to remove";
            return $"{Status} on {Target}, {state}. " + base.ToString();
        }
    }
}
