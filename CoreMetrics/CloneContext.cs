using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreMetrics
{
    /// <summary>
    /// Stores information about what clones already exist and what clones are requested but not yet created.
    /// </summary>
    /// <remarks>Inspired by Deepseek written code but hand tuned</remarks>
    public sealed class CloneContext
    {
        /// <summary>
        /// Stores the original and cloned values. Uses reference equality in case the original has indistinguishable objects.
        /// Admittedly, you probably shouldn't have indistinguishable objects in the first place.
        /// </summary>
        private readonly Dictionary<object, object> _map = new(ReferenceEqualityComparer.Instance);
        /// <summary>
        /// Attempts to retrieve the clone associated with the specified original object.
        /// </summary>
        /// <typeparam name="T">The type of the original object and its clone.</typeparam>
        /// <param name="original">The original object for which to retrieve the clone.</param>
        /// <param name="clone">The cloned object to use.</param>
        /// <returns>true if a clone was found. If False, we should be using GetOrClone or do an alternate method.</returns>
        /// <remarks>Use this when linking, since by then, everything should be cloned. Only use this and not GetOrClone if you haven't registered yourself yet to avoid recursion</remarks>
        public bool TryGet<T>(T original, out T clone) where T : class
        {
            ArgumentNullException.ThrowIfNull(original);
            if (_map.TryGetValue(original, out object? existing))
            {
                clone = (T)existing;
                return true;
            }
            clone = null!;
            return false;
        }
        public void Register(object original, object clone)
        {
            ArgumentNullException.ThrowIfNull(original);
            ArgumentNullException.ThrowIfNull(clone);
            _map[original] = clone;
        }
        /// <summary>
        /// Gets an object if already cloned, otherwise clones it and returns the clone. This is a convenience method that combines TryGet and Clone.
        /// </summary>
        /// <typeparam name="T">The ICombatCloneable implementing type that should be cloned.</typeparam>
        /// <param name="original">What the object we're checking for is</param>
        /// <returns>The cloned object.</returns>
        /// <remarks>To avoid recursion, the object calling this method should be registered before calling this. If a class doesn't implement ICombatCloneable, you need to clone or copy it manually.</remarks>
        public T GetOrClone<T>(T original) where T : class, ICombatCloneable
        {
            if (TryGet(original, out T existing)) return existing;
            return (T)original.Clone(this);
        }
    }
}
