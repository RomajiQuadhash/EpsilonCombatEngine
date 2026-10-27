using CoreMetrics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Meters
{
    /// <summary>
    /// Event triggered by a threshold being touched on a continuous meter
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for the metric</typeparam>
    public class TouchThresholdEvent<T, M> : AbstractThresholdEvent<T, M> where T : INumber<T> where M : INumber<M>
    {
        protected AbstractMeter<T, M> _owningMeter;
        protected bool _allowNegative;
        /// <summary>
        /// Set to true if we changed instantly.
        /// </summary>
        protected bool _instantFlag;
        /// <summary>
        /// Set if we are subscribed properly
        /// </summary>
        protected bool _properlySubscribed;

        public TouchThresholdEvent(AbstractMeter<T, M> owningMeter, M thresholdPoint, PriorityRank? priority, Timeline<T> owningTimeline)
    : this(owningMeter, thresholdPoint, priority, owningTimeline, instantFlag: false, allowNegative: false) { }
        /// <summary>
        /// Clone constructor: carries over the clone-only state.
        /// </summary>
        protected TouchThresholdEvent(AbstractMeter<T, M> owningMeter, M thresholdPoint, PriorityRank? priority, Timeline<T> owningTimeline, bool instantFlag, bool allowNegative)
        {
            _owningMeter = owningMeter;
            ThresholdPoint = thresholdPoint;
            SettablePriority = priority;
            OwningTimeline = owningTimeline;
            _instantFlag = instantFlag;
            _allowNegative = allowNegative;
            SubscribeToAll();
        }
        /// <summary>
        /// Called on construction and should be called again if adding back to the Timeline
        /// </summary>
        public void SubscribeToAll()
        {
            OwningMeter.InstantChangeEvt += OnInstantChange;
            OwningMeter.ContinuousChangeEvt += OnContinuousChange;
            OwningTimeline.Advance += OnAdvance; //This doesn't do anything here, but we should always do it for events.
            _properlySubscribed = true;
        }

        public PriorityRank? SettablePriority { get; set; }
        public override M ThresholdPoint { get; set; }
        public Timeline<T> OwningTimeline {  get; init; }
        public override PriorityRank? Priority => SettablePriority;

        /// <summary>
        /// What meter is this for? Note, assumes a continuous meter.
        /// </summary>
        public override AbstractMeter<T, M> OwningMeter => _owningMeter;
        public override TransitionData<M>? TransitionData { get {
                TimeOrNever<T> timeRemaining = ComputeTimeRemaining();
                if (timeRemaining.IsNever)
                {
                    return null;
                }
                if (T.IsZero(timeRemaining.Time))
                {
                   return new(_owningMeter.InstantChange, _owningMeter.CurrentValue, _owningMeter.Derivative, _owningMeter.Range, ThresholdPoint);
                }
                return new(M.Zero, ThresholdPoint, _owningMeter.Derivative, _owningMeter.Range, ThresholdPoint);
            } }

        public override bool HaltsInstantAction { get; set; }
        /// <summary>
        /// Guarantee that there's at least one difference between two transition events
        /// UNLESS YOU'RE A UNIT TEST, NEVER SET THIS
        /// </summary>
        public override int UUID { get; set; } = Guid.NewGuid().GetHashCode();

        public override void Trigger()
        {
            if (ComputeTimeRemaining() != new TimeOrNever<T>(false)) //Time of 0
            {
                throw new InvalidOperationException("Events should only occur at time zero.");
            }
            InvokeOccurring();

            //Unsubscribe the event (use SubscribeToAll if adding again)
            OwningTimeline.Advance -= OnAdvance;
            OwningMeter.InstantChangeEvt -= OnInstantChange;
            OwningMeter.ContinuousChangeEvt -= OnContinuousChange;
            _properlySubscribed = false;

            //Reset information
            _instantFlag = false;
            _allowNegative = false;
        }

        public override object Clone(CloneContext context)
        {
            if (context.TryGet(this, out TouchThresholdEvent<T, M> existing)) return existing;
            if (!context.TryGet(OwningTimeline, out Timeline<T> clonedTimeline))
                throw new UnrecoverableCloneException("Timeline must be cloned before its threshold event.");
            AbstractMeter<T,M> clonedMeter = context.GetOrClone(OwningMeter);
            TouchThresholdEvent<T,M> clone = new(clonedMeter,ThresholdPoint,Priority,clonedTimeline,_instantFlag, _allowNegative);
            context.Register(this, clone);
            
            //Move other properties
            clone.UUID=UUID;
            clone.HaltsInstantAction=HaltsInstantAction;
            clone.Direction=Direction;
            clone.CouldOccur=CouldOccur;
            return clone;
        }

        public override void OnAdvance(object? sender, T timeElapsed)
        {
            if (sender != OwningTimeline)
            {
                throw new InvalidOperationException("Don't subscribe a card to a timeline it doesn't belong to.");
            }
            if (_properlySubscribed==false)
            {
                throw new InvalidOperationException("Subscribe only with SubscribeToAll");
            }
            //We don't do anything else, since we recalculate based on the meter and its events
        }

        public override string? ToString()
        {
            return $"{GetType()} (UUID: {UUID}) with ThresholdPoint {ThresholdPoint}, OwningMeter {OwningMeter},"
                +(Direction!=ThresholdDirection.Both ? $" Direction {Direction},": "")
                +$" and an estimate time to event of {ComputeTimeRemaining()}";
        }

        protected override TimeOrNever<T> ComputeTimeRemaining()
        {
            if (_instantFlag)
            {
                return new(false); //An instant change has told us we should occur as soon as possible
            }
            if (OwningMeter.Derivative < M.Zero && Direction==ThresholdDirection.Up)
            {
                //If it is going down, it can't cross through going up
                return new(true);
            }
            if (OwningMeter.Derivative > M.Zero && Direction==ThresholdDirection.Down)
            {
                //If it is going up, it can't cross through going down
                return new(true);
            }
            return OwningMeter.ThresholdTouch(ThresholdPoint, _allowNegative);
        }
        protected override void OnInstantChange(object sender, M before, M after)
        {
            if (sender != OwningMeter)
            {
                throw new InvalidOperationException($"Sender {sender} must be the OwningMeter {OwningMeter}." +
                                                "Don't manually subscribe to other objects");
            }
            if (_properlySubscribed == false)
            {
                throw new InvalidOperationException("Subscribe only with SubscribeToAll");
            }
            if (M.Sign(before-ThresholdPoint)*M.Sign(after-ThresholdPoint) > 0)
            {
                //No crossing or touching has occurred.
                return;
            }
            int changeDir=M.Sign(after-before);
            if (changeDir==-1 && Direction == ThresholdDirection.Up)
            {
                //We moved downward, so we should wipe marking it at all, since we only care about going up
                _instantFlag = false;
                return;
            }
            if (changeDir==1 && Direction== ThresholdDirection.Down)
            {
                //We moved upward, so we should wipe marking it at all, since we only care about going down
                _instantFlag = false;
                return;
            }
            _instantFlag=true; //We crossed or touched and the direction doesn't nullify it.
        }
        protected override void OnContinuousChange(object sender, T timeElapsed, M before, M after)
        {
            if (sender != OwningMeter)
            {
                throw new InvalidOperationException($"Sender {sender} must be the OwningMeter {OwningMeter}." +
                                                "Don't manually subscribe to other objects");
            }
            if (_properlySubscribed == false)
            {
                throw new InvalidOperationException("Subscribe only with SubscribeToAll");
            }
            //Default: Don't allow negative
            _allowNegative = false;
            if (after<ThresholdPoint && Direction == ThresholdDirection.Up)
            {
                return; //We are below afterwards when we should move up
            }
            if (after>ThresholdPoint && Direction == ThresholdDirection.Down)
            {
                return; //We are above afterwards when we should be moving down
            }
            if (M.Sign(before - ThresholdPoint) * M.Sign(after - ThresholdPoint) > 0)
            {
                return; //We didn't cross so no negative
            }
            TimeOrNever<T> anticipatedTimeRemaining = OwningMeter.ThresholdTouch(ThresholdPoint, true);
            if (new TimeOrNever<T>(-timeElapsed).CompareTo(anticipatedTimeRemaining)<=0)
            {
                //This means if we allowed negative, the value would be, at earliest, before advancing.
                _allowNegative = true;
            }
        }
    }
}
