using CoreMetrics;
using Meters;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Combatants
{
    /// <summary>
    /// A status effect that is applied after a set length of time and removed a set length of time after that. Its face is the status.
    /// Unlike other cards it occurs twice, the way a temporary change to a meter does: when it applies it goes back on the timeline to wait for the removal.
    /// Like other cards, it isn't put on the timeline automatically the first time. Use AddEvent or AddPossibleEvent once it is made.
    /// </summary>
    /// <remarks>Claude written, Romaji approved</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public class StatusCard<T> : Card<T, StatusEffects>, IStatusEvent<T> where T : INumber<T>
    {
        public StatusEffects Status => Face;
        public AbstractCombatant<T> Target { get; private set; }
        public AbstractCombatant<T>? Owner { get; private set; }
        public ApplyOrRemove LifeCyclePhase { get; private set; } = ApplyOrRemove.Apply;
        public bool IsFinished { get; private set; }
        public bool IsCancelled { get; private set; }
        public Okazo<T> AsOkazo => this;
        /// <summary>
        /// How long the status stays, from when it is applied until it is removed.
        /// </summary>
        public T ApplyTime { get; }
        /// <summary>
        /// The priority while it is waiting to apply the status.
        /// </summary>
        public PriorityRank? ApplyPriority { get; }
        /// <summary>
        /// The priority while it is waiting to remove the status.
        /// </summary>
        public PriorityRank? RemovePriority { get; }
        /// <summary>
        /// Makes the card, with the combatant the status is for listening to it.
        /// </summary>
        /// <param name="target">The combatant the status is for</param>
        /// <param name="status">The status effect this applies and then removes</param>
        /// <param name="owner">The combatant that owns the status. Null if nobody owns it</param>
        /// <param name="waitingTime">How long from now until the status is applied. Negative for one that should have applied in the past, which makes the timeline rewind to then</param>
        /// <param name="applyTime">How long the status stays before it is removed. 0 or more</param>
        /// <param name="applyPriority">The priority while it is waiting to apply. StatusEffectStart unless there is a reason for another</param>
        /// <param name="removePriority">The priority while it is waiting to remove. StatusEffectEnd unless there is a reason for another</param>
        /// <param name="owningTimeline">The timeline this is for. Must be the target's</param>
        /// <exception cref="ArgumentOutOfRangeException">If applyTime is negative</exception>
        public StatusCard(AbstractCombatant<T> target, StatusEffects status, AbstractCombatant<T>? owner, T waitingTime, T applyTime, PriorityRank? applyPriority, PriorityRank? removePriority, Timeline<T> owningTimeline)
            : this(target, status, owner, waitingTime, NotNegative(applyTime), applyPriority, removePriority, owningTimeline, true)
        {
            Occurring += Target.OnOccur;
        }
        /// <summary>
        /// Constructor that only sets what it is given, for the public constructor and for clones. Nothing listens to the card it makes.
        /// </summary>
        /// <param name="forSubclasses">Not used. It is here to tell this constructor apart from the public one</param>
        protected StatusCard(AbstractCombatant<T> target, StatusEffects status, AbstractCombatant<T>? owner, T timeToEvent, T applyTime, PriorityRank? applyPriority, PriorityRank? removePriority, Timeline<T> owningTimeline, bool forSubclasses)
            : base(timeToEvent, owningTimeline, applyPriority)
        {
            Target = target;
            Owner = owner;
            Face = status;
            ApplyTime = applyTime;
            ApplyPriority = applyPriority;
            RemovePriority = removePriority;
        }
        /// <summary>
        /// Checks how long the status stays on the way into the base constructor, so that a refused card was never subscribed to the timeline.
        /// </summary>
        private static T NotNegative(T applyTime)
        {
            if (T.IsNegative(applyTime))
            {
                throw new ArgumentOutOfRangeException(nameof(applyTime), applyTime, "How long a status stays can't be negative");
            }
            return applyTime;
        }
        /// <summary>
        /// Tells whoever is listening that the status is being applied or removed.
        /// After applying, it goes back on the timeline to wait out the apply time. After removing, it is finished.
        /// </summary>
        /// <remarks>
        /// BaseCard asks for Trigger not to be overridden, since cards are only meant to tell their listeners. This still only tells its listeners.
        /// The one thing it adds is going back on the timeline for its second time.
        /// </remarks>
        /// <exception cref="InvalidOperationException">If the time isn't zero</exception>
        public override void Trigger()
        {
            if (LifeCyclePhase == ApplyOrRemove.Remove)
            {
                base.Trigger();
                IsFinished = true;
                return;
            }
            if (TimeToEvent != T.Zero)
            {
                throw new InvalidOperationException("Events should only occur at time zero.");
            }
            InvokeOccurring();
            LifeCyclePhase = ApplyOrRemove.Remove;
            Priority = RemovePriority;
            //If it was cancelled while applying (refused, say), it doesn't wait out its apply time before finishing
            TimeToEvent = IsCancelled ? T.Zero : ApplyTime;
            OwningTimeline.AddPossibleEvent(this);
        }
        public void Cancel()
        {
            if (IsFinished)
            {
                return;
            }
            IsCancelled = true;
            TimeToEvent = T.Zero;
        }
        protected override BaseCard<T> CreateEmptyForClone(T timeToEvent, Timeline<T> owningTimeline)
        {
            //There is no clone context here, so this starts with the original's combatants and CopyPropertiesForClone swaps them for the clones.
            //Rewiring is what makes the cloned target listen to it.
            return new StatusCard<T>(Target, Status, Owner, timeToEvent, ApplyTime, ApplyPriority, RemovePriority, owningTimeline, false);
        }
        protected override void CopyPropertiesForClone(BaseCard<T> clone, CloneContext context)
        {
            base.CopyPropertiesForClone(clone, context);
            if (clone is StatusCard<T> statusClone)
            {
                statusClone.Target = context.GetOrClone(Target);
                statusClone.Owner = Owner is null ? null : context.GetOrClone(Owner);
                statusClone.LifeCyclePhase = LifeCyclePhase;
                statusClone.Priority = Priority;
                statusClone.IsFinished = IsFinished;
                statusClone.IsCancelled = IsCancelled;
                if (IsFinished)
                {
                    //A finished one stopped listening to the timeline, and making the clone started it listening again.
                    statusClone.OwningTimeline.Advance -= statusClone.OnAdvance;
                }
                return;
            }
            throw new UnrecoverableCloneException("Somehow, we cloned a StatusCard and got a different type back. This should never happen.");
        }
        public override string ToString()
        {
            string state = IsFinished ? "finished" : LifeCyclePhase == ApplyOrRemove.Apply ? "to apply" : "to remove";
            return $"{Status} on {Target}, {state}. " + base.ToString();
        }
    }
}
