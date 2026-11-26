using CoreMetrics;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Combatants
{
    /// <summary>
    /// A card that says a Guard has just changed state. It does nothing by itself. A HealthMeter puts one on the timeline at the moment the change happens,
    /// so the change shows up among the events that occurred (in the StepReports), for anything that draws the timeline.
    /// Its face is which change it was.
    /// </summary>
    /// <remarks>Claude written, Romaji Approved</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public class GuardStateCard<T> : Card<T, GuardTransition>, IOkTiebreaker<T> where T : INumber<T>
    {
        /// <summary>
        /// The health meter whose Guard changed.
        /// </summary>
        public HealthMeter<T> Health { get; private set; }
        /// <summary>
        /// Which of that health meter's cards this is, counting up from the first. Cards made at the same moment occur in this order,
        /// so that leaving Staggered comes before becoming Broken.
        /// </summary>
        public long Sequence { get; }
        /// <summary>
        /// Makes a card for a change that is happening right now.
        /// </summary>
        /// <param name="health">The health meter whose Guard changed</param>
        /// <param name="transition">Which change it was</param>
        /// <param name="sequence">Which of that health meter's cards this is. Each one is higher than the last</param>
        /// <param name="owningTimeline">The timeline this is for. Must be the meter's</param>
        public GuardStateCard(HealthMeter<T> health, GuardTransition transition, long sequence, Timeline<T> owningTimeline) : base(T.Zero, owningTimeline, PriorityRank.MeterTriggerInstant)
        {
            Health = health;
            Face = transition;
            Sequence = sequence;
        }
        /// <summary>
        /// Puts cards for the same health meter in the order they were made. Says nothing about any other event.
        /// </summary>
        /// <param name="other">Another event at the same time and priority</param>
        /// <returns>Negative if this should come before other, 0 if this can't say, positive if it should come after</returns>
        public new int Tiebreaker(Okazo<T> other)
        {
            if (other is GuardStateCard<T> otherCard && ReferenceEquals(otherCard.Health, Health))
            {
                return Sequence.CompareTo(otherCard.Sequence);
            }
            return 0;
        }
        protected override BaseCard<T> CreateEmptyForClone(T timeToEvent, Timeline<T> owningTimeline)
        {
            //There is no clone context here, so this starts with the original's health meter and CopyPropertiesForClone swaps it for the clone's
            return new GuardStateCard<T>(Health, Face, Sequence, owningTimeline) { TimeToEvent = timeToEvent };
        }
        protected override void CopyPropertiesForClone(BaseCard<T> clone, CloneContext context)
        {
            base.CopyPropertiesForClone(clone, context);
            if (clone is GuardStateCard<T> cardClone)
            {
                cardClone.Health = context.GetOrClone(Health);
                return;
            }
            throw new UnrecoverableCloneException("Somehow, we cloned a GuardStateCard and got a different type back. This should never happen.");
        }
        public override string ToString()
        {
            return $"Guard {Face}! " + base.ToString();
        }
    }
    /// <summary>
    /// The changes of state a Guard can go through. Going from Staggered to Broken is two of these: leaving Staggered, then becoming Broken.
    /// </summary>
    public enum GuardTransition
    {
        /// <summary>The Guard reached 0 without being Broken. The next source of damage to start will Break it</summary>
        BecameStaggered,
        /// <summary>The Guard is no longer Staggered, either since it healed above 0 or since it was Broken</summary>
        LeftStaggered,
        /// <summary>A source of damage started while the Guard was Staggered. Damage over time is divided by the Defense alone</summary>
        BecameBroken,
        /// <summary>A Broken Guard healed back above 0 (or was put back to full), so it counts toward the Defense again</summary>
        LeftBroken
    }
    /// <summary>
    /// The states a Guard can be in.
    /// </summary>
    public enum GuardState
    {
        /// <summary>Above 0 and adding to the Defense</summary>
        Up,
        /// <summary>At 0 but still adding to the Defense. The next source of damage to start will Break it</summary>
        Staggered,
        /// <summary>Below 0 and not adding to the Defense, until it heals back above 0</summary>
        Broken
    }
}
