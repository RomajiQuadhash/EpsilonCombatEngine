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
            if (x is IOkPriority okXWithPriority)
            {
                if (y is IOkPriority okYWithPriority)
                {
                    ret = okYWithPriority.IsHigherPriorityThan(okXWithPriority);
                }
                else { 
                    ret = -(okXWithPriority.IsHigherThanNone()); 
                }
            } else if (y is IOkPriority okYWithPriority)
            {
                ret = okYWithPriority.IsHigherThanNone();
            }
            if (ret != 0) return ret;

            //Then, transition data. Unlike priority, both need to have transition data for it to be a valid tiebreaker
            if (x is IOkTransition<T> okXWithTransition && y is IOkTransition<T> okYWithTransition)
            {
                ret = okXWithTransition.TransitionData.CompareTo(okYWithTransition.TransitionData);
                if (ret != 0) return ret;
            }
            //Imagine other possible Tiebreaker properties here

            //Eventually, try the UUID, which should be the last tiebreaker.
            if (x is IOkUUID okXWithUUID)
            {
                if (y is IOkUUID okYWithUUID)
                {
                    ret = okXWithUUID.UUID.CompareTo(okYWithUUID.UUID);
                }
                else
                {
                    //If only x has a UUID, it should come after y, since it has more specific information.
                    ret = 1;
                }
            } if (y is IOkUUID)
            {
                //If only y has a UUID, it should come after x, since it has more specific information.
                ret = -1;
            }
            return ret;
        }
    }
}
