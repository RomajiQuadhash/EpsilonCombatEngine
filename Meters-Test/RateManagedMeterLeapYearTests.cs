using Meters;
using Rationalz;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Meters_Test
{
    /// <summary>
    /// Tests for the leap years of a RateManagedMeter and for changes that wait for a tick to land.
    /// All of them run through the timeline's TakeSteps, since the point is that the timeline stops where the meter needs it to.
    /// </summary>
    /// <remarks>From Claude, with slightly more helper functions than I would do</remarks>
    public class RateManagedMeterLeapYearTests
    {
        /// <summary>
        /// Runs the timeline the way a game would, reopening it after each Display, until the given event has occurred.
        /// </summary>
        /// <remarks>A MostRecentEventHolder listening to that event is what says it has occurred. The step reports are only used for the list that is returned.</remarks>
        /// <returns>Every event that occurred, in order</returns>
        private static List<Okazo<T>> RunUntil<T>(Timeline<T> timeline, Okazo<T> stopAfter) where T : INumber<T>
        {
            MostRecentEventHolder<T> reached = new();
            stopAfter.Occurring += reached.OnTrigger;
            List<Okazo<T>> occurred = [];
            for (int round = 0; round < 10000; round++)
            {
                if (timeline.Phase == TimelinePhase.Display)
                {
                    timeline.SetOpen();
                }
                foreach (StepReport<T> report in timeline.TakeSteps())
                {
                    if (report.OccurredEvent is not null)
                    {
                        occurred.Add(report.OccurredEvent);
                    }
                }
                if (reached.MostRecentEvent is not null || timeline.Phase == TimelinePhase.Terminated)
                {
                    return occurred;
                }
            }
            Assert.Fail("The timeline never reached the event it was run until.");
            return occurred;
        }
        /// <summary>
        /// A card that does nothing but mark a time for a test to run until or look at the meter at.
        /// </summary>
        private static Card<T, string> Marker<T>(Timeline<T> timeline, T time, Action? whenItOccurs = null) where T : INumber<T>
        {
            Card<T, string> marker = new(time, timeline, PriorityRank.None) { Face = $"Test marker at {time}" };
            if (whenItOccurs is not null)
            {
                marker.Occurring += (_, _) => whenItOccurs();
            }
            timeline.AddEvent(marker);
            return marker;
        }
        private static Rational<long> Fraction(long numerator, long denominator = 1) => new(numerator, denominator);

        [Fact(DisplayName = "A rate of 7/25 ticking every 5 adds exactly 7 over 25, using a leap year to make up what rounding lost")]
        public void LeapYearMakesUpWhatRoundingLost()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
            meter.Add(new ModInstance<int, int>(1, 5));  //The strongest, so the meter ticks every 5
            meter.Add(new ModInstance<int, int>(2, 25)); //Together 7/25, which is 1 and 2/5 per tick
            Assert.Equal((1, 5), (meter.Derivative, meter.DerivativeDenominator));

            (int value, LeapYearState leapYear) at10 = default, at15 = default, at20 = default;
            Marker(timeline, 10, () => at10 = (meter.CurrentValue, meter.CurrentLeapYear));
            Marker(timeline, 15, () => at15 = (meter.CurrentValue, meter.CurrentLeapYear));
            Marker(timeline, 20, () => at20 = (meter.CurrentValue, meter.CurrentLeapYear));
            Card<int, string> end = Marker(timeline, 25);

            RunUntil(timeline, end);

            //Two ordinary ticks, then the spillover gets to 1 during the third
            Assert.Equal((2, LeapYearState.None), at10);
            //The third tick lands as an ordinary one, and the leap year starts from there
            Assert.Equal((3, LeapYearState.Positive), at15);
            Assert.Equal((5, LeapYearState.Positive), at20);
            //Two leap ticks pay it all back, so the leap year is over
            Assert.Equal(7, meter.CurrentValue);
            Assert.Equal(Fraction(0), meter.CurrentSpilloverFraction);
            Assert.Equal(LeapYearState.None, meter.CurrentLeapYear);
            Assert.Equal(1, meter.Derivative);
            Assert.Equal(0, meter.MidAdvanceRateChanges);
        }
        [Fact(DisplayName = "A falling rate uses a negative leap year the same way")]
        public void NegativeLeapYearMirrorsPositive()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
            meter.Add(new ModInstance<int, int>(-1, 5));
            meter.Add(new ModInstance<int, int>(-2, 25));

            (int value, LeapYearState leapYear) at15 = default;
            Marker(timeline, 15, () => at15 = (meter.CurrentValue, meter.CurrentLeapYear));
            Card<int, string> end = Marker(timeline, 25);

            RunUntil(timeline, end);

            Assert.Equal((-3, LeapYearState.Negative), at15);
            Assert.Equal(-7, meter.CurrentValue);
            Assert.Equal(Fraction(0), meter.CurrentSpilloverFraction);
            Assert.Equal(LeapYearState.None, meter.CurrentLeapYear);
            Assert.Equal(0, meter.MidAdvanceRateChanges);
        }
        [Fact(DisplayName = "Over a long run the meter plus its spillover is exactly what the rate earned, and the spillover stays small")]
        public void LongRunLosesNothing()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -100000, 100000);
            meter.Add(new ModInstance<int, int>(7, 3)); //Ticks every 3
            meter.Add(new ModInstance<int, int>(1, 4)); //Together 31/12, which is 7 and 3/4 per tick
            Rational<long> largestSpillover = Fraction(0);
            for (int time = 3; time < 600; time += 3)
            {
                Marker(timeline, time, () =>
                {
                    Rational<long> spilloverNow = Rational<long>.Abs(meter.CurrentSpilloverFraction);
                    if (spilloverNow > largestSpillover)
                    {
                        largestSpillover = spilloverNow;
                    }
                });
            }
            Card<int, string> end = Marker(timeline, 600);

            RunUntil(timeline, end);

            //600 is a tick, so nothing is partway built up and the two should add up exactly
            Assert.Equal(0, meter.TimeSinceLastIncrement);
            Assert.Equal(Fraction(31 * 600, 12), Fraction(meter.CurrentValue) + meter.CurrentSpilloverFraction);
            Assert.True(largestSpillover < Fraction(2), $"Spillover got as large as {largestSpillover}");
            Assert.Equal(0, meter.MidAdvanceRateChanges);
        }
        [Fact(DisplayName = "A rate that needs no rounding never puts anything on the timeline")]
        public void ExactRateStaysQuiet()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -1000, 1000);
            meter.Add(new ModInstance<int, int>(5, 2));
            Card<int, string> end = Marker(timeline, 20);

            List<Okazo<int>> occurred = RunUntil(timeline, end);

            Assert.Equal([end], occurred);
            Assert.Equal(50, meter.CurrentValue);
            Assert.Equal(LeapYearState.None, meter.CurrentLeapYear);
        }
        [Fact(DisplayName = "A new tick rate that has to wait stops the timeline at the tick it is waiting for")]
        public void WaitingRateChangeStopsTheTimelineAtTheTick()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -100000, 100000);
            ModifierKey key = meter.Add(new ModInstance<int, int>(50, 10));
            //At 4 the tick has 6 left, and a tick rate of 7 isn't sooner than that, so it waits
            Card<int, string> change = Marker(timeline, 4, () => meter.Replace(key, new ModInstance<int, int>(70, 7)));
            (int value, int tickRate) at10 = default;
            Marker(timeline, 10, () => at10 = (meter.CurrentValue, meter.DerivativeDenominator));
            Card<int, string> end = Marker(timeline, 30);

            List<Okazo<int>> occurred = RunUntil(timeline, end);

            //The tick in progress landed at 10 with the new rate's amount for the old schedule, and the new schedule started there
            Assert.Equal((100, 7), at10);
            //Then ticks of 70 at 17 and 24
            Assert.Equal(240, meter.CurrentValue);
            Assert.Equal(6, meter.TimeSinceLastIncrement);
            Assert.False(meter.IsRateChangePending);
            //The timeline stopped at the tick by itself instead of the meter having to split an advance
            Assert.Equal(0, meter.MidAdvanceRateChanges);
            Assert.Equal(4, occurred.Count); //The change, the meter's own card at the tick, and the markers at 10 and 30
            Assert.Same(change, occurred[0]);
            Assert.Same(meter, Assert.IsType<Card<int, RateManagedMeter<int, int>>>(occurred[1]).Face);
        }
        [Fact(DisplayName = "When the modifiers go away mid-tick, the tick in progress lands once and then the meter stops")]
        public void LastTickLandsThenTheMeterStops()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -100000, 100000);
            ModifierKey key = meter.Add(new ModInstance<int, int>(50, 10));
            Marker(timeline, 4, () => meter.Remove(key));
            Card<int, string> end = Marker(timeline, 30);

            RunUntil(timeline, end);

            Assert.Equal(50, meter.CurrentValue);
            Assert.Equal(0, meter.Derivative);
            Assert.False(meter.IsRateChangePending);
            Assert.Equal(Fraction(0), meter.CurrentSpilloverFraction);
            Assert.Equal(0, meter.MidAdvanceRateChanges);
        }
        [Fact(DisplayName = "What a cancelled tick had built up goes into a blend, not a leap year, when it divides evenly")]
        public void CancelledTickDoesNotStartALeapYearWhenItBlendsEvenly()
        {
            Timeline<int> timeline = new();
            RateManagedMeter<int, int> meter = new(timeline, 0, -100000, 100000);
            ModifierKey key = meter.Add(new ModInstance<int, int>(70, 7));
            //At 4 the tick has 3 left, and a tick rate of 2 is sooner than that, so the tick is cancelled with 40 built up.
            //3 time at a tick every 2 is 2 ticks, so each of those carries 20 extra.
            Marker(timeline, 4, () => meter.Replace(key, new ModInstance<int, int>(8, 2)));
            (int value, LeapYearState leapYear, int perTick, Rational<long> spillover) at5 = default;
            Marker(timeline, 5, () => at5 = (meter.CurrentValue, meter.CurrentLeapYear, meter.Derivative, meter.CurrentSpilloverFraction));
            Card<int, string> end = Marker(timeline, 10);

            RunUntil(timeline, end);

            //Nothing has landed yet at 5, and none of it had to go to the spillover
            Assert.Equal((0, LeapYearState.None, 28, Fraction(0)), at5);
            //Ticks of 28 at 6 and 8, then an ordinary 8 at 10
            Assert.Equal(64, meter.CurrentValue);
            Assert.Equal(Fraction(0), meter.CurrentSpilloverFraction);
            Assert.Equal(0, meter.MidAdvanceRateChanges);
        }
        [Fact(DisplayName = "On a floating point timeline a leap year can end partway through a tick, and nothing is lost")]
        public void FloatingPointTimelineLosesNothing()
        {
            Timeline<double> timeline = new();
            RateManagedMeter<double, int> meter = new(timeline, 0, -100000, 100000);
            meter.Add(new ModInstance<double, int>(1, 0.5)); //Ticks every half
            meter.Add(new ModInstance<double, int>(1, 4));   //Together 9/4, which is 1 and 1/8 per tick
            Card<double, string> end = Marker(timeline, 8.0);

            List<Okazo<double>> occurred = RunUntil(timeline, end);

            Assert.Equal(0.0, meter.TimeSinceLastIncrement);
            Assert.Equal(Fraction(18), Fraction(meter.CurrentValue) + meter.CurrentSpilloverFraction);
            Assert.True(Rational<long>.Abs(meter.CurrentSpilloverFraction) < Fraction(2), $"Spillover ended at {meter.CurrentSpilloverFraction}");
            Assert.Equal(0, meter.MidAdvanceRateChanges);
            Assert.True(occurred.Count > 1, "Expected leap year events to occur along the way");
        }
    }
}
