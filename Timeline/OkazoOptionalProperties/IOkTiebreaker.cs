using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Timeline.OkazoOptionalProperties
{
    public interface IOkTiebreaker<T> where T : INumber<T>
    {
        /// <summary>
        /// Used for the last "logical" comparison, specific to a type, before falling to UUID.
        /// Should not check any other definied optional properties.
        /// </summary>
        /// <param name="other"></param>
        /// <returns>-1 if this should occur before the other, +1 if it should occur after, and 0 if inconclusive</returns>
        public int Tiebreaker(Okazo<T> other);
    }
}
