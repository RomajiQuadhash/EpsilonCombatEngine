using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Timeline.OkazoOptionalProperties
{
    /// <summary>
    /// For events triggered by crossing a threshold.
    /// See the TransitionData class for more information on the data used in comparison.
    /// </summary>
    public interface IOkTransition<T> where T : INumber<T>
    {
        public TransitionData<T> TransitionData { get; }
    }
}
