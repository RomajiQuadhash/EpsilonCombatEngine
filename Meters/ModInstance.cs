using Rationalz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Meters
{
    /// <summary>
    /// Holds the information about a single source of change for a meter.
    /// Fully anonymous, so the RateManagedMeter should be used to manage the sources and identify which source is which and hence what source to replace/remove when the effect adding a source changes or ends.
    /// </summary>
    /// <typeparam name="T">The time unit of the timeline</typeparam>
    /// <typeparam name="M">Type of the meter. Usually an integer type.</typeparam>
    public record class ModInstance<T, M> where T : INumber<T> where M : INumber<M>
    {
        /// <summary>
        /// Creates a source of change.
        /// </summary>
        /// <param name="perTick">How much change to apply per tick</param>
        /// <param name="tickRate">How much time is between ticks. Must be positive</param>
        /// <exception cref="ArgumentOutOfRangeException">If tickRate is zero or negative</exception>
        public ModInstance(M perTick, T tickRate)
        {
            if (tickRate <= T.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "TickRate must be positive");
            }
            PerTick = perTick;
            TickRate = tickRate;
            Magnitude = Rational<long>.CreateChecked(perTick)/Rational<long>.CreateChecked(tickRate);
        }
        /// <summary>
        /// How much change this ModInstance wants to apply per tick.
        /// </summary>
        public M PerTick { get;}
        /// <summary>
        /// How often this ModInstance wants to apply its change.
        /// </summary>
        public T TickRate { get;}
        /// <summary>
        /// The amount of change per Delta. Used to compute which source is the strongest and should set the tick rate of the RateManagedMeter.
        /// </summary>
        public Rational<long> Magnitude { get; }
        public override string ToString()
        {
            return $"ModInstance<{typeof(T).Name},{typeof(M).Name}>: PerTick={PerTick}, TickRate={TickRate}, overall magnitude={Magnitude}";
        }
    }
}
