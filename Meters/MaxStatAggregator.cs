using CoreMetrics;
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
    /// Gets what the maximum of a stat is across all the 
    /// </summary>
    /// <typeparam name="T">Time unit of the timeline</typeparam>
    /// <typeparam name="M">The type of the stat modification meter to use</typeparam>
    public class MaxStatAggregator<T,M> : ICombatCloneable where T : INumber<T> where M : StatModificationMeter<T>
    {
        public ISet<M> Meters { get; init; }

        public MaxStatAggregator()
        {
            Meters = new HashSet<M>();
        }
        public Rational<long> GetMaximum()
        {
            Rational<long> ret = 0;
            foreach (var meter in Meters)
            {
                var curVal=meter.GetModifiedStat();
                if (curVal>ret)
                {
                    ret = curVal;
                }
            }
            return ret;
        }

        public object Clone(CloneContext context)
        {
            if (context.TryGet(this,out var existingClone))
            {
                return existingClone;
            }
            MaxStatAggregator<T, M> clone = new();
            context.Register(this,clone);

            //Now try to fill it with everything. 
            foreach (var meter in Meters)
            {
                //Safe to use GetOrClone since if the meter type relies on this existing, it is registered now
                //It's also okay if they add themselves in their constructor, since then the Add does nothing
                clone.Meters.Add(context.GetOrClone(meter));
            }
            return clone;
        }
    }
}
