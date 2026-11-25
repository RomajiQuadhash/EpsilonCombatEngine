## Current:
### Development Structure
The order of projects is now set: `CoreMetrics -> Timeline -> Meters -> Combatants -> Combat -> TestMachine`

This means that when implementing Meters, eveything is done in the context that it is used by Combatants. If something would be useful to a higher level, implement it there so every Meter has been tested when Combatants are implemented.

Development is to be done in branches that each implement a concrete idea. 
Since the framework is "design as code", sometimes they use an overly abstract class for a concept.
Replacing those with a more specific class now that concrete classes exist is **ALL** you should do in any later project before your current project is implemented **and** tested.
### Function of Each Project
#### Meters
Holds anything where values change over timeline time.
Nothing should be done about the values besides compare them and fire off events.
Don't think about concepts like "KOs", "Abilities", "specific stats", or "Status effects".

Speed is a stat that is treated differently from other stats since it is an "Aggregate meter". Meters whose values participate in combat-wide aggregates (max, min, sum, etc.) are Aggregate meters and register with a corresponding registry object. The registry is shared across the combat and lives at Meters, and all the meters reference it. Objects owning an aggregate meter hold a reference to the registry and register with it on construction.

`RateManagedMeter` is the meter for anything that has several sources of change at once. It is by far the most complicated one, and `Meters/RateManagedMeter.md` explains why it is the way it is. Read that before changing it.

Things that whatever owns meters needs to know:
- **A `RateManagedMeter` has to be cloned by whatever owns it, before the timeline's events are rewired.** Usually nothing on the timeline refers to the meter itself (its events only know its spillover meter), so cloning the timeline alone doesn't reach it, and rewiring throws if it was missed.
- **A `LinearContinuousMeter` never holds an integer type.** Use `double` or a fraction. Whole-number meters are `LinearDiscreteMeter` or `RateManagedMeter`.
- **Meters are changed by events.** `InstantChangeEvent` makes one change. The temp changes (`TempInstantChange`, `TempContinuousChange`, `TempRMMChange`) make a change and take it back off later. `TempContinuousChange.CreateEventFromLerp` makes the right one for "this much over this long", and `TempRMMChange.CreateEventFromTickedLerp` does the same in a set number of ticks. All of them are an `IMeterChangeEvent`, which says which meter they change and whether they have finished. None of them put themselves on the timeline.
- **A threshold outside a meter's minimum and maximum never occurs.** One exactly on a limit does. Moving a limit so that the meter is pushed counts as an instant change.
- **Anything that listens to an event has to be a method of an object that gets cloned too.** Rewiring can't carry any other kind of listener over to a clone.

To run the tests, use `dotnet run --project Meters-Test` and `dotnet run --project Timeline-Test`. `dotnet test` doesn't work with the test packages on the current SDK.
#### Combatants
Combatants hold meters and set up flags of itself and hold intrinsic specialties.

The one thing to be aware of is that they don't commit actions. That is created by the Commanders, they just sit there and raise events if conditions happen to them like killed or KO.
#### Combat
This one is mostly "everything else", but there is still an exclusion: UI handling.

You should surface what is going to be needed to draw a UI, but the whole point is that this works whether you're in a terminal, Godot, a web server, anything. Try to keep things flexible so a config file could be used to set names of actions, attack values, modifications of stats, durations, all that.
#### TestMachine
This project is the UI (but not a GUI), a way to test the engine as a system, a sandbox. It is at the top of the list and nothing else should depend on TestMachine.

It is not a game.

What I mean by this is that any games that implement this engine should not do it by sending things into TestMachine. They should do it by interacting with Combat, and build any objects they need as subclasses of those in Combat and read their information from Combat.

### Customization points
While almost everything needs to be built on top of what comes in, there are some intrinsic elements based on what games I plan to make with it. If you are making your game, evaluate with these.

A list of these include (and things you may need to modify)
- `PriorityRank` in `IOkPriority` in Timeline: Reference game specific information. Mostly easy to change if needed but you will need a `None` value for the functions to work. You will also need `MeterBookkeeping`, which meters use for their own upkeep (a `RateManagedMeter` starting or ending a leap year, or changing its rate at a tick). It has to stay after the meter limit and trigger priorities, so that a trigger set off by a tick still sees the rate that caused it.
- `MeterTypes` in `Enums` in Combatants: These currently have no functional meaning (just being lookups), but they need to be set before using Actions (in Combat) that are specific to those meters. May have others added.
- `StatusEffects` in `Enums` in Combatants: Current ones are pretty generic, implementations will center around those
- `CombatStats` in `Enums` in Combatants: Likely, these don't need to be changed unless you're fundamentally changing elements of the system
- `ReasonForRNG` in `ICombatRng` in Combat: I suspect these will not need adding to, except maybe more specificity
- `HitQuality` in `ICombatRng` in Combat: Currently planned as a core part of the mechanics. Do not modify, since it is linked to core game mechanics.
## OLD/Historical:

Why are we looking at copying all of these classes?
It is so that we can duplicate a "combat" (later holding class) to simulate events if a user picks some option. 

To do that smoothly, we'll need to ensure that all the classes involved in the combat simulation can all be duplicated and re-linked to each other systematically.
The interfaces added will tell later classes how to duplicate those classes and how to re-link them.