using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Timeline.OkazoOptionalProperties
{
    internal static class OkazoComparitor<T> where T : INumber<T>
    {
        /// <summary>
        /// Tiebreaker comparator for Okazo<T> that tries to use optional properties to compare consistently
        /// Should make sure that x!=y and that they both are occuring at the time before calling this.
        /// </summary>
        /// <param name="x">The first event to consider</param>
        /// <param name="y">The second event to consider</param>
        /// <exception cref="ArgumentException">Thrown if there's no order between events that can be determined</exception>
        /// <returns>-1 if x should come before y, 1 if x should come after y, or throws an error if it can't be distinguished.</returns>
        public static int Compare(Okazo<T> x, Okazo<T> y)
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

            // Then, transition data. Both need to have transition data of the SAME type
            if (TryGetTransitionType(x, out Type? xTransitionType) &&
                TryGetTransitionType(y, out Type? yTransitionType) &&
                xTransitionType == yTransitionType)
            {
                //Since we don't know the type at compile time, we have to use dynamic to call CompareTo.
                dynamic xOk = x;
                dynamic yOk = y;
                ret = xOk.TransitionData.CompareTo(yOk.TransitionData);
                if (ret != 0) return ret;
            }
            //Put new properties here.


            //Second to last, see if we have an object specific tiebreaker.
            if (x is IOkTiebreaker<T> okXWithTiebreaker)
            {
                ret = okXWithTiebreaker.Tiebreaker(y);
                if (y is IOkTiebreaker<T> okYWithTiebreaker)
                {
                    if (ret!=0 && okYWithTiebreaker.Tiebreaker(x)==ret) //These being equal means both think they should be first or both second.
                    {
                        ret = 0; //set it as inconclusive
                    }
                    else if(ret==0) //if X is inconclusive though, try Y
                    {
                        ret = -okYWithTiebreaker.Tiebreaker(x);
                    }
                }
            } else if (y is IOkTiebreaker<T> okYWithTiebreaker)
            {
                ret = -okYWithTiebreaker.Tiebreaker(x);
            }
            if (ret != 0) return ret;

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
            } else if (y is IOkUUID)
            {
                //If only y has a UUID, it should come after x, since it has more specific information.
                ret = -1;
            }
            if (ret != 0) return ret;
            throw new ArgumentException("Two events can't be compared but are not equal.");
        }
        /// <summary>
        /// Attempts to retrieve the generic type argument from an IOkTransition<> interface implemented by the
        /// specified object. Should only be used on Okazo<T> objects, as they are the only ones that should implement IOkTransition<>,
        /// but we shouldn't be here if they aren't Okazo<T> objects since this is a private method.
        /// </summary>
        /// <param name="obj">The object to inspect for an IOkTransition<> implementation.</param>
        /// <param name="transitionType">When this method returns, contains the transition type if found; otherwise, null.</param>
        /// <returns>true if the transition type was successfully retrieved; otherwise, false.</returns>
        private static bool TryGetTransitionType(object obj, out Type? transitionType)
        {
            transitionType = null;

            var transitionInterface = obj.GetType()
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                                    i.GetGenericTypeDefinition() == typeof(IOkTransition<>));

            if (transitionInterface != null)
            {
                transitionType = transitionInterface.GetGenericArguments()[0];
                return true;
            }

            return false;
        }
    }
}
