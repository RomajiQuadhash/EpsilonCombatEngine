using Meters;
using Rationalz;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Meters_Test
{
    /// <summary>
    /// Tests for what a RateManagedMeter does with what a cancelled tick had built up:
    /// it is blended evenly into the next ticks so that none of it lands sooner than the cancelled tick would have.
    /// All of them run through the timeline's TakeSteps.
    /// </summary>
    /// <remarks>From Claude, with slightly more helper functions than I would do</remarks>
    public class RateManagedMeterBlendTests
    {
        /// <summary>
        /// Runs the timeline the way a game would, reopening it after each Display, until the given event has occurred.
        /// <remarks>A MostRecentEventHolder listening to that event is what says it has occurred.</remarks>
        private static void RunUntil(Timeline<int> timeline, Okazo<int> stopAfter)
        {
            MostRecentEventHolder<int> reached = new();
            stopAfter.Occurring += reached.OnTrigger;
            for (int round = 0; round < 10000; round++)
            {
                if (timeline.Phase == TimelinePhase.Display)
                {
                    timeline.SetOpen();
                }
                foreach (StepReport<int> _ in timeline.TakeSteps())
                {
                }
                if (reached.MostRecentEvent is not null || timeline.Phase == TimelinePhase.Terminated)
                {
                    return;
                }
            }
            Assert.Fail("The timeline never reached the event it was run until.");
        }
        /// <summary>
        /// A card that does nothing but mark a time for a test to run until, or to do or look at something at.
        /// </summary>
        private static Card<int, string> At(Timeline<int> timeline, int time, Action? whenItOccurs = null)
        {
            Card<int, string> marker = new(time, timeline, PriorityRank.None) { Face = $"Test marker at {time}" };
            if (whenItOccurs is not null)
            {
                marker.Occurring += (_, _) => whenItOccurs();
            }
            timeline.AddEvent(marker);
            return marker;
        }
        /// <summary>
        /// Notes the meter's value at each of the given times.
        /// </summary>
        private static Dictionary<int, int> ValuesAt(Timeline<int> timeline, RateManagedMeter<int, int> meter, params int[] times)
        {
            Dictionary<int, int> values = [];
            foreach (int time in times)
            {
                At(timeline, time, () => values[time] = meter.CurrentValue);
            }
            return values;
        }
        private static Rational<long> Fraction(long numerator, long denominator = 1) => new(numerator, denominator);

        [Fact(DisplayName = "What a cancelled tick had built up is split over the ticks up to when it would have landed")]
        public void BlendSplitsEvenlyOverTheTicks()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -100000, 100000);
            ModifierKey key = meter.Add(new ModInstance<int, int>(50, 10));
            //At 4, the tick has built up 20 and would have landed in 6. A tick every 4 gets there in 2 ticks, so 10 extra each.
            At(timeline, 4, () => meter.Replace(key, new ModInstance<int, int>(8, 4)));
            (int perTick, int blend, int ticksLeft) at5 = default;
            At(timeline, 5, () => at5 = (meter.Derivative, meter.BlendPerTick, meter.BlendTicksLeft));
            Dictionary<int, int> values = ValuesAt(timeline, meter, 7, 8, 12, 16);
            Card<int, string> end = At(timeline, 17);

            RunUntil(timeline, end);

            Assert.Equal((18, 10, 2), at5);
            Assert.Equal(0, values[7]);  //Nothing lands before the new schedule's first tick
            Assert.Equal(18, values[8]);
            Assert.Equal(36, values[12]); //The last of it lands at 12, not before the 10 that the cancelled tick was due at
            Assert.Equal(44, values[16]); //Back to the ordinary 8
            Assert.Equal((8, 0, 0), (meter.Derivative, meter.BlendPerTick, meter.BlendTicksLeft));
            Assert.Equal(Fraction(0), meter.CurrentSpilloverFraction);
            Assert.Equal(0, meter.MidAdvanceRateChanges);
        }
        [Fact(DisplayName = "What doesn't divide evenly into the blend goes to the spillover and is paid by an ordinary leap year")]
        public void BlendRemainderGoesToALeapYear()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -100000, 100000);
            ModifierKey key = meter.Add(new ModInstance<int, int>(63, 7));
            //At 5, the tick has built up 45 and would have landed in 2. A tick every 1 gets there in 2 ticks: 22 extra each, and 1 left over.
            At(timeline, 5, () => meter.Replace(key, new ModInstance<int, int>(3, 1)));
            Dictionary<int, int> values = ValuesAt(timeline, meter, 6, 7, 8);
            Card<int, string> end = At(timeline, 9);

            RunUntil(timeline, end);

            Assert.Equal(26, values[6]); //3 + 22 + 1 for the leap year the leftover started
            Assert.Equal(51, values[7]); //3 + 22. All 45 has now landed, along with two ordinary ticks of 3
            Assert.Equal(54, values[8]);
            Assert.Equal(LeapYearState.None, meter.CurrentLeapYear);
            Assert.Equal(Fraction(0), meter.CurrentSpilloverFraction);
            Assert.Equal(0, meter.MidAdvanceRateChanges);
        }
        [Fact(DisplayName = "An amount too small to give every tick at least 1 isn't blended, and all goes to the spillover")]
        public void TooSmallToBlend()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -100000, 100000);
            ModifierKey key = meter.Add(new ModInstance<int, int>(7, 7));
            //At 1, the tick has built up 1 and would have landed in 6. That is 6 ticks at a tick every 1, and 1 doesn't go into 6.
            (int blend, int ticksLeft) atChange = (-1, -1);
            At(timeline, 1, () =>
            {
                meter.Replace(key, new ModInstance<int, int>(3, 1));
                atChange = (meter.BlendPerTick, meter.BlendTicksLeft);
            });
            Dictionary<int, int> values = ValuesAt(timeline, meter, 2, 3);
            Card<int, string> end = At(timeline, 4);

            RunUntil(timeline, end);

            Assert.Equal((0, 0), atChange);
            Assert.Equal(4, values[2]); //3 + 1 for the leap year
            Assert.Equal(7, values[3]);
            Assert.Equal(Fraction(0), meter.CurrentSpilloverFraction);
        }
        [Fact(DisplayName = "If the modifiers go away during a blend, the blend is still paid out and then the meter stops")]
        public void BlendIsPaidOutAfterTheModifiersGoAway()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -100000, 100000);
            ModifierKey key = meter.Add(new ModInstance<int, int>(60, 10));
            //At 4: 24 built up, due in 6, so 3 ticks of a tick every 2 with 8 extra each (at 6, 8 and 10)
            At(timeline, 4, () => meter.Replace(key, new ModInstance<int, int>(8, 2)));
            //At 7 the second of those ticks is in progress. It lands as it was, and the third then only has the blend to add.
            At(timeline, 7, () => meter.Remove(key));
            Dictionary<int, int> values = ValuesAt(timeline, meter, 6, 8, 9, 10);
            Card<int, string> end = At(timeline, 30);

            RunUntil(timeline, end);

            Assert.Equal(16, values[6]);
            Assert.Equal(32, values[8]);
            Assert.Equal(32, values[9]);
            Assert.Equal(40, values[10]); //Just the 8 of the blend
            //All 24 landed, plus the two ordinary 8s from before the modifier went away
            Assert.Equal(40, meter.CurrentValue);
            Assert.Equal((0, 0, 0), (meter.Derivative, meter.BlendPerTick, meter.BlendTicksLeft));
            Assert.Equal(Fraction(0), meter.CurrentSpilloverFraction);
            Assert.Equal(0, meter.MidAdvanceRateChanges);
        }
        [Fact(DisplayName = "Going to no modifiers doesn't start a blend of its own")]
        public void StoppingDoesNotBlend()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -100000, 100000);
            ModifierKey key = meter.Add(new ModInstance<int, int>(50, 10));
            (int blend, int ticksLeft) atChange = (-1, -1);
            At(timeline, 4, () =>
            {
                meter.Remove(key);
                atChange = (meter.BlendPerTick, meter.BlendTicksLeft);
            });
            Card<int, string> end = At(timeline, 30);

            RunUntil(timeline, end);

            Assert.Equal((0, 0), atChange);
            Assert.Equal(50, meter.CurrentValue);
        }
        [Fact(DisplayName = "If the tick rate changes during a blend and has to wait, what the blend still owes is spread again on the new schedule")]
        public void BlendIsSpreadAgainWhenTheTickRateChanges()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -100000, 100000);
            ModifierKey key = meter.Add(new ModInstance<int, int>(60, 10));
            //At 4: 3 ticks of a tick every 2 with 8 extra each (at 6, 8 and 10)
            At(timeline, 4, () => meter.Replace(key, new ModInstance<int, int>(8, 2)));
            //At 5 the first of those is in progress with 1 left, and a tick every 10 isn't sooner, so it waits for that tick.
            //It lands at 6 with 8 (the same rate on the old schedule) + 8, and the 16 still owed had 4 more time to land in.
            //That is 1 tick of the new schedule, so the tick at 16 carries all 16.
            At(timeline, 5, () => meter.Replace(key, new ModInstance<int, int>(40, 10)));
            Dictionary<int, int> values = ValuesAt(timeline, meter, 6, 15, 16, 26);
            Card<int, string> end = At(timeline, 27);

            RunUntil(timeline, end);

            Assert.Equal(16, values[6]);
            Assert.Equal(16, values[15]); //Nothing lands sooner than it would have
            Assert.Equal(72, values[16]); //40 + 16
            Assert.Equal(112, values[26]);
            Assert.Equal((40, 0, 0), (meter.Derivative, meter.BlendPerTick, meter.BlendTicksLeft));
            Assert.Equal(Fraction(0), meter.CurrentSpilloverFraction);
            Assert.Equal(0, meter.MidAdvanceRateChanges);
        }
        [Fact(DisplayName = "If a tick is cancelled during a blend, what it had built up and what the blend still owes are spread together")]
        public void CancellingDuringABlendMergesThem()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -100000, 100000);
            ModifierKey key = meter.Add(new ModInstance<int, int>(60, 10));
            //At 4: 24 built up, due in 6, so 2 ticks of a tick every 4 with 12 extra each (at 8 and 12)
            At(timeline, 4, () => meter.Replace(key, new ModInstance<int, int>(16, 4)));
            //At 5 the first is a quarter done. A tick every 1 is sooner than the 3 it has left, so it is cancelled.
            //Owed: the 24 from before, and the 4 that a quarter of an ordinary 16 comes to. The blend ran until 12, which is 7 ticks away, so 4 extra each.
            (int perTick, int blend, int ticksLeft) at5 = default;
            At(timeline, 5, () =>
            {
                meter.Replace(key, new ModInstance<int, int>(5, 1));
                at5 = (meter.Derivative, meter.BlendPerTick, meter.BlendTicksLeft);
            });
            Dictionary<int, int> values = ValuesAt(timeline, meter, 6, 12, 13);
            Card<int, string> end = At(timeline, 14);

            RunUntil(timeline, end);

            Assert.Equal((9, 4, 7), at5);
            Assert.Equal(9, values[6]);
            Assert.Equal(63, values[12]); //All 28 has landed by 12 and not before, along with 7 ordinary ticks of 5
            Assert.Equal(68, values[13]);
            Assert.Equal(Fraction(0), meter.CurrentSpilloverFraction);
            Assert.Equal(0, meter.MidAdvanceRateChanges);
        }
    }
}
