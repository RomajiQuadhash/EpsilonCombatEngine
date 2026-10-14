using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreMetrics
{
    /// <summary>
    /// Exception thrown if a clone operation cannot be completed, such as an Event without a timeline.
    /// </summary>
    [Serializable]
    public class UnrecoverableCloneException : Exception
    {
        public UnrecoverableCloneException() : base() { }
        public UnrecoverableCloneException(string message) : base(message) { }
        public UnrecoverableCloneException(string message, Exception inner) : base(message, inner) { }
    }
}
