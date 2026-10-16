using CoreMetrics;
using Rationalz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Timeline;

namespace Meters
{
    /// <summary>
    /// A meter used for stats that can be modified with helpful modification helpers.
    /// Units of this are called "Ratio"
    /// </summary>
    /// <typeparam name="T">Time unit of the timeline</typeparam>
    public class StatModificationMeter<T> : InstantChangeMeter<T, Rational<long>> where T : INumber<T>
    {
        /// <summary>
        /// The value of the stat when there are no modifications.
        /// </summary>
        public int BaseStat { get; init; }
        /// <summary>
        /// Creates a new StatModificationMeter with 0 (no) buff or debuff.
        /// </summary>
        /// <param name="timeline">The timeline that owns this meter</param>
        /// <param name="baseStat">Positive value for what the stat is at no modification</param>
        /// <param name="minValue">Minimum Ratio this stat can be modified to</param>
        /// <param name="maxValue">Maximum Ratio this stat can be modified to</param>
        /// <exception cref="ArgumentException">Base stat must be positive and minValue needs to be negative and maxValue needs to be positive</exception>
        public StatModificationMeter(Timeline<T> timeline, int baseStat, Rational<long> minValue, Rational<long> maxValue): 
            base(timeline, Rational<long>.Zero, minValue, maxValue)
        {
            if (baseStat<=0)
            {
                throw new ArgumentException($"baseStat must be positive, which {baseStat} is not.");
            }
            BaseStat = baseStat;
        }
        #region Function Overrides
        public override object Clone(CloneContext context)
        {
            if (context.TryGet(this, out var existingClone))
            {
                return existingClone;
            }
            if (!context.TryGet(OwningTimeline, out Timeline<T> owningTimelineClone))
            {
                throw new UnrecoverableCloneException("Owning timeline must be cloned before cloning any meters.");
            }
            StatModificationMeter<T> clone = new(owningTimelineClone, BaseStat, MinValue, MaxValue);

            context.Register(this, clone);

            //Now update this to match
            _currentValue = CurrentValue - InstantChange;
            clone.AddInstantChange(InstantChange); //Set instant change and bring CurrentValue back to where it is here
            clone.UUID = UUID;
            return clone;
        }
        /// <summary>
        /// Changes the meter minimum to a non-positive value
        /// </summary>
        /// <param name="newMin">Target minimum</param>
        /// <param name="allowClamping">If newMin is more than the current value, can we push the value up to newMin?</param>
        /// <returns>True if we successfully changed the minimum to newMin</returns>
        /// <exception cref="ArgumentException">Minimum must be less than or equal to zero</exception>
        public override bool UpdateMin(Rational<long> newMin, bool allowClamping)
        {
            if (newMin > Rational<long>.Zero)
            {
                throw new ArgumentException($"Minimum of a Ratio must be <=0, which {newMin} is not");
            }
            return base.UpdateMin(newMin, allowClamping);
        }
        /// <summary>
        /// Changes the meter maximum to a non-negative value
        /// </summary>
        /// <param name="newMax">Target maximum</param>
        /// <param name="allowClamping">If newMax is less than the current value, can we push the value down to newMax?</param>
        /// <returns>True if we successfully changed the maximum to newMax</returns>
        /// <exception cref="ArgumentException">Maximum must be greater than or equal to zero</exception>
        public override bool UpdateMax(Rational<long> newMax, bool allowClamping)
        {
            if (newMax < Rational<long>.Zero)
            {
                throw new ArgumentException($"Maximum of a Ratio must be >=0, which {newMax} is not");
            }
            return base.UpdateMax(newMax, allowClamping);
        }
        #endregion
        #region Stat Evaluation
        /// <summary>
        /// Gives what multiplier the stat represents
        /// </summary>
        /// <returns>1+CurrentValue if positive, 1/(1+|CurrentValue|) if negative</returns>
        public Rational<long> GetMultiplier()
        {
            if (CurrentValue < Rational<long>.Zero)
            {
                return 1 / (1 - CurrentValue); //Technically add the absolute value, but since negative, subtract
            }
            return CurrentValue+1;
        }
        /// <summary>
        /// Gets the resulting stat value
        /// </summary>
        /// <returns>What stat to use. No rounding is applied</returns>
        public Rational<long> GetModifiedStat()
        {
            return BaseStat * GetMultiplier();
        }
        /// <summary>
        /// Is there overall a buff, debuff, or none?
        /// </summary>
        /// <returns></returns>
        public StatModificationType ModificationType()
        {
            if (CurrentValue<0)
            {
                return StatModificationType.Debuff;
            }
            if (CurrentValue==0)
            {
                return StatModificationType.None;
            }
            return StatModificationType.Buff;
        }
        #endregion
    }
    /// <summary>
    /// Enum to represent if a stat should be seen as a buff or debuff
    /// </summary>
    public enum StatModificationType
    {
        Debuff =-1, //Start at -1 so it matches with the sign of the Ratio
        None,
        Buff
    }
}
