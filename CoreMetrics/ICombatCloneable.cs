using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreMetrics
{
    public interface ICombatCloneable
    {
        /// <summary>
        /// Creates a clone of the current object using the provided CloneContext (or returns it if it has already been cloned).
        /// </summary>
        /// <param name="context">What other objects have been cloned.</param>
        /// <returns>A clone of the current object</returns>
        /// <remarks>Always start by checking CloneContext for itself</remarks>
        /// <exception cref="UnrecoverableCloneException">Standard exception to raise if something goes wrong during cloning.</exception>
        object Clone(CloneContext context);
    }
}
