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
    }
}
