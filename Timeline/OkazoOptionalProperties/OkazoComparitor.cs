using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Timeline.OkazoOptionalProperties
{
    public static class OkazoComparitor<T> where T : INumber<T>
    {
        /// <summary>
        /// Tiebreaker comparator for IOkazo<T> that tries to use optional properties to compare consistently
        /// If every test here falls through, then we should test with the class's own Tiebreaker method.
        /// </summary>
        /// <param name="x">The first event to consider</param>
        /// <param name="y">The second event to consider</param>
        /// <returns>-1 if x should come before y, 1 if x should come after y, 0 if inconclusive (or the events are the same)</returns>
        public static int Compare(IOkazo<T> x, IOkazo<T> y)
        {
            int ret = 0;

            // First, priority.
            if (x is IOkPriority)
            {
                if (y is IOkPriority)
                {
                    ret = ((IOkPriority)y).IsHigherPriorityThan((IOkPriority)x);
                }
                else { 
                    ret = -((IOkPriority)x).IsHigherThanNone(); 
                }
            } else if (y is IOkPriority)
            {
                ret = ((IOkPriority)y).IsHigherThanNone();
            }
            if (ret != 0) return ret;

            //Then, transition data. Unlike priority, both need to have transition data for it to be a valid tiebreaker
            if (x is IOkTransition<T> && y is IOkTransition<T>)
            {
                ret = ((IOkTransition<T>)x).TransitionData.CompareTo(((IOkTransition<T>)y).TransitionData);
            }
            // Possibly more tiebreakers in the future, but for now, if we can't break the tie with these, we should defer to the class's own tiebreaker method.
            return ret;
        }
    }
}
