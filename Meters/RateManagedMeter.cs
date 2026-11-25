using CoreMetrics;
using Rationalz;
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
    /// A discrete meter whose rate of change is worked out from any number of anonymous sources (ModInstances).
    /// The sources are summed, and the strongest one decides how often the meter ticks.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for this meter's value. Integer</typeparam>
    public class RateManagedMeter<T, M> : LinearDiscreteMeter<T,M> where T : INumber<T> where M : IBinaryInteger<M>
    {
        #region Modifier storage
        /// <summary>
        /// The bookkeeping for the modifiers that Add, Remove and Replace work on: which ones there are, their exact total, and which is the strongest.
        /// </summary>
        private readonly ModifierGroup<T, M> _sources;
        /// <summary>
        /// The same group that Add, Remove and Replace work on, for a subclass that has more groups of its own
        /// and needs this one's total and strongest modifier to put together with theirs.
        /// </summary>
        protected ModifierGroup<T, M> Sources => _sources;
        /// <summary>
        /// The modifiers that are currently affecting this RateManagedMeter.
        /// Keys are unique identifiers given by the RateManagedMeter to the caller when they add a modifier, and can be used to remove or replace the modifier later.
        /// </summary>
        public IReadOnlyDictionary<ModifierKey, ModInstance<T, M>> Modifiers => _sources.Modifiers;
        /// <summary>
        /// The change per unit of time that this meter is asked for in total. The ticks approximate this.
        /// </summary>
        /// <remarks>
        /// A subclass with more than one group of modifiers overrides this to say how their totals go together.
        /// It has to call ApplyRateChange whenever the answer changes for a reason other than Add, Remove or Replace.
        /// </remarks>
        public virtual Rational<long> TrueRate => _sources.Sum;
        /// <summary>
        /// How often the strongest source of change wants to tick, which is how often this meter ticks unless a tick that often would round down to nothing.
        /// Only used if TrueRate isn't 0.
        /// </summary>
        /// <remarks>A subclass with more than one group of modifiers overrides this along with TrueRate, to say which group's strongest modifier is the strongest overall.</remarks>
        protected virtual T PaceTickRate => _sources.FastestTickRateAtMax;
        #endregion
        #region Rate change state
        /// <summary>
        /// True while the tick in progress has to land before the derivative and tick rate are worked out again.
        /// </summary>
        public bool IsRateChangePending { get; private set; }
        /// <summary>
        /// How many times a waiting change had to be applied partway through an advance, since the timeline moved past the tick it was waiting for.
        /// Should stay 0, since a card stops the timeline at that tick.
        /// </summary>
        public int MidAdvanceRateChanges { get; private set; }
        /// <summary>
        /// The leap year the tick in progress is in. A leap year adds or takes one more per tick to pay back the spillover.
        /// </summary>
        public LeapYearState CurrentLeapYear { get; private set; }
        /// <summary>
        /// The leap year that ticks will be in once the tick in progress lands. This is what the leap year events set.
        /// </summary>
        public LeapYearState UpcomingLeapYear { get; private set; }
        /// <summary>
        /// How much extra each tick is adding to pay out what a cancelled tick had built up. 0 if no blend is running.
        /// This is part of Derivative, but isn't counted against the spillover since it was never put there.
        /// </summary>
        public M BlendPerTick { get; private set; } = M.Zero;
        /// <summary>
        /// How many more ticks, counting the one in progress, will add BlendPerTick.
        /// </summary>
        public M BlendTicksLeft { get; private set; } = M.Zero;
        /// <summary>
        /// True while the meter is paused: time passing doesn't move it, and it answers threshold questions as a meter that isn't moving.
        /// Its modifiers, its place in the tick in progress, its leap year and its blend are all kept for when it is resumed.
        /// </summary>
        public bool IsPaused { get; private set; }
        /// <summary>
        /// The spillover's rate from before the pause, which it gets back when the meter is resumed. Only meaningful while paused.
        /// </summary>
        private Rational<long> _spilloverRateBeforePause = Rational<long>.Zero;
        /// <summary>
        /// True if something that should change the rate happened while paused. The change is made when the meter is resumed.
        /// </summary>
        private bool _rateChangedWhilePaused;
        /// <summary>
        /// A paused meter isn't moving, whatever its derivative says.
        /// </summary>
        protected override bool IsStill => IsPaused || base.IsStill;
        /// <summary>
        /// True if a blend still has ticks to pay out on.
        /// </summary>
        private bool IsBlendRunning => !M.IsZero(BlendPerTick) && BlendTicksLeft > M.Zero;
        /// <summary>
        /// True while anything is waiting for the tick in progress to land: a new tick rate, the meter stopping, a leap year starting or ending,
        /// or a blend whose last tick has landed but is still part of Derivative.
        /// </summary>
        private bool IsWaitingForTick => IsRateChangePending || UpcomingLeapYear != CurrentLeapYear || (!M.IsZero(BlendPerTick) && !IsBlendRunning);
        /// <summary>
        /// True if a tick has started and not landed yet. If not, changes can just take effect.
        /// </summary>
        private bool IsTickInProgress => !M.IsZero(Derivative) && !T.IsZero(TimeSinceLastIncrement);
        /// <summary>
        /// A meter that tracks value that has been earned but not added to this meter yet:
        /// what rounding each tick down has lost, plus whatever of a cancelled tick didn't divide evenly into a blend.
        /// Its derivative is the true rate minus the rate the ticks are actually applying, so it keeps itself up to date as time advances.
        /// </summary>
        /// <remarks>Created after this meter subscribes to the timeline, so on every advance this meter moves first and the spillover second.</remarks>
        private readonly LinearContinuousMeter<T, Rational<long>> _spilloverFraction;
        /// <summary>
        /// Value that has been earned but not added to the meter yet. Negative if the meter is ahead of what it has earned.
        /// </summary>
        public Rational<long> CurrentSpilloverFraction => _spilloverFraction.CurrentValue;
        /// <summary>
        /// Far more than the spillover should ever hold, but small enough that comparing against it can't overflow.
        /// </summary>
        private const long SpilloverLimit = 1_000_000_000;
        #endregion
        #region Events on the timeline
        //Every one of these is on the timeline for as long as this meter refers to it, and is forgotten when it occurs.
        //They tell this meter they occurred through Occurring, so the timeline's own cloning and rewiring covers them.
        /// <summary>
        /// Occurs when the spillover rises to +1. Exists unless a positive leap year is running or about to.
        /// </summary>
        private ReachThresholdEvent<T, Rational<long>>? _positiveLeapYearStart;
        /// <summary>
        /// Occurs when the spillover falls to -1. Exists unless a negative leap year is running or about to.
        /// </summary>
        private ReachThresholdEvent<T, Rational<long>>? _negativeLeapYearStart;
        /// <summary>
        /// Occurs when the spillover gets back to 0. Exists only while a leap year is running or about to.
        /// </summary>
        private ReachThresholdEvent<T, Rational<long>>? _leapYearEnd;
        /// <summary>
        /// Stops the timeline at the tick that something is waiting for, or at the last tick of a blend. Its face is this meter, so it is clear in a debugger why it is there.
        /// </summary>
        private Card<T, RateManagedMeter<T, M>>? _tickCard;
        #endregion

        /// <summary>
        /// Creates a meter with nothing changing it yet.
        /// </summary>
        /// <param name="owningTimeline">The timeline that owns this meter</param>
        /// <param name="initialValue">The value that the meter starts at</param>
        /// <param name="minValue">Minimum of the meter</param>
        /// <param name="maxValue">Maximum of the meter</param>
        /// <param name="groupCounter">
        /// What numbers this meter's group of modifiers, so its keys can't be mistaken for those of another group made with the same counter.
        /// Leave it out if nothing needs telling apart, and the keys are in group 0.
        /// </param>
        public RateManagedMeter(Timeline<T> owningTimeline,M initialValue, M minValue, M maxValue, ModifierGroupCounter? groupCounter = null):base(owningTimeline,initialValue,minValue,maxValue)
        {
            _sources = groupCounter is null ? new() : new(groupCounter);
            _spilloverFraction = new(owningTimeline, Rational<long>.Zero, new Rational<long>(-SpilloverLimit), new Rational<long>(SpilloverLimit));
            PutLeapYearEventsOnTimeline();
        }
        /// <summary>
        /// Clone constructor: for a meter whose spillover meter and group of modifiers already exist.
        /// Unlike the normal constructor, it puts no leap year events on the timeline. Clone fills in everything else.
        /// </summary>
        /// <param name="owningTimeline">The timeline that owns this meter</param>
        /// <param name="initialValue">The value that the meter starts at</param>
        /// <param name="minValue">Minimum of the meter</param>
        /// <param name="maxValue">Maximum of the meter</param>
        /// <param name="spilloverFraction">The meter to use for the spillover. Must be on the same timeline</param>
        /// <param name="sources">The group of modifiers that Add, Remove and Replace work on</param>
        protected RateManagedMeter(Timeline<T> owningTimeline, M initialValue, M minValue, M maxValue, LinearContinuousMeter<T, Rational<long>> spilloverFraction, ModifierGroup<T, M> sources)
            : base(owningTimeline, initialValue, minValue, maxValue)
        {
            _spilloverFraction = spilloverFraction;
            _sources = sources;
        }

        #region Changing modifiers
        /// <summary>
        /// Adds a source of change to this meter.
        /// </summary>
        /// <param name="modifier">The source to add</param>
        /// <returns>The key to use to replace or remove this source later. Keep it, since the modifier itself is anonymous</returns>
        /// <exception cref="OverflowException">If the total of all the modifiers can't be stored exactly. Nothing is changed in that case</exception>
        /// <remarks>Virtual so that a subclass can refuse sources while it is in a state where nothing should be changing it.</remarks>
        public virtual ModifierKey Add(ModInstance<T, M> modifier)
        {
            ModifierKey key = _sources.Add(modifier); //If this throws, nothing has been changed
            ApplyRateChange();
            return key;
        }
        /// <summary>
        /// Removes a source of change from this meter.
        /// </summary>
        /// <param name="key">The key Add gave for the source</param>
        /// <returns>True if it was removed, false if this meter has no source with that key (in which case nothing changes)</returns>
        public bool Remove(ModifierKey key)
        {
            if (!_sources.Remove(key))
            {
                return false;
            }
            ApplyRateChange();
            return true;
        }
        /// <summary>
        /// Swaps a source of change for a different one, keeping the same key. Use this when the effect behind a source changes strength.
        /// </summary>
        /// <param name="key">The key Add gave for the source</param>
        /// <param name="modifier">What to replace it with</param>
        /// <returns>True if it was replaced, false if this meter has no source with that key (in which case nothing changes)</returns>
        /// <exception cref="OverflowException">If the total of all the modifiers can't be stored exactly. Nothing is changed in that case</exception>
        public bool Replace(ModifierKey key, ModInstance<T, M> modifier)
        {
            if (!_sources.Replace(key, modifier)) //If this throws, nothing has been changed
            {
                return false;
            }
            ApplyRateChange();
            return true;
        }
        /// <summary>
        /// The rate of this meter comes from its modifiers, so it can't be set directly.
        /// </summary>
        /// <exception cref="NotSupportedException">Always. Add, Remove or Replace a modifier instead</exception>
        public override bool SetDerivative(M newDerivative, T newDDenominator, out Rational<long> spillover)
        {
            throw new NotSupportedException($"A {nameof(RateManagedMeter<T, M>)} works out its own derivative. Add, Remove or Replace a modifier instead.");
        }
        #endregion
        #region Working out and applying the rate
        /// <summary>
        /// The parts of the schedule that only this kind of meter has, kept together so that working out "what would happen" can carry them along.
        /// </summary>
        /// <param name="LeapYear">The leap year the ticks are in</param>
        /// <param name="BlendPerTick">How much extra each tick adds for a blend</param>
        /// <param name="BlendTicksLeft">How many more ticks add that extra</param>
        private readonly record struct Plan(LeapYearState LeapYear, M BlendPerTick, M BlendTicksLeft);
        private Plan CurrentPlan => new(CurrentLeapYear, BlendPerTick, BlendTicksLeft);
        /// <summary>
        /// Works out what the modifiers alone ask for, with no leap year or blend.
        /// </summary>
        /// <param name="tickRateIfStopped">The tick rate to report if the modifiers ask for nothing</param>
        /// <param name="perTick">How much each tick should add. 0 if the modifiers cancel out or there are none</param>
        /// <param name="tickRate">How often it should tick</param>
        /// <returns>True if the modifiers ask for any change at all</returns>
        private bool ComputeBaseRate(T tickRateIfStopped, out M perTick, out T tickRate)
        {
            Rational<long> trueRate = TrueRate; //With no modifiers this is 0 too
            if (trueRate.Numerator == 0)
            {
                perTick = M.Zero;
                tickRate = tickRateIfStopped;
                return false;
            }
            //The strongest modifier sets the pace, but never so fast that a tick would round down to nothing.
            Rational<long> smallestUsefulTickRate = Rational<long>.Abs(Rational<long>.One / trueRate);
            tickRate = RationalConversion.FromRational<T>(smallestUsefulTickRate, FractionRounding.AtLeast);
            T pace = PaceTickRate;
            if (pace > tickRate)
            {
                tickRate = pace;
            }
            perTick = TruncatedPerTick(tickRate);
            if (M.IsZero(perTick))
            {
                //If there's a change, the derivative should never be 0
                perTick = trueRate.Numerator < 0 ? -M.One : M.One;
            }
            return true;
        }
        /// <summary>
        /// How much a tick adds at the given tick rate, dropping any fraction (so toward zero). Doesn't include any leap year or blend.
        /// </summary>
        private M TruncatedPerTick(T tickRate)
        {
            Rational<Int128> exact = RationalConversion.Widen(RationalConversion.ToRational(tickRate)) * RationalConversion.Widen(TrueRate);
            return M.CreateChecked(exact.Numerator / exact.Denominator);
        }
        /// <summary>
        /// Adds a leap year's extra one to what a tick adds.
        /// </summary>
        private static M WithLeapYear(M perTick, LeapYearState leapYear)
        {
            switch (leapYear)
            {
                case LeapYearState.Positive:
                    perTick += M.One;
                    //If there's a change, the derivative should never be 0, so bump over the boundary
                    return M.IsZero(perTick) ? M.One : perTick;
                case LeapYearState.Negative:
                    perTick -= M.One;
                    return M.IsZero(perTick) ? -M.One : perTick;
                default:
                    return perTick;
            }
        }
        /// <summary>
        /// Puts together everything a tick adds: what the modifiers ask for, the leap year's extra one, and a blend's share.
        /// </summary>
        /// <param name="hasRate">False if the modifiers ask for nothing. Then there is no leap year either, and only a blend is left to pay out</param>
        private static M WithExtras(M basePerTick, bool hasRate, LeapYearState leapYear, M blendPerTick)
        {
            //A leap year does nothing while the modifiers ask for nothing. It picks up again when there is something to tick.
            M perTick = hasRate ? WithLeapYear(basePerTick, leapYear) : M.Zero;
            if (M.IsZero(blendPerTick))
            {
                return perTick;
            }
            perTick += blendPerTick;
            if (hasRate && M.IsZero(perTick))
            {
                //Same rule as the leap year: never 0 while there's a change, so bump the way the blend was going
                return M.IsNegative(blendPerTick) ? -M.One : M.One;
            }
            return perTick;
        }
        /// <summary>
        /// Called after the modifiers change. Decides whether the new rate starts now, replaces the tick in progress, or waits for it.
        /// </summary>
        /// <remarks>A subclass that overrides TrueRate or PaceTickRate calls this whenever what they give has changed.</remarks>
        protected void ApplyRateChange()
        {
            if (IsPaused)
            {
                //The schedule is frozen. Whatever the rate is by then takes effect when the meter is resumed.
                _rateChangedWhilePaused = true;
                return;
            }
            if (!IsTickInProgress)
            {
                //Nothing has built up, so the new rate can just start.
                StartNewTick();
                return;
            }
            bool hasRate = ComputeBaseRate(DerivativeDenominator, out M basePerTick, out T targetTickRate);
            if (!hasRate)
            {
                //The tick in progress still lands with what it was going to add, and then the meter stops (after paying out any blend).
                //The spillover keeps its rate until then too, so that last tick is treated like any other.
                IsRateChangePending = true;
                UpdateTickCard();
                return;
            }
            if (targetTickRate == DerivativeDenominator)
            {
                //Same schedule, so the tick in progress just lands with the new amount. It stays in the leap year and blend it started in.
                SetDerivativeKeepingPhase(WithExtras(basePerTick, true, CurrentLeapYear, BlendPerTick));
                IsRateChangePending = false;
                UpdateSpilloverRate();
                UpdateTickCard();
                return;
            }
            M perTickOnOldSchedule = TruncatedPerTick(DerivativeDenominator);
            if (targetTickRate < TimeUntilTick(M.One) || M.IsZero(perTickOnOldSchedule))
            {
                //A tick on the new schedule lands before the one in progress would have (or the one in progress would add nothing),
                //so the tick in progress is cancelled and what it had built up is blended into the next ticks.
                StartNewTick();
                return;
            }
            //Otherwise the tick in progress lands on its old schedule with the new amount, and then the new schedule starts.
            SetDerivativeKeepingPhase(WithExtras(perTickOnOldSchedule, true, CurrentLeapYear, BlendPerTick));
            IsRateChangePending = true;
            UpdateSpilloverRate();
            UpdateTickCard();
        }
        /// <summary>
        /// Starts a new tick from now with whatever the modifiers and the upcoming leap year ask for, so nothing is left waiting.
        /// If a tick was in progress it is cancelled, and what it had built up is blended into the next ticks.
        /// </summary>
        private void StartNewTick()
        {
            TickState now = CurrentTickState;
            Plan plan;
            Rational<long> owed;
            TickState next = IsTickInProgress
                ? NewTickCancelling(now, CurrentPlan, out plan, out owed)
                : NewTickAtBoundary(now, CurrentPlan, out plan, out owed);
            base.SetDerivative(next.Derivative, next.DerivativeDenominator, out _); //What was built up has already been counted above
            CurrentLeapYear = plan.LeapYear;
            BlendPerTick = plan.BlendPerTick;
            BlendTicksLeft = plan.BlendTicksLeft;
            IsRateChangePending = false;
            UpdateSpilloverRate();
            UpdateTickCard();
            //Last, since the leap year events can react to this straight away.
            _spilloverFraction.AddInstantChange(owed);
        }
        /// <summary>
        /// Works out the tick that starts when the one before it has just landed (or when nothing was ticking). Changes nothing.
        /// The upcoming leap year takes over, a blend that has run out is dropped, and a blend that is still running is spread again if the tick rate changes.
        /// </summary>
        /// <param name="atBoundary">The meter with no tick in progress</param>
        /// <param name="plan">The leap year and blend before this</param>
        /// <param name="newPlan">The leap year and blend from this tick on</param>
        /// <param name="owed">What has to go into the spillover because it didn't divide evenly into the blend</param>
        private TickState NewTickAtBoundary(TickState atBoundary, Plan plan, out Plan newPlan, out Rational<long> owed)
        {
            bool hasRate = ComputeBaseRate(atBoundary.DerivativeDenominator, out M basePerTick, out T tickRate);
            M blendPerTick = plan.BlendPerTick;
            M blendTicksLeft = plan.BlendTicksLeft;
            owed = Rational<long>.Zero;
            if (M.IsZero(blendPerTick) || blendTicksLeft <= M.Zero)
            {
                blendPerTick = M.Zero;
                blendTicksLeft = M.Zero;
            }
            else if (tickRate != atBoundary.DerivativeDenominator)
            {
                //The ticks left were counted on the old schedule. Spread what they still owe over the same length of time on the new one.
                Rational<Int128> stillOwed = new(Int128.CreateChecked(blendPerTick) * Int128.CreateChecked(blendTicksLeft), Int128.One);
                T timeItHad = T.CreateChecked(blendTicksLeft) * atBoundary.DerivativeDenominator;
                SpreadOverTicks(stillOwed, timeItHad, tickRate, out blendPerTick, out blendTicksLeft, out owed);
            }
            newPlan = new(UpcomingLeapYear, blendPerTick, blendTicksLeft);
            return atBoundary with
            {
                Derivative = WithExtras(basePerTick, hasRate, UpcomingLeapYear, blendPerTick),
                DerivativeDenominator = tickRate,
                TimeSinceLastIncrement = T.Zero
            };
        }
        /// <summary>
        /// Works out the tick that starts when the one in progress is cancelled. Changes nothing.
        /// What the cancelled tick had built up, plus anything a running blend still owed, is spread over the new ticks
        /// so that none of it lands sooner than it would have.
        /// </summary>
        /// <param name="now">The meter with a tick in progress</param>
        /// <param name="plan">The leap year and blend before this</param>
        /// <param name="newPlan">The leap year and blend from this tick on</param>
        /// <param name="owed">What has to go into the spillover because it didn't divide evenly into the blend</param>
        private TickState NewTickCancelling(TickState now, Plan plan, out Plan newPlan, out Rational<long> owed)
        {
            bool hasRate = ComputeBaseRate(now.DerivativeDenominator, out M basePerTick, out T tickRate);
            Rational<Int128> phase = RationalConversion.Widen(RationalConversion.ToRational(now.TimeSinceLastIncrement))
                / RationalConversion.Widen(RationalConversion.ToRational(now.DerivativeDenominator));
            //What the cancelled tick had built up, which it would have delivered when it landed
            Rational<Int128> total = RationalConversion.Widen(RationalConversion.ToRational(now.Derivative)) * phase;
            T latestItWouldHaveLanded = TimeUntilTick(M.One, now);
            if (!M.IsZero(plan.BlendPerTick) && plan.BlendTicksLeft > M.Zero)
            {
                //A blend was still running. All of its ticks left are owed, but the built up part of this tick's share was already counted above.
                Rational<Int128> share = RationalConversion.Widen(RationalConversion.ToRational(plan.BlendPerTick));
                Rational<Int128> ticksLeft = new(Int128.CreateChecked(plan.BlendTicksLeft), Int128.One);
                total = total + share * ticksLeft - share * phase;
                latestItWouldHaveLanded = TimeUntilTick(plan.BlendTicksLeft, now);
            }
            SpreadOverTicks(total, latestItWouldHaveLanded, tickRate, out M blendPerTick, out M blendTicksLeft, out owed);
            newPlan = new(UpcomingLeapYear, blendPerTick, blendTicksLeft);
            return now with
            {
                Derivative = WithExtras(basePerTick, hasRate, UpcomingLeapYear, blendPerTick),
                DerivativeDenominator = tickRate,
                TimeSinceLastIncrement = T.Zero
            };
        }
        /// <summary>
        /// Splits an amount evenly over however many ticks it takes to reach the time it would have been delivered by.
        /// </summary>
        /// <param name="total">The amount to deliver</param>
        /// <param name="deliveredBy">How long from now the last of it would have been delivered</param>
        /// <param name="tickRate">How much time is between the ticks it is being spread over</param>
        /// <param name="perTick">The whole number each tick adds. 0 if the amount is too small to give every tick at least 1</param>
        /// <param name="ticks">How many ticks add it. 0 if perTick is 0</param>
        /// <param name="leftover">What is left of the amount after those ticks, for the spillover and its leap years to deliver</param>
        private static void SpreadOverTicks(Rational<Int128> total, T deliveredBy, T tickRate, out M perTick, out M ticks, out Rational<long> leftover)
        {
            Rational<Int128> ticksNeeded = RationalConversion.Widen(RationalConversion.ToRational(deliveredBy))
                / RationalConversion.Widen(RationalConversion.ToRational(tickRate));
            //Rounded up, so the last tick is at or after the time it would have been delivered by
            Int128 count = (ticksNeeded.Numerator + ticksNeeded.Denominator - Int128.One) / ticksNeeded.Denominator;
            if (count < Int128.One)
            {
                count = Int128.One;
            }
            Int128 share = total.Numerator / (total.Denominator * count); //Dropping any fraction, toward zero
            if (share == Int128.Zero)
            {
                perTick = M.Zero;
                ticks = M.Zero;
                leftover = RationalConversion.LimitDenominator(total);
                return;
            }
            perTick = M.CreateChecked(share);
            ticks = M.CreateChecked(count);
            leftover = RationalConversion.LimitDenominator(total - new Rational<Int128>(share * count, Int128.One));
        }
        /// <summary>
        /// The rate the spillover should change at for a schedule: what the modifiers ask for minus what the ticks apply, not counting a blend's share.
        /// </summary>
        private Rational<long> SpilloverRateFor(TickState state, Plan plan)
        {
            if (M.IsZero(state.Derivative))
            {
                //Nothing is ticking, so nothing is being lost to rounding
                return Rational<long>.Zero;
            }
            Rational<Int128> appliedRate = RationalConversion.Widen(RationalConversion.ToRational(state.Derivative - plan.BlendPerTick))
                / RationalConversion.Widen(RationalConversion.ToRational(state.DerivativeDenominator));
            return RationalConversion.LimitDenominator(RationalConversion.Widen(TrueRate) - appliedRate);
        }
        /// <summary>
        /// Points the spillover at the gap between what the modifiers ask for and what the ticks are applying.
        /// </summary>
        private void UpdateSpilloverRate()
        {
            _spilloverFraction.SetDerivative(SpilloverRateFor(CurrentTickState, CurrentPlan));
        }
        /// <summary>
        /// Works out what the meter would be after the time provided. Changes nothing.
        /// </summary>
        protected override TickState AdvanceTicks(T timeElapsed, TickState start)
        {
            if (IsPaused)
            {
                return start;
            }
            return Walk(timeElapsed, start, out _, out _, out _, out _);
        }
        /// <summary>
        /// Makes the result of AdvanceTicks real, including the parts only this kind of meter has.
        /// </summary>
        protected override void CommitTickState(T timeElapsed, TickState start, TickState end)
        {
            if (IsPaused)
            {
                return; //Nothing moved, so there is nothing to make real
            }
            //Has to be done before anything is committed, since it is the same working out that AdvanceTicks did.
            Walk(timeElapsed, start, out Plan plan, out bool startedNewTick, out Rational<long> spilloverOwed, out int midAdvanceChanges);
            base.CommitTickState(timeElapsed, start, end);
            BlendPerTick = plan.BlendPerTick;
            BlendTicksLeft = plan.BlendTicksLeft;
            if (!startedNewTick)
            {
                return;
            }
            IsRateChangePending = false;
            CurrentLeapYear = plan.LeapYear;
            UpdateSpilloverRate();
            MidAdvanceRateChanges += midAdvanceChanges;
            _spilloverFraction.AddInstantChange(spilloverOwed);
        }
        /// <summary>
        /// Works out what the meter would be after the time provided, along with everything else that would change on the way. Changes nothing.
        /// If this time reaches past a tick that something is waiting for, or past the last tick of a blend,
        /// the time up to that tick uses the schedule from before it and the rest uses the one from after it.
        /// </summary>
        /// <remarks>The tick card stops the timeline at those ticks, so going past one only happens if something else moves this meter.</remarks>
        /// <param name="plan">The leap year and blend at the end</param>
        /// <param name="startedNewTick">True if the schedule changed at any point</param>
        /// <param name="spilloverOwed">
        /// What has to be added to the spillover if this is made real: anything that didn't divide evenly into a blend,
        /// and the difference for any time spent at a different spillover rate than the one at the end.
        /// (The spillover advances after this meter, for the whole time, at the rate it has by then.)
        /// </param>
        /// <param name="midAdvanceChanges">How many times the schedule changed after some of the time had already passed</param>
        private TickState Walk(T timeElapsed, TickState start, out Plan plan, out bool startedNewTick, out Rational<long> spilloverOwed, out int midAdvanceChanges)
        {
            TickState state = start;
            plan = CurrentPlan;
            startedNewTick = false;
            midAdvanceChanges = 0;
            bool waiting = IsWaitingForTick;
            Rational<long> spilloverRate = _spilloverFraction.Derivative;
            Rational<Int128> owed = new(Int128.Zero, Int128.One);
            List<(Rational<long> rate, T time)>? timeAtEarlierRates = null;
            while (timeElapsed > T.Zero)
            {
                //How many ticks land before the schedule changes. 0 if it changes right now.
                M ticksFirst = M.Zero;
                if (waiting)
                {
                    if (!M.IsZero(state.Derivative) && !T.IsZero(state.TimeSinceLastIncrement))
                    {
                        ticksFirst = M.One;
                    }
                }
                else if (!M.IsZero(plan.BlendPerTick) && plan.BlendTicksLeft > M.Zero)
                {
                    ticksFirst = plan.BlendTicksLeft;
                }
                else
                {
                    break;
                }
                if (!M.IsZero(ticksFirst))
                {
                    T timeFirst = TimeUntilTick(ticksFirst, state);
                    if (timeElapsed <= timeFirst)
                    {
                        //Stopping exactly on the tick doesn't count. The tick lands, and the tick card occurring there is what makes the change.
                        break;
                    }
                    state = base.AdvanceTicks(timeFirst, state);
                    timeElapsed -= timeFirst;
                    plan = plan with { BlendTicksLeft = BlendTicksAfter(plan, ticksFirst) };
                    (timeAtEarlierRates ??= []).Add((spilloverRate, timeFirst));
                    midAdvanceChanges++;
                }
                state = NewTickAtBoundary(state, plan, out plan, out Rational<long> owedNow);
                owed += RationalConversion.Widen(owedNow);
                spilloverRate = SpilloverRateFor(state, plan);
                waiting = false;
                startedNewTick = true;
            }
            M landedBefore = state.TicksLanded;
            state = base.AdvanceTicks(timeElapsed, state);
            plan = plan with { BlendTicksLeft = BlendTicksAfter(plan, state.TicksLanded - landedBefore) };
            if (timeAtEarlierRates is not null)
            {
                foreach ((Rational<long> rate, T time) in timeAtEarlierRates)
                {
                    owed += (RationalConversion.Widen(rate) - RationalConversion.Widen(spilloverRate)) * RationalConversion.Widen(RationalConversion.ToRational(time));
                }
            }
            spilloverOwed = RationalConversion.LimitDenominator(owed);
            return state;
        }
        /// <summary>
        /// How many ticks a blend has left after some more ticks land. Rewinding gives ticks back.
        /// </summary>
        private static M BlendTicksAfter(Plan plan, M ticksLanded)
        {
            if (M.IsZero(plan.BlendPerTick))
            {
                return plan.BlendTicksLeft;
            }
            return M.Max(M.Zero, plan.BlendTicksLeft - ticksLanded);
        }
        #endregion
        #region Leap years and the tick card
        /// <summary>
        /// Makes sure the leap year events that should exist are on the timeline:
        /// each start event unless its own leap year is the upcoming one, and the end event only if a leap year is upcoming.
        /// </summary>
        /// <remarks>
        /// An event only occurs when the spillover crosses its threshold, so one made when the spillover is already past it would wait forever.
        /// In that case the change it stands for is made here instead, and the events are worked out again.
        /// </remarks>
        private void PutLeapYearEventsOnTimeline()
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                Rational<long> spillover = _spilloverFraction.CurrentValue;
                int spilloverSign = Math.Sign(spillover.Numerator);
                if (UpcomingLeapYear == LeapYearState.None)
                {
                    DropLeapYearEnd();
                }
                else if (_leapYearEnd is null)
                {
                    bool alreadyOver = UpcomingLeapYear == LeapYearState.Positive ? spilloverSign <= 0 : spilloverSign >= 0;
                    if (alreadyOver)
                    {
                        UpcomingLeapYear = LeapYearState.None;
                        changed = true;
                        continue;
                    }
                    //A positive leap year pays the spillover down to 0, a negative one pays it up to 0.
                    _leapYearEnd = MakeLeapYearEvent(Rational<long>.Zero,
                        UpcomingLeapYear == LeapYearState.Positive ? ThresholdDirection.Down : ThresholdDirection.Up);
                }
                if (UpcomingLeapYear != LeapYearState.Positive && _positiveLeapYearStart is null)
                {
                    if (RationalConversion.Compare(spillover, Rational<long>.One) >= 0)
                    {
                        UpcomingLeapYear = LeapYearState.Positive;
                        DropLeapYearEnd(); //If there is one, it is for a negative leap year
                        changed = true;
                        continue;
                    }
                    _positiveLeapYearStart = MakeLeapYearEvent(Rational<long>.One, ThresholdDirection.Up);
                }
                if (UpcomingLeapYear != LeapYearState.Negative && _negativeLeapYearStart is null)
                {
                    if (RationalConversion.Compare(spillover, -Rational<long>.One) <= 0)
                    {
                        UpcomingLeapYear = LeapYearState.Negative;
                        DropLeapYearEnd(); //If there is one, it is for a positive leap year
                        changed = true;
                        continue;
                    }
                    _negativeLeapYearStart = MakeLeapYearEvent(-Rational<long>.One, ThresholdDirection.Down);
                }
            }
        }
        /// <summary>
        /// Makes an event for the spillover reaching a value, listens to it, and puts it on the timeline.
        /// </summary>
        private ReachThresholdEvent<T, Rational<long>> MakeLeapYearEvent(Rational<long> threshold, ThresholdDirection direction)
        {
            ReachThresholdEvent<T, Rational<long>> leapYearEvent = new(_spilloverFraction, threshold, PriorityRank.MeterBookkeeping, OwningTimeline)
            {
                CouldOccur = true, //Otherwise the timeline throws it away the first time it isn't heading for its threshold
                Direction = direction
            };
            leapYearEvent.Occurring += OnLeapYearEvent;
            OwningTimeline.AddPossibleEvent(leapYearEvent);
            return leapYearEvent;
        }
        /// <summary>
        /// Stops caring about the leap year end event, for when the leap year it was for is no longer the upcoming one.
        /// It can't always be taken off the timeline right away, so it is told it can't occur and is no longer listened to.
        /// </summary>
        private void DropLeapYearEnd()
        {
            if (_leapYearEnd is null)
            {
                return;
            }
            _leapYearEnd.Occurring -= OnLeapYearEvent;
            _leapYearEnd.CouldOccur = false;
            _leapYearEnd = null;
        }
        /// <summary>
        /// Called when a leap year event occurs. Notes which leap year the ticks should be in from the next tick on,
        /// and puts the events for getting out of that state on the timeline.
        /// </summary>
        private void OnLeapYearEvent(object sender, Okazo<T> okazo)
        {
            if (ReferenceEquals(okazo, _positiveLeapYearStart))
            {
                _positiveLeapYearStart = null;
                UpcomingLeapYear = LeapYearState.Positive;
                DropLeapYearEnd(); //If there is one, it is for a negative leap year
            }
            else if (ReferenceEquals(okazo, _negativeLeapYearStart))
            {
                _negativeLeapYearStart = null;
                UpcomingLeapYear = LeapYearState.Negative;
                DropLeapYearEnd(); //If there is one, it is for a positive leap year
            }
            else if (ReferenceEquals(okazo, _leapYearEnd))
            {
                _leapYearEnd = null;
                UpcomingLeapYear = LeapYearState.None;
            }
            else
            {
                return; //Not one this meter still cares about
            }
            PutLeapYearEventsOnTimeline();
            if (IsPaused)
            {
                //Only possible if the spillover was sitting exactly on a threshold when the pause started. Resuming picks it up.
                _rateChangedWhilePaused = true;
                return;
            }
            if (IsTickInProgress)
            {
                //The tick in progress finishes as it was. The tick card makes the change when it lands.
                UpdateTickCard();
            }
            else
            {
                StartNewTick();
            }
        }
        /// <summary>
        /// Makes sure a card is on the timeline wherever the timeline has to stop for this meter:
        /// at the tick in progress if anything is waiting for it, or otherwise at the last tick of a blend.
        /// </summary>
        private void UpdateTickCard()
        {
            T untilTick;
            if (IsPaused)
            {
                //No tick is going to land while paused, so there is nothing to stop the timeline for
                RemoveTickCard();
                return;
            }
            if (IsWaitingForTick && IsTickInProgress)
            {
                untilTick = TimeUntilTick(M.One);
            }
            else if (!IsWaitingForTick && IsBlendRunning)
            {
                untilTick = TimeUntilTick(BlendTicksLeft);
            }
            else
            {
                RemoveTickCard();
                return;
            }
            if (_tickCard is not null)
            {
                _tickCard.TimeToEvent = untilTick;
                return;
            }
            _tickCard = new(untilTick, OwningTimeline, PriorityRank.MeterBookkeeping) { Face = this };
            _tickCard.Occurring += OnTickCard;
            OwningTimeline.AddPossibleEvent(_tickCard);
        }
        /// <summary>
        /// Takes the tick card off the timeline, if there is one and the timeline allows it right now.
        /// If it couldn't be taken off, it stays. It gets reused if it is needed again, and ignored if it occurs first.
        /// </summary>
        private void RemoveTickCard()
        {
            if (_tickCard is not null && OwningTimeline.RemoveEvent(_tickCard))
            {
                OwningTimeline.Advance -= _tickCard.OnAdvance;
                _tickCard.Occurring -= OnTickCard;
                _tickCard = null;
            }
        }
        /// <summary>
        /// Called when the tick card occurs. If the tick it was for has landed and something is waiting on that, the change is made.
        /// </summary>
        private void OnTickCard(object sender, Okazo<T> okazo)
        {
            if (!ReferenceEquals(okazo, _tickCard))
            {
                return;
            }
            _tickCard = null; //The timeline is done with it once it has occurred
            if (IsPaused)
            {
                return; //It couldn't be taken off when the pause started. Resuming puts one wherever it is needed then.
            }
            if (IsWaitingForTick && !IsTickInProgress)
            {
                StartNewTick();
                return;
            }
            //Either it was for a tick that got cancelled, or nothing needed it any more. Put it wherever it is needed now, if anywhere.
            UpdateTickCard();
        }
        #endregion
        #region Pausing
        /// <summary>
        /// Stops time from moving this meter until Resume is called. Does nothing if it is already paused.
        /// The meter keeps its modifiers and its place in the tick in progress, and modifiers can still be added, replaced and removed.
        /// Instant changes and changes to the minimum and maximum still happen as normal.
        /// </summary>
        /// <remarks>
        /// The meter and its spillover stay subscribed to the timeline, since that is what clears InstantChange after each advance.
        /// Instead, advancing changes nothing, the spillover's rate is 0, and no tick card is kept, so nothing on the timeline is waiting for this meter to move.
        /// </remarks>
        protected void Pause()
        {
            if (IsPaused)
            {
                return;
            }
            IsPaused = true;
            _rateChangedWhilePaused = false;
            _spilloverRateBeforePause = _spilloverFraction.Derivative;
            _spilloverFraction.SetDerivative(Rational<long>.Zero);
            UpdateTickCard();
        }
        /// <summary>
        /// Lets time move this meter again, from exactly where it was when it was paused. Does nothing if it isn't paused.
        /// If the modifiers (or anything else the rate comes from) changed while paused, that change is made now, as if it had just happened.
        /// </summary>
        protected void Resume()
        {
            if (!IsPaused)
            {
                return;
            }
            IsPaused = false;
            _spilloverFraction.SetDerivative(_spilloverRateBeforePause);
            if (_rateChangedWhilePaused)
            {
                _rateChangedWhilePaused = false;
                ApplyRateChange();
            }
            else
            {
                UpdateTickCard();
            }
        }
        /// <summary>
        /// Puts the meter at a value with nothing changing it, as if it had just been made: every modifier from Add is removed (their keys stop working),
        /// the tick in progress is dropped without landing, any blend and anything owed in the spillover is forgotten, and a pause is ended.
        /// </summary>
        /// <remarks>
        /// The jump to the new value is an instant change, so thresholds between the old value and the new one notice it.
        /// A subclass with groups of modifiers of its own clears those before calling this. Whatever they still hold starts ticking from now.
        /// </remarks>
        /// <param name="newValue">The value to jump to. Must be within the minimum and maximum</param>
        /// <exception cref="ArgumentOutOfRangeException">If newValue is below the minimum or above the maximum. Nothing is changed in that case</exception>
        protected void StartFresh(M newValue)
        {
            if (IsOutsideRange(newValue))
            {
                throw new ArgumentOutOfRangeException(nameof(newValue), newValue, $"The value to start fresh at must be in [{MinValue},{MaxValue}]");
            }
            _sources.Clear();
            IsPaused = false;
            _rateChangedWhilePaused = false;
            _spilloverRateBeforePause = Rational<long>.Zero;

            //The schedule. Setting the derivative directly is what drops the tick in progress.
            base.SetDerivative(M.Zero, DerivativeDenominator, out _);
            IsRateChangePending = false;
            BlendPerTick = M.Zero;
            BlendTicksLeft = M.Zero;
            CurrentLeapYear = LeapYearState.None;
            UpcomingLeapYear = LeapYearState.None;

            //The spillover. The end event is dropped first, since emptying the spillover would otherwise look to it like the leap year finishing.
            DropLeapYearEnd();
            _spilloverFraction.SetDerivative(Rational<long>.Zero);
            _spilloverFraction.AddInstantChange(-_spilloverFraction.CurrentValue);
            PutLeapYearEventsOnTimeline();

            AddInstantChange(newValue - CurrentValue);
            //Starts ticking for whatever a subclass's own groups still hold, and takes the tick card off if nothing is left.
            ApplyRateChange();
        }
        #endregion
        #region Cloning
        /// <summary>
        /// Clones this meter along with its spillover meter, its leap year events and its tick card.
        /// </summary>
        /// <remarks>
        /// The clone doesn't listen to its events here. Rewiring the original's events does that (Timeline.RewireAllEvents does all of them),
        /// the same as for anything else that listens to an event. Every event this meter cares about is on the timeline, so that is enough.
        /// Subclasses override CreateEmptyForClone, and CopyPropertiesForClone if they have more to copy.
        /// </remarks>
        /// <exception cref="UnrecoverableCloneException">If the owning timeline hasn't been cloned yet</exception>
        public override object Clone(CloneContext context)
        {
            if (context.TryGet(this, out RateManagedMeter<T, M> existingClone))
            {
                return existingClone;
            }
            if (!context.TryGet(OwningTimeline, out Timeline<T> owningTimelineClone))
            {
                throw new UnrecoverableCloneException("Owning timeline must be cloned before cloning any meters.");
            }
            //The spillover is an ordinary meter that knows nothing about this one, so it is safe to clone before this is registered.
            //If the timeline has already cloned one of the leap year events, this just finds the clone that made.
            LinearContinuousMeter<T, Rational<long>> spilloverClone = context.GetOrClone(_spilloverFraction);
            //The group of modifiers is cloned, not made again, so it keeps its number and its keys whatever order things are cloned in.
            RateManagedMeter<T, M> clone = CreateEmptyForClone(owningTimelineClone, spilloverClone, context.GetOrClone(_sources), context);
            context.Register(this, clone);
            //This comes after registering, since the tick card's face is this meter and cloning the card asks for this meter's clone.
            CopyPropertiesForClone(clone, context);

            //A meter has to advance before its spillover, and the spillover clone was made (and so subscribed) before the meter clone.
            //Taking it off and putting it back on makes it the later of the two again.
            owningTimelineClone.Advance -= spilloverClone.OnAdvance;
            owningTimelineClone.Advance += spilloverClone.OnAdvance;
            return clone;
        }
        /// <summary>
        /// Makes a new instance of this class for the cloned timeline, with the value, minimum and maximum this meter has and nothing else.
        /// A subclass overrides this to make one of itself with its own clone constructor, which should pass the spillover on to this class's clone constructor.
        /// </summary>
        /// <param name="owningTimeline">The timeline that should own the clone</param>
        /// <param name="spilloverFraction">The clone of this meter's spillover meter, for the clone to use as its own</param>
        /// <param name="sources">The clone of this meter's group of modifiers, for the clone to use as its own</param>
        /// <param name="context">The clone context, in case the subclass needs the clones of other objects to make itself</param>
        protected virtual RateManagedMeter<T, M> CreateEmptyForClone(Timeline<T> owningTimeline, LinearContinuousMeter<T, Rational<long>> spilloverFraction, ModifierGroup<T, M> sources, CloneContext context)
        {
            //Without the instant change, which CopyPropertiesForClone puts back so that the clone has it as an instant change too
            return new(owningTimeline, CurrentValue - InstantChange, MinValue, MaxValue, spilloverFraction, sources);
        }
        /// <summary>
        /// Fills in a clone to match this meter. Override this (but always call base.CopyPropertiesForClone) in subclasses to copy any additional properties.
        /// </summary>
        /// <param name="clone">The new clone to modify. Already registered with the context</param>
        /// <param name="context">The clone context, for the clones of the events this meter refers to</param>
        protected virtual void CopyPropertiesForClone(RateManagedMeter<T, M> clone, CloneContext context)
        {
            clone.AddInstantChange(InstantChange); //Set instant change and bring CurrentValue back to where it is here
            clone.UUID = UUID;
            clone.CopyScheduleFrom(this);

            //The events. Whichever of these the timeline has already cloned are in the context, and the rest are cloned now.
            clone._positiveLeapYearStart = _positiveLeapYearStart is null ? null : context.GetOrClone(_positiveLeapYearStart);
            clone._negativeLeapYearStart = _negativeLeapYearStart is null ? null : context.GetOrClone(_negativeLeapYearStart);
            clone._leapYearEnd = _leapYearEnd is null ? null : context.GetOrClone(_leapYearEnd);
            clone._tickCard = _tickCard is null ? null : context.GetOrClone(_tickCard);
        }
        /// <summary>
        /// Puts this meter at the same point in the same schedule as another one. Only for cloning.
        /// </summary>
        /// <remarks>Needed since the public SetDerivative refuses, and the base one can only be reached from inside the meter being set.</remarks>
        private void CopyScheduleFrom(RateManagedMeter<T, M> original)
        {
            base.SetDerivative(original.Derivative, original.DerivativeDenominator, out _);
            SetTimeSinceLastIncrement(original.TimeSinceLastIncrement);
            IsRateChangePending = original.IsRateChangePending;
            MidAdvanceRateChanges = original.MidAdvanceRateChanges;
            CurrentLeapYear = original.CurrentLeapYear;
            UpcomingLeapYear = original.UpcomingLeapYear;
            BlendPerTick = original.BlendPerTick;
            BlendTicksLeft = original.BlendTicksLeft;
            IsPaused = original.IsPaused;
            _spilloverRateBeforePause = original._spilloverRateBeforePause;
            _rateChangedWhilePaused = original._rateChangedWhilePaused;
        }
        #endregion
        public override string ToString()
        {
            return base.ToString() + $", {Modifiers.Count} modifiers asking for {TrueRate} per time unit, spillover {CurrentSpilloverFraction}"
                + (IsPaused ? ", paused" : "")
                + (CurrentLeapYear != LeapYearState.None ? $", in a {CurrentLeapYear} leap year" : "")
                + (IsBlendRunning ? $", blending {BlendPerTick} extra into each of the next {BlendTicksLeft} ticks" : "")
                + (IsRateChangePending ? ", rate change waiting for the tick in progress" : "")
                + (UpcomingLeapYear != CurrentLeapYear ? $", leap year changing to {UpcomingLeapYear} when the tick in progress lands" : "");
        }
    }
    /// <summary>
    /// Whether a RateManagedMeter is adding or taking one more per tick than its rate rounds to, to pay back what rounding has cost.
    /// </summary>
    public enum LeapYearState
    {
        /// <summary>Ticks are what the rate rounds to</summary>
        None,
        /// <summary>Each tick is one higher, since the meter is behind what it has earned</summary>
        Positive,
        /// <summary>Each tick is one lower, since the meter is ahead of what it has earned</summary>
        Negative
    }
}
