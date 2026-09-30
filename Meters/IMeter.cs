using System.Numerics;

namespace Meters
{
    /// <summary>
    /// Generic that all kinds of meters with a particular timeline unit use.
    /// Useful for collections of meters with possibly different value units but all exist in the same timeline
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline<</typeparam>
    public interface IMeter<T> where T : INumber<T>
    {
    }
}
