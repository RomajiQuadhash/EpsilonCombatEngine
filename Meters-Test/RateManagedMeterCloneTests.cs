using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Meters_Test
{
    /// <summary>
    /// Tests for cloning a RateManagedMeter along with its timeline.
    /// Nothing here listens to an event with anything but a method of an object that gets cloned too,
    /// since rewiring can't carry any other kind of listener over to a clone.
    /// So changes to the modifiers are made between runs while the timeline is Open, and a MostRecentEventHolder is what says a time was reached.
    /// </summary>
    /// <remarks>From Claude, with slightly more helper functions than I would do</remarks>
    public class RateManagedMeterCloneTests
    {
        /// <summary>
        /// Puts a card on the timeline at the given time and returns the holder that will hold that card once it has occurred.
        /// </summary>
        private static MostRecentEventHolder<int> StopAt(Timeline<int> timeline, int time)
        {
            MostRecentEventHolder<int> holder = new();
            Card<int, string> card = new(time, timeline, PriorityRank.None) { Face = $"Test stop at {time}" };
            card.Occurring += holder.OnTrigger;
            timeline.AddEvent(card);
            return holder;
        }
        /// <summary>
        /// Runs the timeline the way a game would until the holder's card has occurred, and leaves the timeline Open.
        /// </summary>
        private static void RunUntil(Timeline<int> timeline, MostRecentEventHolder<int> reached)
        {
            for (int round = 0; round < 10000 && reached.MostRecentEvent is null; round++)
            {
                foreach (StepReport<int> _ in timeline.TakeSteps())
                {
                }
                Assert.NotEqual(TimelinePhase.Terminated, timeline.Phase);
                timeline.SetOpen();
            }
            Assert.NotNull(reached.MostRecentEvent);
        }
        /// <summary>
        /// Clones the way a combat would: the timeline (and so every event on it), then whatever else is part of the combat, then the rewiring.
        /// </summary>
        /// <param name="alsoClone">Anything else that listens to an event on the timeline</param>
        private static CloneContext CloneEverything(Timeline<int> timeline, RateManagedMeter<int, int> meter, params MostRecentEventHolder<int>[] alsoClone)
        {
            CloneContext context = new();
            timeline.Clone(context);
            context.GetOrClone(meter);
            foreach (MostRecentEventHolder<int> holder in alsoClone)
            {
                context.GetOrClone(holder);
            }
            timeline.RewireAllEvents(context);
            return context;
        }
        private static T CloneOf<T>(CloneContext context, T original) where T : class
        {
            Assert.True(context.TryGet(original, out T clone), $"{original} was never cloned");
            return clone;
        }
        private static Rational<long> Fraction(long numerator, long denominator = 1) => new(numerator, denominator);
        /// <summary>
        /// Everything about a meter that can be seen from outside, for comparing a clone with its original.
        /// </summary>
        private static string Describe(RateManagedMeter<int, int> meter)
        {
            return $"value {meter.CurrentValue}, instant change {meter.InstantChange}, range {meter.MinValue} to {meter.MaxValue}, "
                + $"{meter.Derivative} every {meter.DerivativeDenominator} and {meter.TimeSinceLastIncrement} in, "
                + $"true rate {meter.TrueRate}, spillover {meter.CurrentSpilloverFraction}, "
                + $"leap year {meter.CurrentLeapYear} then {meter.UpcomingLeapYear}, "
                + $"blend {meter.BlendPerTick} for {meter.BlendTicksLeft}, waiting {meter.IsRateChangePending}, "
                + $"mid advance changes {meter.MidAdvanceRateChanges}, UUID {meter.UUID}, "
                + $"modifiers {string.Join("; ", meter.Modifiers.OrderBy(pair => pair.Key.Id).Select(pair => $"{pair.Key.Id}: {pair.Value}"))}";
        }
        /// <summary>
        /// A meter partway through a blend, with a new tick rate waiting for the tick in progress, stopped at time 5.
        /// Left alone from here, it reaches 112 at time 26 (the same numbers as the blend test for a tick rate change that has to wait).
        /// </summary>
        private static (Timeline<int> timeline, RateManagedMeter<int, int> meter, ModifierKey key, MostRecentEventHolder<int> at26) BlendingAndWaiting()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -100000, 100000);
            ModifierKey key = meter.Add(new ModInstance<int, int>(60, 10));
            MostRecentEventHolder<int> at4 = StopAt(timeline, 4);
            MostRecentEventHolder<int> at5 = StopAt(timeline, 5);
            MostRecentEventHolder<int> at26 = StopAt(timeline, 26);
            RunUntil(timeline, at4);
            meter.Replace(key, new ModInstance<int, int>(8, 2));   //Cancels the tick: 3 ticks carrying 8 extra each
            RunUntil(timeline, at5);
            meter.Replace(key, new ModInstance<int, int>(40, 10)); //Has to wait for the tick in progress
            Assert.True(meter.IsRateChangePending);
            Assert.Equal((8, 3), (meter.BlendPerTick, meter.BlendTicksLeft));
            return (timeline, meter, key, at26);
        }

        [Fact(DisplayName = "A clone matches its original in everything that can be seen, and is a different meter on the cloned timeline")]
        public void CloneMatchesOriginal()
        {
            var (timeline, meter, _, at26) = BlendingAndWaiting();

            CloneContext context = CloneEverything(timeline, meter, at26);
            RateManagedMeter<int, int> meterClone = CloneOf(context, meter);

            Assert.NotSame(meter, meterClone);
            Assert.Same(CloneOf(context, timeline), meterClone.OwningTimeline);
            Assert.Equal(Describe(meter), Describe(meterClone));
            Assert.Equal(meter, meterClone); //Same UUID and type
        }
        [Fact(DisplayName = "Cloning the meter again gives the same clone")]
        public void CloneTakesFromContext()
        {
            var (timeline, meter, _, at26) = BlendingAndWaiting();
            CloneContext context = CloneEverything(timeline, meter, at26);

            Assert.Same(CloneOf(context, meter), meter.Clone(context));
        }
        [Fact(DisplayName = "The meter can't be cloned before its timeline")]
        public void TimelineMustBeClonedFirst()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);

            Assert.Throws<UnrecoverableCloneException>(() => meter.Clone(new CloneContext()));
        }
        [Fact(DisplayName = "A clone taken mid-blend with a change waiting ends up exactly where its original does")]
        public void CloneAndOriginalFinishTheSame()
        {
            var (timeline, meter, _, at26) = BlendingAndWaiting();
            CloneContext context = CloneEverything(timeline, meter, at26);
            Timeline<int> timelineClone = CloneOf(context, timeline);
            RateManagedMeter<int, int> meterClone = CloneOf(context, meter);

            RunUntil(timeline, at26);
            RunUntil(timelineClone, CloneOf(context, at26));

            Assert.Equal(112, meter.CurrentValue);
            Assert.Equal(Describe(meter), Describe(meterClone));
            Assert.Equal(0, meterClone.MidAdvanceRateChanges);
        }
        [Fact(DisplayName = "A clone taken partway through a long run with leap years ends up exactly where its original does")]
        public void CloneAndOriginalFinishTheSameThroughLeapYears()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -100000, 100000);
            meter.Add(new ModInstance<int, int>(7, 3));
            meter.Add(new ModInstance<int, int>(1, 4)); //Together 31/12, so a leap year comes and goes every so often
            MostRecentEventHolder<int> at31 = StopAt(timeline, 31);
            MostRecentEventHolder<int> at600 = StopAt(timeline, 600);
            RunUntil(timeline, at31);

            CloneContext context = CloneEverything(timeline, meter, at600);
            Timeline<int> timelineClone = CloneOf(context, timeline);
            RateManagedMeter<int, int> meterClone = CloneOf(context, meter);
            RunUntil(timeline, at600);
            RunUntil(timelineClone, CloneOf(context, at600));

            Assert.Equal(Describe(meter), Describe(meterClone));
            Assert.Equal(Fraction(31 * 600, 12), Fraction(meterClone.CurrentValue) + meterClone.CurrentSpilloverFraction);
            Assert.Equal(0, meterClone.MidAdvanceRateChanges);
        }
        [Fact(DisplayName = "Running or changing the clone leaves the original alone, and the other way around")]
        public void CloneIsIndependent()
        {
            var (timeline, meter, key, at26) = BlendingAndWaiting();
            CloneContext context = CloneEverything(timeline, meter, at26);
            Timeline<int> timelineClone = CloneOf(context, timeline);
            RateManagedMeter<int, int> meterClone = CloneOf(context, meter);
            string originalBefore = Describe(meter);

            //The key from the original works on the clone, since the clone has the same modifiers under the same keys
            Assert.True(meterClone.Replace(key, new ModInstance<int, int>(3, 1)));
            meterClone.Add(new ModInstance<int, int>(-1, 7));
            RunUntil(timelineClone, CloneOf(context, at26));

            Assert.Equal(originalBefore, Describe(meter));
            Assert.Null(at26.MostRecentEvent);

            string cloneBefore = Describe(meterClone);
            RunUntil(timeline, at26);

            Assert.Equal(112, meter.CurrentValue);
            Assert.Equal(cloneBefore, Describe(meterClone));
        }
        [Fact(DisplayName = "The clone and its original give out the same key for the next modifier")]
        public void CloneContinuesTheSameKeys()
        {
            var (timeline, meter, _, at26) = BlendingAndWaiting();
            CloneContext context = CloneEverything(timeline, meter, at26);
            RateManagedMeter<int, int> meterClone = CloneOf(context, meter);

            Assert.Equal(meter.Add(new ModInstance<int, int>(1, 1)), meterClone.Add(new ModInstance<int, int>(1, 1)));
        }
        [Fact(DisplayName = "The cloned timeline has the same events as the original, and the clone's events tell the clone and nothing else")]
        public void CloneEventsTellTheClone()
        {
            var (timeline, meter, _, at26) = BlendingAndWaiting();
            CloneContext context = CloneEverything(timeline, meter, at26);
            Timeline<int> timelineClone = CloneOf(context, timeline);
            RateManagedMeter<int, int> meterClone = CloneOf(context, meter);

            List<Okazo<int>> originalEvents = [.. timeline.Events, .. timeline.PossibleEvents];
            List<Okazo<int>> cloneEvents = [.. timelineClone.Events, .. timelineClone.PossibleEvents];
            //Nothing was made twice: each original event has exactly its own clone on the cloned timeline
            Assert.Equal(originalEvents.Count, cloneEvents.Count);
            foreach (Okazo<int> original in originalEvents)
            {
                Assert.Contains(cloneEvents, cloneEvent => ReferenceEquals(cloneEvent, CloneOf(context, original)));
            }
            //Every event that told the original meter now has a clone that tells the clone meter once, and never the original
            int eventsTellingOriginal = 0;
            foreach (Okazo<int> original in originalEvents)
            {
                int tellsOriginal = original.GetOccurringSubscribers().Count(subscriber => ReferenceEquals(subscriber, meter));
                IReadOnlyList<object> cloneSubscribers = CloneOf(context, original).GetOccurringSubscribers();
                Assert.Equal(tellsOriginal, cloneSubscribers.Count(subscriber => ReferenceEquals(subscriber, meterClone)));
                Assert.DoesNotContain(cloneSubscribers, subscriber => ReferenceEquals(subscriber, meter));
                eventsTellingOriginal += tellsOriginal;
            }
            //Two leap year start events and the tick card
            Assert.Equal(3, eventsTellingOriginal);
        }
        [Fact(DisplayName = "The clone's tick card has the clone as its face, and cloning the timeline alone is enough to clone the meter through it")]
        public void TickCardBringsTheMeterAlong()
        {
            var (timeline, meter, _, _) = BlendingAndWaiting();
            CloneContext context = new();

            Timeline<int> timelineClone = (Timeline<int>)timeline.Clone(context);

            RateManagedMeter<int, int> meterClone = CloneOf(context, meter);
            Card<int, RateManagedMeter<int, int>> cardClone = Assert.Single(timelineClone.Events.Concat(timelineClone.PossibleEvents).OfType<Card<int, RateManagedMeter<int, int>>>());
            Assert.Same(meterClone, cardClone.Face);
            Assert.Equal(1, cardClone.TimeToEvent); //The tick in progress lands at 6
        }
        [Fact(DisplayName = "On the cloned timeline the meter still advances before its spillover, even though the timeline cloned the events first")]
        public void CloneAdvancesBeforeItsSpillover()
        {
            var (timeline, meter, _, at26) = BlendingAndWaiting();
            CloneContext context = CloneEverything(timeline, meter, at26);
            Timeline<int> timelineClone = CloneOf(context, timeline);
            RateManagedMeter<int, int> meterClone = CloneOf(context, meter);

            List<IAdvanceable<int>> order = [.. timelineClone.GetAdvanceSubscribers()];
            int meterPosition = order.FindIndex(subscriber => ReferenceEquals(subscriber, meterClone));
            //The only continuous meter of fractions on this timeline is the spillover
            int spilloverPosition = order.FindIndex(subscriber => subscriber is LinearContinuousMeter<int, Rational<long>>);

            Assert.True(meterPosition >= 0 && spilloverPosition >= 0);
            Assert.True(meterPosition < spilloverPosition, $"Meter is at {meterPosition} and its spillover at {spilloverPosition}");
        }
        [Fact(DisplayName = "Rewiring fails loudly if the meter itself was never cloned, since its events would have nothing to tell")]
        public void RewiringNeedsTheMeterCloned()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
            meter.Add(new ModInstance<int, int>(7, 3));
            CloneContext context = new();
            timeline.Clone(context); //Nothing on the timeline refers to the meter here: no tick card, and the leap year events only know the spillover

            Assert.False(context.TryGet(meter, out RateManagedMeter<int, int> _));
            Assert.Throws<UnrecoverableCloneException>(() => timeline.RewireAllEvents(context));

            //Once whatever owns the meter has cloned it, rewiring works
            context.GetOrClone(meter);
            timeline.RewireAllEvents(context);
        }
    }
}
