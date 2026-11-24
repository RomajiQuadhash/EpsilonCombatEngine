using CoreMetrics;
using Meters;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Meters_Test
{
    /// <summary>
    /// Tests for a DiscreteReachThresholdEvent on a LinearDiscreteMeter when a tick jumps over the threshold instead of landing on it
    /// (97 -> 102 past a threshold of 100).
    /// Everything that matters here is run through the timeline's TakeSteps rather than by moving the meter directly,
    /// so that they show the timeline stops at the right time, fires the event there, and doesn't go past it.
    /// </summary>
    /// <remarks>Written by Claude. This isn't how I would write it.</remarks>
    public class DiscreteThresholdSkipOverTests
    {
        /// <summary>
        /// What happened during one or more calls to TakeSteps.
        /// </summary>
        private sealed class RunResult
        {
            /// <summary>Every event that occurred, in order</summary>
            public List<Okazo<int>> Occurred { get; } = [];
            /// <summary>How much time the timeline moved forward in total (rewinds subtract)</summary>
            public int TotalAdvance { get; set; }
            /// <summary>The largest single amount the timeline moved forward by</summary>
            public int LargestSingleAdvance { get; set; }
            /// <summary>The exception that stopped the timeline, if one did</summary>
            public Exception? Error { get; set; }
        }
        /// <summary>
        /// Runs the timeline the way a game would until it reaches Display or Terminated, noting what happened on the way.
        /// An exception stops the run and is kept in the result so the test can say what it expected instead.
        /// </summary>
        private static void RunToDisplay(Timeline<int> timeline, RunResult result)
        {
            try
            {
                foreach (StepReport<int> report in timeline.TakeSteps())
                {
                    result.TotalAdvance += report.AdvanceAmount;
                    result.LargestSingleAdvance = Math.Max(result.LargestSingleAdvance, report.AdvanceAmount);
                    if (report.OccurredEvent is not null)
                    {
                        result.Occurred.Add(report.OccurredEvent);
                    }
                }
            }
            catch (Exception e)
            {
                result.Error = e;
            }
        }
        /// <summary>
        /// Makes a meter that changes by perTick every tickRate, with a threshold event already on the timeline.
        /// </summary>
        private static (LinearDiscreteMeter<int, int> meter, DiscreteReachThresholdEvent<int, int> thresholdEvent) MeterWithThreshold(
            Timeline<int> timeline, int startValue, int perTick, int tickRate, int threshold)
        {
            LinearDiscreteMeter<int, int> meter = new(timeline, startValue, -1000, 1000);
            meter.SetDerivative(perTick, tickRate, out _);
            DiscreteReachThresholdEvent<int, int> thresholdEvent = new(meter, threshold, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
            timeline.AddEvent(thresholdEvent);
            return (meter, thresholdEvent);
        }

        #region Controls: these describe behaviour that already works
        [Fact(DisplayName = "Control: a tick that lands exactly on the threshold fires the event at that tick")]
        public void ExactLandingFiresAtTheTick()
        {
            Timeline<int> timeline = new();
            var (meter, thresholdEvent) = MeterWithThreshold(timeline, startValue: 95, perTick: 5, tickRate: 10, threshold: 100);
            RunResult result = new();

            RunToDisplay(timeline, result);

            Assert.Null(result.Error);
            Assert.Equal(10, result.TotalAdvance);
            Assert.Equal(100, meter.CurrentValue);
            Assert.Equal([thresholdEvent], result.Occurred);
        }
        [Fact(DisplayName = "Control: before the tick, the event predicts the first tick that gets past the threshold")]
        public void PredictionBeforeTheTickIsTheFirstTickPast()
        {
            Timeline<int> timeline = new();
            var (_, thresholdEvent) = MeterWithThreshold(timeline, startValue: 97, perTick: 5, tickRate: 10, threshold: 100);

            Assert.Equal(new TimeOrNever<int>(10), thresholdEvent.TimeRemaining);
        }
        #endregion

        #region Through the timeline
        [Theory(DisplayName = "A tick that jumps over the threshold fires the event at that tick")]
        [InlineData(97, 5, 100, 102)]   //Going up: 97 -> 102 past 100
        [InlineData(3, -5, 0, -2)]      //Going down: 3 -> -2 past 0
        [InlineData(99, 50, 100, 149)]  //Barely short, and a tick far bigger than the gap
        public void SkipOverFiresAtTheTick(int startValue, int perTick, int threshold, int valueAfterTick)
        {
            Timeline<int> timeline = new();
            var (meter, thresholdEvent) = MeterWithThreshold(timeline, startValue, perTick, tickRate: 10, threshold);
            RunResult result = new();

            RunToDisplay(timeline, result);

            Assert.Null(result.Error);
            //The timeline stopped at the tick that crossed, and nowhere else
            Assert.Equal(10, result.TotalAdvance);
            Assert.Equal(valueAfterTick, meter.CurrentValue);
            Assert.Equal([thresholdEvent], result.Occurred);
        }
        [Fact(DisplayName = "A threshold jumped over several ticks away fires at the tick that crosses, not an earlier or later one")]
        public void SkipOverSeveralTicksAway()
        {
            Timeline<int> timeline = new();
            //91 -> 96 -> 101: the second tick is the one that gets past 100
            var (meter, thresholdEvent) = MeterWithThreshold(timeline, startValue: 91, perTick: 5, tickRate: 10, threshold: 100);
            RunResult result = new();

            RunToDisplay(timeline, result);

            Assert.Null(result.Error);
            Assert.Equal(20, result.TotalAdvance);
            Assert.Equal(101, meter.CurrentValue);
            Assert.Equal([thresholdEvent], result.Occurred);
        }
        [Fact(DisplayName = "A threshold jumped over by a tick that was already partway done fires when that tick lands")]
        public void SkipOverWithATickAlreadyInProgress()
        {
            Timeline<int> timeline = new();
            LinearDiscreteMeter<int, int> meter = new(timeline, 97, -1000, 1000);
            meter.SetDerivative(5, 10, out _);
            meter.OnAdvance(timeline, 4); //4 of the 10 are done, so the tick is 6 away
            DiscreteReachThresholdEvent<int, int> thresholdEvent = new(meter, 100, PriorityRank.MeterLimit, timeline) { CouldOccur = true };
            timeline.AddEvent(thresholdEvent);
            RunResult result = new();

            RunToDisplay(timeline, result);

            Assert.Null(result.Error);
            Assert.Equal(6, result.TotalAdvance);
            Assert.Equal(102, meter.CurrentValue);
            Assert.Equal(0, meter.TimeSinceLastIncrement);
            Assert.Equal([thresholdEvent], result.Occurred);
        }
        [Fact(DisplayName = "The timeline stops for a jumped-over threshold instead of going on to a later event")]
        public void SkipOverIsNotPassedOnTheWayToALaterEvent()
        {
            Timeline<int> timeline = new();
            var (meter, thresholdEvent) = MeterWithThreshold(timeline, startValue: 97, perTick: 5, tickRate: 10, threshold: 100);
            Card<int, string> laterCard = new(35, timeline, PriorityRank.None);
            timeline.AddEvent(laterCard);
            //Note what the meter and the later card look like at the moment the threshold event occurs
            int? valueWhenThresholdOccurred = null;
            int? cardTimeWhenThresholdOccurred = null;
            thresholdEvent.Occurring += (_, _) =>
            {
                valueWhenThresholdOccurred = meter.CurrentValue;
                cardTimeWhenThresholdOccurred = laterCard.TimeToEvent;
            };
            RunResult result = new();

            RunToDisplay(timeline, result);

            Assert.Null(result.Error);
            //Only one tick has landed and the card is still 25 away, so the timeline stopped at 10 and not at 35
            Assert.Equal(102, valueWhenThresholdOccurred);
            Assert.Equal(25, cardTimeWhenThresholdOccurred);
            Assert.Equal(10, result.LargestSingleAdvance);
            Assert.Equal([thresholdEvent], result.Occurred);

            //Then the rest of the timeline carries on normally
            timeline.SetOpen();
            RunToDisplay(timeline, result);

            Assert.Null(result.Error);
            Assert.Equal(35, result.TotalAdvance);
            Assert.Equal(112, meter.CurrentValue); //Ticks at 10, 20 and 30
            Assert.Equal([thresholdEvent, laterCard], result.Occurred);
        }
        [Fact(DisplayName = "A jumped-over threshold fires once, and never moves the timeline backwards")]
        public void SkipOverDoesNotRewindOrRepeat()
        {
            Timeline<int> timeline = new();
            var (_, thresholdEvent) = MeterWithThreshold(timeline, startValue: 97, perTick: 5, tickRate: 10, threshold: 100);
            Card<int, string> laterCard = new(35, timeline, PriorityRank.None);
            timeline.AddEvent(laterCard);
            int timesOccurred = 0;
            thresholdEvent.Occurring += (_, _) => timesOccurred++;
            List<int> advances = [];
            RunResult result = new();

            try
            {
                for (int round = 0; round < 3 && timeline.Phase != TimelinePhase.Terminated; round++)
                {
                    foreach (StepReport<int> report in timeline.TakeSteps())
                    {
                        advances.Add(report.AdvanceAmount);
                    }
                    if (timeline.Phase == TimelinePhase.Display)
                    {
                        timeline.SetOpen();
                    }
                }
            }
            catch (Exception e)
            {
                result.Error = e;
            }

            Assert.Null(result.Error);
            Assert.Equal(1, timesOccurred);
            Assert.DoesNotContain(advances, advance => advance < 0);
        }
        #endregion

        #region The cause, without the timeline
        [Fact(DisplayName = "Right after a tick jumps over the threshold, the event's time remaining is zero")]
        public void TimeRemainingIsZeroRightAfterTheSkipOver()
        {
            Timeline<int> timeline = new();
            var (meter, thresholdEvent) = MeterWithThreshold(timeline, startValue: 97, perTick: 5, tickRate: 10, threshold: 100);

            meter.OnAdvance(timeline, 10); //The same advance the timeline would make

            Assert.Equal(102, meter.CurrentValue);
            Assert.Equal(new TimeOrNever<int>(0), thresholdEvent.TimeRemaining);
        }
        [Theory(DisplayName = "Looking back, the threshold was reached at the tick that jumped over it")]
        [InlineData(10, 0)]   //Just landed
        [InlineData(13, -3)]  //3 after it landed
        [InlineData(19, -9)]  //Just before the next tick
        public void ThresholdReachLookingBackIsTheTickThatCrossed(int timeAdvanced, int expectedTimeOfReach)
        {
            Timeline<int> timeline = new();
            LinearDiscreteMeter<int, int> meter = new(timeline, 97, -1000, 1000);
            meter.SetDerivative(5, 10, out _);

            meter.OnAdvance(timeline, timeAdvanced);

            Assert.Equal(102, meter.CurrentValue);
            //The tick at time 10 took it from 97 (short of 100) to 102 (past it), so that tick is when 100 was reached
            Assert.Equal(new TimeOrNever<int>(expectedTimeOfReach), meter.ThresholdReach(100, allowNegative: true));
        }
        #endregion
    }
}
