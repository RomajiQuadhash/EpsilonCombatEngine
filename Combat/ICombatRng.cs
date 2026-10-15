using Rationalz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Combat
{
    /// <summary>
    /// The RNG that should be used during a combat. In a simulation, a different more deterministic setup is used instead.
    /// </summary>
    public interface ICombatRng
    {
        /// <summary>
        /// Asks a yes no question with a probability yes of P. (in simulations, this might be entirely deterministic)
        /// </summary>
        /// <param name="p">Probability of Yes/true. If >=1, will always be true, <=0 always false, and NaN throws</param>
        /// <param name="reason">Why you're calling RNG. Mostly used in a simulation to give "reasonable" guesses</param>
        /// <returns>True if yes, false if no</returns>
        bool Roll(double p,ReasonForRNG reason);

        /// <summary>
        /// If a roll failed, how much of the damage/other flexible effect goes through anyway?
        /// </summary>
        /// <remarks>In real RNG, this basically returns 0, the fuzzy outcome makes sense only if there's prediction uncertainty</remarks>
        /// <param name="p">The probability of the roll that returned False</param>
        /// <param name="reason">Why you're calling RNG.</param>
        /// <returns>What fraction of the outcome should have been considered to happen. 0 means none</returns>
        Rational<long> FuzzyOutcome(double p, ReasonForRNG reason);
        /// <summary>
        /// Gets a random range including both endpoints in whatever numerical unit you want
        /// </summary>
        /// <typeparam name="T">The numeric type of the range to compute</typeparam>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="reason">Why you're calling RNG. Mostly used in a simulation to give "reasonable" guesses</param>
        /// <returns>T Element of [min,max]</returns>
        /// <remarks>If your T can't be multiplied by a Rational, this might have problems</remarks>
        T RandInRange<T>(T min, T max, ReasonForRNG reason) where T : INumber<T>;
        /// <summary>
        /// Specialized function for the hit quality roll (halves the hit rate and rolls it twice)
        /// </summary>
        /// <param name="hitRate">Number in [0,2] for the hit rate. At 2 or above, always returns Direct</param>
        /// <returns>Barely if both halved probabilities missed, direct if both hit, Normal if only one hit.</returns>
        HitQuality GetHitQuality(double hitRate);
    }
    /// <summary>
    /// Why are you looking for a random output?
    /// </summary>
    public enum ReasonForRNG
    {
        Nonspecific,
        HitRoll,
        HitRollOfMultihit, //split out because it's likely they want to use FuzzyOutcome to simulate a fraction rather than yes/no
        BonusDamage,
        AdditionalNegativeEffect,
        AdditionalPositiveEffect,
        DelaySize,
        EarlyRecovery,
        AIDecisionMaking, //This one might be the only truly random ones in a simulation since Commanders only see clones
    }
    /// <summary>
    /// What kinds of Hit can happen?
    /// </summary>
    public enum HitQuality
    {
        Barely, //Does less damage, should likely have no or a reduced change of additional effects
        Normal,
        Direct //Does everything Normal does, but an additional bit of Instant damage and possibly higher additional effects
    }
}
