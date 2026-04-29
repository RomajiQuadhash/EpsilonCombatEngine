using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Timeline
{
    /// <summary>
    /// Interface for objects that can be advanced by the timeline.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    public interface IAdvanceable<T> where T : INumber<T>
    {
        /// <summary>
        /// Handles the advancement event by processing the elapsed time since the last update.
        /// </summary>
        /// <param name="sender">The Timeline advancing time</param>
        /// <param name="timeElapsed">The amount of time to move the event by.</param>
        public void OnAdvance(object? sender, T timeElapsed);
    }
}
