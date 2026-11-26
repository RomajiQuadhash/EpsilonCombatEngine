using Rationalz;

namespace CoreMetrics
{
    /// <summary>
    /// Every number that tunes how battles feel, kept in one place so they can be adjusted together.
    /// This is an exception to the rule that the lower projects know nothing about what the higher ones do with them:
    /// it names things (Defense, speed delays) that only mean something from Meters or Combatants upward.
    /// It is here anyway, since it has to be below everything that reads it, and one file is much easier to tune than values spread over the projects.
    /// Nothing else about those concepts belongs in CoreMetrics. Only the numbers.
    /// They are exact fractions, so any value can be set: new(3, 2) is one and a half.
    /// </summary>
    /// <remarks>Claude written, Romaji Approved</remarks>
    public static class BattleTuning
    {
        /// <summary>
        /// How many times longer than at the highest speed a delay can get, however slow the combatant is. Read by SpeedMeter.
        /// </summary>
        public static readonly Rational<long> MaxDelayMagnitude = new(3, 1);
        /// <summary>
        /// The lowest that Defense can count as, so that damage can't explode as a debuffed Defense gets close to 0. Read by HealthMeter.
        /// Placeholder: the design hasn't chosen this value yet.
        /// </summary>
        public static readonly Rational<long> MinDefense = new(1, 1);
        /// <summary>
        /// Turns a source of damage's rate (damage per unit of time, before Defense) into how much it chips off the Guard when it starts. Read by HealthMeter.
        /// If every length of time in the game is multiplied by some number, multiply this by the same number.
        /// Placeholder: the design says to work it out from how many overlapping sources of damage a Guard Break should take (about 2 or 3).
        /// </summary>
        public static readonly Rational<long> GuardScale = new(1, 1);
        /// <summary>
        /// How long the Guard takes to heal back what one source of damage chipped off, from when that source ends. Read by HealthMeter.
        /// Provisional: 20 isn't a design choice. It is long enough that the heal still takes several whole time units on a whole-number timeline.
        /// </summary>
        public static readonly Rational<long> GuardHealDuration = new(20, 1);
        /// <summary>
        /// The lowest the Guard meter can go while the Guard is Broken, standing in for "negative infinity". Read by HealthMeter.
        /// </summary>
        public static readonly Rational<long> GuardBrokenFloor = new(-1_000_000_000, 1);
        /// <summary>
        /// How far above 0 the Guard meter has to heal for a Guard Break to end. Read by HealthMeter.
        /// Small and positive, not 0: a Guard at exactly 0 with its floor back is Staggered, and a meter that should come back to 0 can land a hair above it.
        /// </summary>
        public static readonly Rational<long> GuardRestoreThreshold = new(1, 1_000_000);
    }
}
