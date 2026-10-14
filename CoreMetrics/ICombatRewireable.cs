using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreMetrics
{
    /// <summary>
    /// Implementing this interface means that you have subscriptions that you can't trust Cloning to create.
    /// </summary>
    public interface ICombatRewireable
    {

        /// <summary>
        /// Called on the original. Re-establishes extrinsic subscriptions on the clone
        /// using the CloneContext to find cloned subscribers.
        /// </summary>
        /// <remarks>The CloneContext should have everything. If not, expect UnrecoverableCloneExceptions</remarks>
        void Rewire(CloneContext context);
    }
}
