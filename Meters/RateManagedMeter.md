# Why RateManagedMeter is the way it is

`RateManagedMeter` is by far the most complicated meter. This explains what it is for, the rules it follows, and why each rule is what it is, including the simpler versions that were tried and didn't work.

## The goal

A discrete meter (`LinearDiscreteMeter`) changes by a whole number every so often: 5 every 2 time units, say. That is one source of change with one schedule.

A real meter has several sources at once, each with its own schedule: a regeneration of 1 every 5, a poison of 7 every 3, a buff that adds 2 every 25. `RateManagedMeter` merges them into the one schedule a discrete meter can have, in a way that:

- **feels reasonable.** The meter ticks at the pace of whatever is affecting it most, and a change to the sources shows up when you would expect it to.
- **loses nothing.** Merging rounds things, and the rounding is paid back smoothly rather than dropped or dumped all at once.
- **stays deterministic and cloneable**, like everything else in a combat.

## The pieces, in one place

| Piece | What it is |
|---|---|
| `ModInstance` | One source: an amount per tick and a tick rate. It can't change. To change a source, replace it. |
| `ModifierKey` | What `Add` hands back, used to `Replace` or `Remove` that source later. |
| `ModifierGroup` | The bookkeeping for one category of sources: which there are, their exact sum, and which is strongest. The meter has one. |
| True rate | The exact sum of every source's amount ÷ tick rate. What the meter should average. |
| Tick rate | How often the meter actually ticks. Taken from the strongest source. |
| Per tick | What each tick adds: the true rate × tick rate with the fraction dropped, plus a leap year's 1, plus a blend's share. |
| Spillover | A continuous meter holding what has been earned but not added yet. |
| Leap year | A stretch of ticks that add (or take) 1 more, to bring the spillover back to 0. |
| Blend | A fixed extra on each of the next few ticks, paying out what a cancelled tick had built up. |
| Tick card | A card that stops the timeline at a tick the meter is waiting for. |

## Sources

### A source is replaced, never changed

`ModInstance` has no setters and can't be altered with `with`. Its `Magnitude` (amount ÷ tick rate) is worked out once in the constructor, which is also where a tick rate of zero or less is refused.

*Why:* the meter caches things about its sources (their sum, which is strongest). If a source could change under it, those would silently go stale. Having only a constructor also means the check on the tick rate can't be skipped.

### The meter hands out the keys

`Add` returns a `ModifierKey`. Callers keep it and never make their own.

*Why:* sources are anonymous on purpose. Two effects can add identical sources, and the meter has to be able to remove one without touching the other. Keys made by the meter can't collide and don't tie a source to whatever created it.

### The sum is exact

The sum of the sources is kept as an exact fraction and is never rounded. If adding a source would need a fraction too big to store, `Add` throws before changing anything.

*Why:* if the sum were rounded on the way up, removing every source might not bring it back to exactly 0, and the meter would never stop. Refusing in `Add` is a safe place to fail, since nothing has been changed yet. When the last source is removed the sum is set to 0 directly, without any arithmetic.

### The strongest source sets the tick rate, and ties go to the fastest

The meter ticks at the tick rate of the source with the largest |Magnitude|. If two are equally strong, the one that ticks more often wins.

*Why the strongest:* it is the one the player is watching. A heavy poison every 3 should still look like something happening every 3 when a small regeneration is added.

*Why ties matter at all:* 20 every 2 and 10 every 1 are the same rate, but a discrete meter only changes when a tick lands, so they behave differently. Faster was chosen because it shows the change sooner and in smaller steps.

The strongest source is cached. Adding only compares the new one. Removing only looks through the rest if the removed one was as strong as the strongest, compared by value since two different sources can be equally strong.

### The tick rate is stretched if a tick would round to nothing

If the true rate is so small that a tick at the strongest source's pace would add less than 1, the tick rate is made long enough that a tick adds exactly 1.

*Why:* a meter that ticks and adds 0 isn't doing anything. This comes up when sources nearly cancel: 1 every 3 against -1 every 4 is 1/12, so the meter ticks 1 every 12.

### The rate can't be set directly

`SetDerivative` throws. On an ordinary discrete meter it is how you set the rate, but here the rate comes from the sources, so setting it isn't "not allowed right now", it is the wrong thing to ask for.

### The bookkeeping is its own class

The sources, their exact sum and the cached strongest source live in a `ModifierGroup`. The meter has one, and `Add`, `Replace` and `Remove` pass through to it. The group knows nothing about ticks. The meter only asks it two things: what the total is (`TrueRate`) and how often the strongest source ticks (`PaceTickRate`).

*Why:* some meters have more than one category of source, where one category counts differently from another (damage that is divided by a defense, next to healing that isn't). Such a meter is a subclass that holds a second group, overrides `TrueRate` and `PaceTickRate` to say how the groups go together, and calls `ApplyRateChange` whenever that answer changes for a reason other than adding, replacing or removing. Putting two categories inside `RateManagedMeter` itself was considered and turned down, since most meters only need one.

Three things follow:

- **Changing how a group counts touches no source.** The sources stay as they were added, and the meter works out its rate again once. The rules below for a change partway through a tick apply as they do to any other change.
- **A key belongs to the group that gave it.** Two groups can hand out the same key, so a subclass needs separate methods for each group.
- **A subclass has to copy its own groups when it is cloned,** with `ModifierGroup.CopyFrom`.

## When the sources change

The hard part of merging is what to do when the sources change partway through a tick. A tick that is 4 into a 10 has built up 40% of what it was going to add. Something has to happen to that.

The rules, in the order they are checked:

| Situation | What happens |
|---|---|
| No tick in progress | The new rate just starts. |
| Nothing is left to tick (the sum is now 0) | The tick in progress lands with what it was going to add. Then the meter stops. |
| Same tick rate as before | The tick in progress keeps its place and lands with the new amount. |
| New tick rate is not sooner than the tick in progress | The tick in progress keeps its place and lands with the new rate's amount for the *old* tick rate. The new tick rate starts when it lands. |
| New tick rate is sooner than the tick in progress | The tick in progress is cancelled. What it had built up is blended into the next ticks. |
| The new rate's amount for the old tick rate would be 0 | Also cancelled, since there is no point waiting for a tick that adds nothing. |

"Sooner" means the new tick rate is strictly less than the time the tick in progress has left. If they are equal, it waits.

### Why not just restart the tick every time

That was the first version, and it is what a plain `LinearDiscreteMeter.SetDerivative` does: reset the tick and report what was built up. For this meter it meant every change to the sources threw away the tick's place. A source added and removed quickly could keep the meter from ever ticking, and each time there was a lump of built-up value to deal with.

Keeping the tick's place wherever possible means most changes don't build anything up at all. The tick was going to land at a certain time and it still does. Only its amount changes.

### Why a tick is only cancelled for something sooner

Waiting for the tick in progress is the least surprising thing to do, as long as it doesn't delay anything. If the new schedule would have ticked before the old tick lands, waiting would make the meter slower to react than the new sources ask for, so that is the one case where the old tick is given up.

### Why a tick lands with whatever the rate is when it lands

When the tick rate doesn't change, the tick in progress simply lands with the new amount. Nothing is corrected for the part of the tick that passed at the old rate.

*Why:* an earlier version did correct for it, by putting the difference into the spillover. That is more exact against a continuous ideal, but it meant every change to the sources jolted the spillover, sometimes by several whole units, and those jolts were what caused the hardest bugs (leap years starting and stopping in a loop). A discrete meter is a thing that changes when a tick lands. Saying the tick delivers what the rate is at that moment is simple, it is how damage over time works in most games, and it removes a whole class of problems.

*What it costs:* how much a short-lived source delivers depends on where the ticks fall. One added just before a tick lands gets a full tick. One added and removed between two ticks delivers nothing.

### Why the last tick still lands when the sources go away

When the sum becomes 0 partway through a tick, that tick lands as it was and then the meter stops. Going to 0 never cancels a tick and never starts a blend.

*Why:* there is no new tick rate to compare against, so the "sooner" rule has nothing to say, and letting the tick finish matches every other case where the tick rate isn't getting shorter.

## Spillover and leap years

### What the spillover holds

Dropping the fraction from each tick loses a little every tick. At 7/25 ticking every 5, each tick should add 1 and 2/5 and adds 1.

The spillover is a `LinearContinuousMeter` whose rate is the true rate minus the rate the ticks are actually applying. It rises (or falls, for a negative rate) steadily as time passes, with no bookkeeping needed per tick. It also receives the part of a cancelled tick that didn't divide evenly into a blend.

*Why a real meter:* it keeps itself up to date as the timeline advances, and threshold events can be put on it like on any other meter. An earlier version held it as a plain number, which meant the meter had to do all of that by hand.

### How a leap year pays it back

Three threshold events watch the spillover, each with a fixed threshold:

- **+1, rising:** start a positive leap year.
- **-1, falling:** start a negative leap year.
- **0:** end the leap year that is running.

During a positive leap year every tick adds 1 more. That makes the ticks apply more than the true rate, so the spillover's rate turns negative and it comes back down to 0, where the end event fires.

With 7/25 ticking every 5:

| Time | What happens | Spillover |
|---|---|---|
| 5, 10 | Ticks of 1 | 2/5, 4/5 |
| 12 | The start event fires (see "early" below). Nothing changes yet | 24/25 |
| 15 | A tick of 1 lands and the leap year starts | 6/5 |
| 20 | Leap tick of 2 | 3/5 |
| 25 | Leap tick of 2. The spillover is back to 0 and the leap year ends | 0 |

That is 7 over 25, exactly.

### Why an event only sets a flag

When a leap year event fires, the meter only notes which leap year the ticks *should* be in (`UpcomingLeapYear`). The amount per tick changes when the tick in progress lands (`CurrentLeapYear`), or straight away if no tick is in progress.

*Why:* this is the same rule as for the sources. The tick in progress keeps what it started with. The earlier version changed the tick in progress the moment the event fired and corrected the spillover for it, and that correction could itself push the spillover back across the threshold that had just fired, so the events could set each other off forever.

### Why 1 per tick, and never 0

A leap year adds or takes exactly 1 per tick, which keeps the meter's movement as even as it can be.

If adding the leap year's 1 would make a tick add exactly 0, it is bumped one further in the leap year's direction. This only happens when a leap year is running the opposite way to the sources (after the sources flip sign partway through one). The rule is that a meter with something acting on it never has a tick that adds nothing. The same rule applies after a blend's share is added.

### The events are made when needed and forgotten when they fire

- A start event exists unless its own leap year is the upcoming one.
- The end event exists only while a leap year is upcoming.

When one fires, the meter forgets it and makes whatever the rules above now say is missing. They are ordinary `ReachThresholdEvent`s that tell the meter through `Occurring`.

*Why:* every event the meter cares about is on the timeline, so the timeline's own cloning and rewiring covers all of them. A design with permanent events that went quiet between uses was considered, and so was a special event type that called the meter directly. Both needed the meter to depend on events that weren't on the timeline, or needed their own cloning rules.

Three things follow from using plain threshold events:

- **An event is never made when the spillover is already past its threshold.** A threshold event fires on a crossing, so one made past its threshold would wait forever. In that case the meter makes the change itself and works out the events again.
- **An event the meter still refers to is trusted when it fires.** The meter doesn't check the spillover's value first. On a whole-number timeline an event can legitimately fire a little early (below), and a check would reject it, make a new one, and loop.
- **When a leap year flips straight to the opposite one, the old end event is dropped,** not reused: the meter stops listening to it and marks it as unable to occur. One big jump in the spillover can flag both the end event and the opposite start event, and without this the old end event could end the new leap year.

### Early on a whole-number timeline

A threshold event's time is turned into the timeline's number type. On a whole-number timeline that drops the fraction, so an event can fire up to one time unit before the spillover really reaches the threshold. In the table above the spillover reaches 1 at 12.5 and the event fires at 12.

Since the event only sets a flag for the next tick, this changes which tick a leap year starts on by at most one, and the totals still come out exact. On a `double` timeline the time is the closest representable value and this doesn't happen.

## Blends

### What they are for

When a tick is cancelled, what it had built up is owed. Putting all of it into the spillover would work, but the spillover is paid back at 1 per tick. Cancelling a 50-per-10 tick at 9 would put 45 there and take 45 ticks to deliver, and if the new sources point the other way the meter would visibly creep the wrong way the whole time.

A blend delivers it quickly but not too quickly.

### How the amount is split

1. The amount is what the cancelled tick had built up: its amount × how far through it was.
2. The number of ticks is the time the cancelled tick had left ÷ the new tick rate, rounded up.
3. Each of those ticks adds the whole-number part of amount ÷ ticks.
4. Whatever is left over, including any fraction, goes into the spillover for an ordinary leap year to deliver.

50 every 10, cancelled at 4 by a source ticking every 4: 20 is built up, the tick had 6 left, so that is 2 ticks adding 10 extra each, the second at 12.

*Why rounded up:* so that none of it lands sooner than the cancelled tick would have. The last of it can land up to one new tick later than the cancelled tick was due.

If the amount is too small to give every tick at least 1, there is no blend and all of it goes to the spillover.

### Why the blend is kept out of the spillover

The blend's share is part of what each tick adds, but it is left out when working out the spillover's rate, and the amount being blended is never put into the spillover.

*Why:* if it were, the spillover would sit far above +1 for the length of the blend. A leap year would start on top of the blend, delivering faster than intended, and then overshoot. Kept apart, the leap year events only ever see the small leftover, and their thresholds keep their meaning.

### Changes during a blend

- **The tick rate changes and waits:** the tick in progress lands with its share. What the blend still owes is then spread again on the new tick rate, over the time the blend had left.
- **The tick rate changes and cancels:** what the blend still owes and what the cancelled tick had built up are spread together, up to when the blend would have ended.
- **The sources go away:** the blend is still paid out on the current tick rate, with ticks that add only the blend's share. Then the meter stops.

In each case the rule is the same as for the first blend: nothing lands sooner than it would have.

## The tick card

Several things wait for the tick in progress to land: a new tick rate, the meter stopping, a leap year starting or ending. A blend also ends on a particular tick.

Whenever any of those is pending, the meter keeps a card on the timeline at that tick. When it occurs, the meter starts its next tick with whatever is now due. The card's face is the meter itself, so in a debugger the card's description is the meter's, which says what is waiting.

*Why a card and not handling it inside the advance:* if the timeline moved past the tick, the meter's rate would change between two events. Any other event on this meter (reaching a maximum, say) predicts its time from the current rate, so it could be missed, or could fire for something that was no longer going to happen. Stopping the timeline at the tick lets every event work out its time again from the new rate.

*Why not make the other events aware of the coming change:* that would have meant the meter's threshold predictions looking ahead through a rate change, and the data that orders events firing at the same time (`TransitionData`) would no longer describe what actually happened.

A card that is no longer needed is taken off the timeline if the timeline allows it at that moment. If not, it stays, is reused if something waits again, and is ignored if it occurs first.

The cards and the leap year events use the `MeterBookkeeping` priority. It comes after the threshold triggers so that a threshold event set off by a tick still sees the rate that caused it.

## Advancing

### Asking is separate from doing

`AdvanceTicks` takes a state and a length of time and returns the state afterwards. It changes nothing. `CommitTickState` is what makes a result real.

*Why:* simulating needs to be able to ask what would happen. The first version of the override changed the meter's value partway through working it out, which made that impossible.

### The fallback inside the advance

The tick card should always stop the timeline at the right tick. If something else moves the meter past it anyway (calling `OnAdvance` directly, or removing the card), the advance is split at that tick: the time before uses the old schedule and the time after uses the new one. `MidAdvanceRateChanges` counts how often this happens. Every test checks it is 0, which is how they show the timeline really did stop.

### Landing exactly on a tick

On a `double` timeline, adding the elapsed time to the tick's progress and taking the remainder can come out a hair to either side of a tick. So `LinearDiscreteMeter` has one method, `TimeUntilTick`, that every prediction of a tick's time comes from, and the advance compares the elapsed time against that same calculation. If they are equal it lands exactly on the tick.

*Why:* subtracting a number from itself is always exactly 0, but only if both sides are worked out the same way. Before this, about 2.3% of random floating point cases missed their tick.

## Pausing

`Pause` and `Resume` are protected, for a subclass that needs time to stop moving the meter for a while and then carry on from exactly where it was (a combatant that can't be touched for a few moments). `IsPaused` says which state it is in.

While paused:

- **Advancing changes nothing.** The meter keeps its sources, its place in the tick in progress, its leap year and its blend.
- **Threshold questions are answered as a meter that isn't moving.** `LinearDiscreteMeter` asks `IsStill` instead of checking the derivative for 0, and this meter says it is still while paused.
- **The spillover's rate is 0,** and it gets its old rate back on resuming.
- **There is no tick card.**
- **Instant changes and changes to the limits work as normal.**
- **Sources can still be added, replaced and removed.** The schedule doesn't change until the meter is resumed, and then it changes as if the sources had just changed.

*Why not unsubscribe the meter and its spillover from the timeline:* that was the first idea, and it does freeze both values. But three things on the timeline predict from the stored rates and would keep waiting for a meter that isn't moving: threshold events on the meter, the leap year events on the spillover, and the tick card. And `InstantChange` is only cleared when a meter advances, so an instant change made while unsubscribed would look like it had just happened for the whole pause. Staying subscribed and doing nothing on an advance avoids all four, and there is no advance order to put back afterwards.

*Why a change while paused waits:* working out a new schedule starts ticks, moves the tick card and sets the spillover's rate, which is everything a pause has stopped. Noting that something changed and doing it once on resuming keeps the pause simple.

## Cloning

`Clone` follows the same pattern as the other meters: a clone constructor that only lets values be set, with `Clone` finding and filling in everything.

Things specific to this meter:

- **It takes its spillover, events and tick card from the clone context,** and makes none of its own. Whichever of them the timeline has already cloned are found there. If the clone made new ones, there would be two spillover meters and the events would be watching the wrong one.
- **It doesn't listen to its events.** Rewiring the original's events does that, as for anything else that listens to an event.
- **It registers itself before cloning the tick card,** since the card's face is the meter.
- **It moves its spillover after itself in the advance order.** The meter has to advance before its spillover, and if the timeline cloned an event first, the spillover's clone was made first.
- **A subclass overrides two methods, not `Clone`.** `CreateEmptyForClone` makes one of the subclass with its own clone constructor, and `CopyPropertiesForClone` copies whatever the subclass adds (after calling the base). `Clone` itself keeps the order of the steps above, so a subclass can't get them wrong. This is the same shape as `TimedTempChange`.

**Whatever owns the meter has to clone it before the timeline's events are rewired.** Unless a tick card happens to be on the timeline, nothing on the timeline refers to the meter itself. The leap year events only know its spillover. If the meter was never cloned, rewiring throws.

## Fractions

Everything exact in this meter is a `Rational<long>`, and all conversions go through `RationalConversion`:

- A number becomes the simplest fraction that converts back to exactly that number, so 3.3333333333333335 is 10/3 and not a sixteen-digit fraction.
- Results other than the sum are rounded to a denominator of at most 10,000,000, after being worked out in a wider type so the working itself can't overflow.

*Why:* the fraction library reads a `double` through its decimal digits and wraps silently on overflow. With its own conversion, a rate of 3/10 at a tick rate of 10/3 gave a spillover rate of 1/66666666666666670 where it should be exactly 0.

## Known limits

- **Floating point timelines need kind values.** Whole numbers and fractions that share a binary grid (0.5, 0.25) are exact. Awkward ones can leave a threshold event a hair off zero, and it refuses to fire.
- **The spillover's arithmetic isn't capped.** It is a plain `LinearContinuousMeter`. With kind values its denominators stay small.
- **At the meter's minimum or maximum, the spillover keeps running.** A meter pinned at its limit with an inexact rate keeps starting and ending leap years, so a simulation of it never runs out of events by itself.
- **Asking what would happen covers the meter, not its spillover.** The answer gives the value and schedule. It doesn't say what the spillover would be.

*made with Claude*