using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timeline
{
    /// <summary>
    /// Raised by Timeline when an action is attempted that is not allowed in the current phase.
    /// Examples include adding or adjusting events when not in the Open phase, or calling Update when in the Terminated phase.
    /// </summary>
    [Serializable]
    public class InvalidPhaseForActionException: Exception
    {
        public InvalidPhaseForActionException() : base() { }
        public InvalidPhaseForActionException(string message) : base(message) { }
        public InvalidPhaseForActionException(string message, Exception inner) : base(message, inner) { }
    }
}
