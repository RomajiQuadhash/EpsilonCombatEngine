using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Combat
{
    /// <summary>
    /// Thrown if an Action is taken when it shouldn't be (like the one performing the action is not existing or active in the timeline)
    /// </summary>
    public class InvalidActionException : Exception
    {
        public InvalidActionException() { }
        public InvalidActionException(string message) : base(message) { }
        public InvalidActionException(string message, Exception innerException) : base(message, innerException) { }
    }
}
