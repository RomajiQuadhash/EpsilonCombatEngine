using CoreMetrics;
using System.Numerics;

namespace Timeline
{
    /// <summary>
    /// Holds the most recent event of those that this holder is subscribed to.
    /// TODO: Consider making it trigger its own event when the most recent event is updated, so that other objects can react to it.
    /// </summary>
    /// <typeparam name="T">Numeric type used for the timeline</typeparam>
    public class MostRecentEventHolder<T> : ICombatCloneable where T : INumber<T>
    {
        public Okazo<T>? MostRecentEvent { get;  set; }
        public void OnTrigger(object? _, Okazo<T> mostRecentEvent)
        {
            MostRecentEvent = mostRecentEvent;
        }
        public object Clone(CloneContext context)
        {
            if (context.TryGet(this, out var existingClone))
            {
                return existingClone;
            }
            var clone = new MostRecentEventHolder<T>();
            context.Register(this, clone);
            if (MostRecentEvent != null)
            {
                clone.MostRecentEvent = context.GetOrClone(MostRecentEvent);
            }
            return clone;
        }
    }
}
